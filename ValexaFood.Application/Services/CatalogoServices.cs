using Microsoft.EntityFrameworkCore;
using ValexaFood.Application.Common;
using ValexaFood.Domain.Entities;
using ValexaFood.Infrastructure.Context;

namespace ValexaFood.Application.Services;

public record RolDto(int RolId, string Nombre, string Descripcion, bool VeDinero, bool OcupaCupo, int Orden, int UsuariosActivos);
public record FuncionDto(string Codigo, string Nombre, string Descripcion, string Grupo, int Orden, List<int> PlanesIds);
public record PermisoDto(int PermisoId, string Codigo, string Nombre, string Modulo, string Descripcion,
    string? FuncionRequerida, string? FuncionNombre, List<int> Roles);
public record MatrizPermisosDto(List<RolDto> Roles, List<PermisoDto> Permisos);
public record RolPermisosGuardarDto(List<int>? PermisoIds);
public record ConfiguracionSaasDto(string? WhatsappVentas, string? EmailSoporte, decimal DescuentoBienvenidaPct,
    int DiasPorVencer);

public class CatalogoService
{
    private readonly ValexaFoodContext _db;
    private readonly BitacoraService _bitacora;

    // Permisos que manejan dinero: un rol que "no ve dinero" (Cocinero, Mesero,
    // Delivery) no puede recibirlos. Regla de oro: cocina no ve dinero.
    private static readonly string[] PermisosDeDinero = ["VER_MONTOS", "COBRAR", "CAJA_ABRIR_CERRAR", "CAJA_ARQUEO", "CAJA_EGRESOS",
        "NOTA_VENTA_EMITIR", "FACTURA_EMITIR"];
    // Sin estos el Administrador no podría ni arreglar su propia empresa.
    private static readonly string[] PermisosMinimosAdmin = ["USUARIOS_GESTIONAR", "SUCURSALES_GESTIONAR", "CONFIGURACION"];

    public CatalogoService(ValexaFoodContext db, BitacoraService bitacora)
    {
        _db = db;
        _bitacora = bitacora;
    }

    public async Task<List<RolDto>> RolesAsync()
    {
        var activos = await _db.Usuarios.IgnoreQueryFilters().Where(u => u.Activo)
            .GroupBy(u => u.RolId).Select(g => new { RolId = g.Key, N = g.Count() }).ToListAsync();
        return (await _db.Roles.AsNoTracking().OrderBy(r => r.Orden).ToListAsync())
            .Select(r => new RolDto(r.RolId, r.Nombre, r.Descripcion, r.VeDinero, r.OcupaCupo, r.Orden,
                activos.FirstOrDefault(a => a.RolId == r.RolId)?.N ?? 0)).ToList();
    }

    public async Task<List<FuncionDto>> FuncionesAsync()
    {
        var pf = await _db.PlanFunciones.AsNoTracking().ToListAsync();
        return (await _db.Funciones.AsNoTracking().OrderBy(f => f.Orden).ToListAsync())
            .Select(f => new FuncionDto(f.Codigo, f.Nombre, f.Descripcion, f.Grupo, f.Orden,
                pf.Where(x => x.FuncionCodigo == f.Codigo).Select(x => x.PlanId).ToList())).ToList();
    }

    public async Task<MatrizPermisosDto> MatrizAsync()
    {
        var roles = (await RolesAsync()).Where(r => r.RolId != RolIds.SuperAdmin).ToList();
        var funciones = await _db.Funciones.AsNoTracking().ToDictionaryAsync(f => f.Codigo, f => f.Nombre);
        var rp = await _db.RolPermisos.AsNoTracking().ToListAsync();
        var permisos = (await _db.Permisos.AsNoTracking().OrderBy(p => p.Orden).ToListAsync())
            .Select(p => new PermisoDto(p.PermisoId, p.Codigo, p.Nombre, p.Modulo, p.Descripcion, p.FuncionRequerida,
                p.FuncionRequerida == null ? null : funciones.GetValueOrDefault(p.FuncionRequerida),
                rp.Where(x => x.PermisoId == p.PermisoId).Select(x => x.RolId).OrderBy(x => x).ToList())).ToList();
        return new MatrizPermisosDto(roles, permisos);
    }

    public async Task<MatrizPermisosDto> GuardarPermisosRolAsync(int rolId, RolPermisosGuardarDto dto)
    {
        var rol = await _db.Roles.FirstOrDefaultAsync(r => r.RolId == rolId) ?? throw ReglaNegocioException.NoEncontrado("Rol");
        if (rolId == RolIds.SuperAdmin) throw new ReglaNegocioException("Los permisos del SuperAdmin no se editan.");

        var ids = (dto.PermisoIds ?? []).Distinct().ToList();
        var permisos = await _db.Permisos.Where(p => ids.Contains(p.PermisoId)).ToListAsync();
        if (permisos.Count != ids.Count) throw new ReglaNegocioException("Hay permisos que no existen.");

        var codigos = permisos.Select(p => p.Codigo).ToHashSet();
        if (!rol.VeDinero)
        {
            var dinero = permisos.Where(p => PermisosDeDinero.Contains(p.Codigo)).Select(p => p.Nombre).ToList();
            if (dinero.Count > 0)
                throw new ReglaNegocioException($"El rol {rol.Nombre} no maneja dinero: no puede tener {string.Join(", ", dinero)}.");
        }
        if (rolId == RolIds.Administrador)
        {
            var faltan = PermisosMinimosAdmin.Where(c => !codigos.Contains(c)).ToList();
            if (faltan.Count > 0)
                throw new ReglaNegocioException("El Administrador debe conservar la gestión de usuarios, sucursales y configuración.");
        }

        var actuales = await _db.RolPermisos.Where(x => x.RolId == rolId).ToListAsync();
        var quitados = actuales.Where(a => !ids.Contains(a.PermisoId)).ToList();
        var nuevos = ids.Where(id => actuales.All(a => a.PermisoId != id)).ToList();
        if (quitados.Count == 0 && nuevos.Count == 0) return await MatrizAsync();

        _db.RolPermisos.RemoveRange(quitados);
        _db.RolPermisos.AddRange(nuevos.Select(id => new RolPermiso { RolId = rolId, PermisoId = id }));
        var nombres = await _db.Permisos.ToDictionaryAsync(p => p.PermisoId, p => p.Nombre);
        var partes = new List<string>();
        if (nuevos.Count > 0) partes.Add("agregados: " + string.Join(", ", nuevos.Select(n => nombres[n])));
        if (quitados.Count > 0) partes.Add("quitados: " + string.Join(", ", quitados.Select(q => nombres[q.PermisoId])));
        await _bitacora.AgregarAsync(null, "PERMISOS_ROL_EDITADOS", "Rol", rolId, $"Rol {rol.Nombre}, {string.Join("; ", partes)}.");
        await _db.SaveChangesAsync();
        return await MatrizAsync();
    }

    public async Task<ConfiguracionSaasDto> ConfiguracionAsync()
    {
        var c = await _db.ConfiguracionSaas.AsNoTracking().FirstOrDefaultAsync() ?? new ConfiguracionSaas();
        return new(c.WhatsappVentas, c.EmailSoporte, c.DescuentoBienvenidaPct, c.DiasPorVencer);
    }

    public async Task<ConfiguracionSaasDto> GuardarConfiguracionAsync(ConfiguracionSaasDto dto)
    {
        if (dto.DescuentoBienvenidaPct is < 0 or > 100) throw new ReglaNegocioException("El descuento de bienvenida va de 0 a 100 %.");
        if (dto.DiasPorVencer is < 1 or > 60) throw new ReglaNegocioException("Los días de aviso van de 1 a 60.");
        var wa = Documento.NormalizarTelefono(dto.WhatsappVentas);
        if (wa != null && !Documento.EsCelularPeru(wa)) throw new ReglaNegocioException("El WhatsApp de ventas debe ser un celular peruano (9 dígitos, empieza con 9).");
        var email = Texto.Limpio(dto.EmailSoporte) == null ? null : Documento.NormalizarEmail(dto.EmailSoporte, "Correo de soporte");

        var c = await _db.ConfiguracionSaas.FirstOrDefaultAsync();
        if (c == null) { c = new ConfiguracionSaas { Id = 1 }; _db.ConfiguracionSaas.Add(c); }
        c.WhatsappVentas = wa;
        c.EmailSoporte = email;
        c.DescuentoBienvenidaPct = dto.DescuentoBienvenidaPct;
        c.DiasPorVencer = dto.DiasPorVencer;
        c.UpdatedAt = DateTime.UtcNow;
        await _bitacora.AgregarAsync(null, "CONFIGURACION_SAAS", "ConfiguracionSaas", 1,
            $"Bienvenida {dto.DescuentoBienvenidaPct:0.##} %, aviso {dto.DiasPorVencer} días, WhatsApp {wa ?? "—"}.");
        await _db.SaveChangesAsync();
        return await ConfiguracionAsync();
    }
}
