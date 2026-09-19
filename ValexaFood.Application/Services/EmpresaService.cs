using Microsoft.EntityFrameworkCore;
using ValexaFood.Application.Common;
using ValexaFood.Domain.Entities;
using ValexaFood.Infrastructure.Context;

namespace ValexaFood.Application.Services;

public static class TiposServicio
{
    public static readonly string[] Todos = ["RESTAURANTE", "CEVICHERIA", "POLLERIA", "CHIFA", "CAFETERIA", "FAST_FOOD", "PIZZERIA", "BAR", "OTRO"];
}

public record EmpresaListaDto(int TenantId, string NombreComercial, string? RazonSocial, string? Ruc, string TipoServicio,
    string? Telefono, int? PlanId, string? PlanNombre, bool Activo, DateOnly? Vencimiento, int? DiasParaVencer,
    string EstadoCobranza, int Sucursales, int MaxSucursales, int Usuarios, int MaxUsuarios, bool Excede,
    DateTime? UltimoAcceso, DateTime CreatedAt);

public record UsoRolDto(int RolId, string Rol, int Usados, int? Maximo, bool Permitido);
public record SucursalDto(int SucursalId, string Nombre, string? Direccion, string? Telefono, bool EsPrincipal, bool Activo,
    int Usuarios, DateTime CreatedAt);
public record UsuarioEmpresaDto(int UsuarioId, string Nombre, string Email, string? Dni, string? Telefono, int RolId,
    string Rol, int? SucursalId, string? Sucursal, bool Activo, bool Bloqueado, DateTime? BloqueadoHasta,
    bool DebeCambiarPassword, DateTime? UltimoLogin, DateTime CreatedAt);
public record AccesoDto(long AccesoId, string Email, string? Usuario, bool Exito, string? Motivo, string? Ip, DateTime Fecha);

public record EmpresaDetalleDto(int TenantId, string NombreComercial, string? RazonSocial, string? Ruc, bool RucValido,
    string? Direccion, string? Telefono, string? Email, string? ContactoNombre, string TipoServicio, bool Activo,
    string? MotivoSuspension, DateTime? FechaSuspension, string? NotasInternas, DateTime CreatedAt,
    PlanDto? Plan, DateOnly? PlanFechaInicio, DateOnly? Vencimiento, int? DiasParaVencer, string EstadoCobranza,
    int SucursalesActivas, int UsuariosOperativos, List<UsoRolDto> UsoPorRol, List<string> Excesos,
    List<SucursalDto> SucursalesLista, List<UsuarioEmpresaDto> UsuariosLista, DateTime? UltimoAcceso, bool CelularPeru);

public record AdminInicialDto(string? Nombre, string? Email, string? Dni, string? Telefono, string? Password);
public record EmpresaCrearDto(string? NombreComercial, string? RazonSocial, string? Ruc, string? Direccion, string? Telefono,
    string? Email, string? ContactoNombre, string? TipoServicio, int PlanId, string? SucursalNombre, string? SucursalDireccion,
    AdminInicialDto? Admin, RegistrarMovimientoDto? PrimerCobro);
public record EmpresaEditarDto(string? NombreComercial, string? RazonSocial, string? Ruc, string? Direccion, string? Telefono,
    string? Email, string? ContactoNombre, string? TipoServicio);
public record EmpresaCreadaDto(EmpresaDetalleDto Empresa, string? PasswordTemporal, string? Advertencia);
public record NotasDto(string? Notas);
public record CambiarPlanDto(int PlanId);
public record MotivoDto(string? Motivo);
public record SucursalGuardarDto(string? Nombre, string? Direccion, string? Telefono);
public record EstadoDto(bool Activo);
public record UsuarioGuardarDto(string? Nombre, string? Email, string? Dni, string? Telefono, int RolId, int? SucursalId, string? Password);
public record PasswordTemporalDto(string PasswordTemporal, string Mensaje);

public class EmpresaService
{
    private readonly ValexaFoodContext _db;
    private readonly LimitePlan _limite;
    private readonly BitacoraService _bitacora;
    private readonly CacheSesiones _cache;
    private readonly SuscripcionService _suscripciones;
    private readonly PlanService _planes;
    private readonly AuthService _auth;

    public EmpresaService(ValexaFoodContext db, LimitePlan limite, BitacoraService bitacora, CacheSesiones cache,
        SuscripcionService suscripciones, PlanService planes, AuthService auth)
    {
        _db = db;
        _limite = limite;
        _bitacora = bitacora;
        _cache = cache;
        _suscripciones = suscripciones;
        _planes = planes;
        _auth = auth;
    }

    private async Task<int> DiasPorVencerAsync() =>
        await _db.ConfiguracionSaas.Select(c => (int?)c.DiasPorVencer).FirstOrDefaultAsync() ?? 7;

    private async Task<Tenant> TenantAsync(int id) =>
        await _db.Tenants.Include(t => t.Plan).FirstOrDefaultAsync(t => t.TenantId == id)
        ?? throw ReglaNegocioException.NoEncontrado("Empresa");

    // ═════════════════════════════════════════════════════════════════════════
    // LISTADO Y FICHA
    // ═════════════════════════════════════════════════════════════════════════
    public async Task<List<EmpresaListaDto>> ListarAsync()
    {
        var hoy = DiaLima.Hoy();
        var porVencer = await DiasPorVencerAsync();
        var tenants = await _db.Tenants.AsNoTracking().Include(t => t.Plan).OrderBy(t => t.NombreComercial).ToListAsync();
        var uso = await _limite.UsoAsync();
        var roles = await _limite.NombresRolAsync();
        var cupos = await _db.PlanRolCupos.AsNoTracking().ToListAsync();
        var accesos = await _db.AccesosLog.AsNoTracking().Where(a => a.Exito && a.TenantId != null)
            .GroupBy(a => a.TenantId).Select(g => new { TenantId = g.Key!.Value, Ultimo = g.Max(a => a.Fecha) }).ToListAsync();

        return tenants.Select(t =>
        {
            var u = uso.GetValueOrDefault(t.TenantId) ?? new UsoEmpresa(t.TenantId, 0, [], false);
            int? dias = t.PlanFechaVencimiento.HasValue ? t.PlanFechaVencimiento.Value.DayNumber - hoy.DayNumber : null;
            var excede = t.Plan != null && LimitePlan.Problemas(u, t.Plan.MaxSucursales, t.Plan.MaxUsuarios,
                cupos.Where(c => c.PlanId == t.PlanId).Select(c => (c.RolId, c.Maximo)), roles).Count > 0;
            return new EmpresaListaDto(t.TenantId, t.NombreComercial, t.RazonSocial, t.Ruc, t.TipoServicio, t.Telefono,
                t.PlanId, t.Plan?.Nombre, t.Activo, t.PlanFechaVencimiento, dias,
                SuscripcionService.Estado(t, dias, porVencer), u.Sucursales, t.Plan?.MaxSucursales ?? 0,
                u.UsuariosOperativos, t.Plan?.MaxUsuarios ?? 0, excede,
                accesos.FirstOrDefault(a => a.TenantId == t.TenantId)?.Ultimo, t.CreatedAt);
        }).ToList();
    }

    public async Task<EmpresaDetalleDto> DetalleAsync(int id)
    {
        var t = await _db.Tenants.AsNoTracking().Include(x => x.Plan).FirstOrDefaultAsync(x => x.TenantId == id)
            ?? throw ReglaNegocioException.NoEncontrado("Empresa");
        var hoy = DiaLima.Hoy();
        int? dias = t.PlanFechaVencimiento.HasValue ? t.PlanFechaVencimiento.Value.DayNumber - hoy.DayNumber : null;
        var uso = (await _limite.UsoAsync([id]))[id];
        var roles = await _db.Roles.AsNoTracking().Where(r => r.RolId >= RolIds.Cajero).OrderBy(r => r.Orden).ToListAsync();
        var plan = t.PlanId.HasValue ? await _planes.ObtenerAsync(t.PlanId.Value) : null;

        var usoPorRol = roles.Select(r =>
        {
            var cupo = plan?.Cupos.FirstOrDefault(c => c.RolId == r.RolId);
            return new UsoRolDto(r.RolId, r.Nombre, uso.UsuariosPorRol.GetValueOrDefault(r.RolId), cupo?.Maximo, cupo != null);
        }).ToList();

        var excesos = plan == null ? [] : LimitePlan.Problemas(uso, plan.MaxSucursales, plan.MaxUsuarios,
            plan.Cupos.Select(c => (c.RolId, c.Maximo)), roles.ToDictionary(r => r.RolId, r => r.Nombre));

        var sucursales = await _db.Sucursales.IgnoreQueryFilters().AsNoTracking().Where(s => s.TenantId == id)
            .OrderByDescending(s => s.EsPrincipal).ThenBy(s => s.Nombre).ToListAsync();
        var usuarios = await UsuariosAsync(id);
        var ultimoAcceso = await _db.AccesosLog.AsNoTracking().Where(a => a.TenantId == id && a.Exito)
            .MaxAsync(a => (DateTime?)a.Fecha);

        return new EmpresaDetalleDto(t.TenantId, t.NombreComercial, t.RazonSocial, t.Ruc, Documento.RucValido(t.Ruc),
            t.Direccion, t.Telefono, t.Email, t.ContactoNombre, t.TipoServicio, t.Activo, t.MotivoSuspension,
            t.FechaSuspension, t.NotasInternas, t.CreatedAt, plan, t.PlanFechaInicio, t.PlanFechaVencimiento, dias,
            SuscripcionService.Estado(t, dias, await DiasPorVencerAsync()), uso.Sucursales, uso.UsuariosOperativos,
            usoPorRol, excesos,
            sucursales.Select(s => new SucursalDto(s.SucursalId, s.Nombre, s.Direccion, s.Telefono, s.EsPrincipal, s.Activo,
                usuarios.Count(u => u.SucursalId == s.SucursalId && u.Activo), s.CreatedAt)).ToList(),
            usuarios, ultimoAcceso, Documento.EsCelularPeru(t.Telefono));
    }

    private async Task<List<UsuarioEmpresaDto>> UsuariosAsync(int tenantId)
    {
        var ahora = DateTime.UtcNow;
        return (await _db.Usuarios.IgnoreQueryFilters().AsNoTracking().Include(u => u.Rol).Include(u => u.Sucursal)
                .Where(u => u.TenantId == tenantId).OrderBy(u => u.RolId).ThenBy(u => u.Nombre).ToListAsync())
            .Select(u => new UsuarioEmpresaDto(u.UsuarioId, u.Nombre, u.Email, u.Dni, u.Telefono, u.RolId, u.Rol?.Nombre ?? "",
                u.SucursalId, u.Sucursal?.Nombre, u.Activo, u.BloqueadoHasta > ahora, u.BloqueadoHasta,
                u.DebeCambiarPassword, u.UltimoLogin, u.CreatedAt)).ToList();
    }

    public async Task<List<AccesoDto>> AccesosAsync(int tenantId, int limite)
    {
        var filas = await _db.AccesosLog.AsNoTracking().Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.AccesoId).Take(Math.Clamp(limite, 1, 500)).ToListAsync();
        var ids = filas.Where(f => f.UsuarioId.HasValue).Select(f => f.UsuarioId!.Value).Distinct().ToList();
        var nombres = await _db.Usuarios.IgnoreQueryFilters().Where(u => ids.Contains(u.UsuarioId))
            .ToDictionaryAsync(u => u.UsuarioId, u => u.Nombre);
        return filas.Select(a => new AccesoDto(a.AccesoId, a.Email,
            a.UsuarioId.HasValue ? nombres.GetValueOrDefault(a.UsuarioId.Value) : null, a.Exito, a.Motivo, a.Ip, a.Fecha)).ToList();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // ALTA: empresa + sucursal principal + Administrador + primer cobro, todo o nada.
    // ═════════════════════════════════════════════════════════════════════════
    public async Task<EmpresaCreadaDto> CrearAsync(EmpresaCrearDto dto)
    {
        var datos = ValidarDatos(new EmpresaEditarDto(dto.NombreComercial, dto.RazonSocial, dto.Ruc, dto.Direccion,
            dto.Telefono, dto.Email, dto.ContactoNombre, dto.TipoServicio));
        await ValidarUnicosAsync(null, datos.NombreComercial!, datos.Ruc);

        var plan = await _db.Planes.FirstOrDefaultAsync(p => p.PlanId == dto.PlanId)
            ?? throw new ReglaNegocioException("Elige un plan para la empresa.");
        if (!plan.Activo) throw new ReglaNegocioException($"El plan {plan.Nombre} está desactivado: elige otro.");

        var sucursalNombre = Texto.Opcional(dto.SucursalNombre, "Nombre de la sucursal", 100) ?? "Principal";
        var sucursalDireccion = Texto.Opcional(dto.SucursalDireccion, "Dirección de la sucursal", 250) ?? datos.Direccion;

        var a = dto.Admin ?? throw new ReglaNegocioException("Faltan los datos del Administrador.");
        var adminNombre = Texto.Requerido(a.Nombre, "Nombre del Administrador", 120, 3);
        var adminEmail = Documento.NormalizarEmail(a.Email, "Correo del Administrador");
        var adminDni = Documento.NormalizarDni(a.Dni);
        var adminTel = Documento.NormalizarTelefono(a.Telefono);
        await ValidarEmailLibreAsync(adminEmail, null);
        string? temporal = null;
        string password;
        if (string.IsNullOrEmpty(a.Password)) password = temporal = Clave.GenerarTemporal();
        else { Clave.ValidarFortaleza(a.Password); password = a.Password; }

        var cobro = dto.PrimerCobro ?? throw new ReglaNegocioException(
            "Registra el primer cobro o una cortesía: ningún día de servicio sin su constancia.");

        int tenantId = 0;
        string? advertencia = null;
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            var ahora = DateTime.UtcNow;
            var tenant = new Tenant
            {
                NombreComercial = datos.NombreComercial!, RazonSocial = datos.RazonSocial, Ruc = datos.Ruc,
                Direccion = datos.Direccion, Telefono = datos.Telefono, Email = datos.Email,
                ContactoNombre = datos.ContactoNombre, TipoServicio = datos.TipoServicio!, PlanId = plan.PlanId,
                Activo = true, CreatedAt = ahora, UpdatedAt = ahora
            };
            _db.Tenants.Add(tenant);
            await _db.SaveChangesAsync();

            // El SuperAdmin no tiene empresa: el tenant_id de cada fila hija va explícito.
            var sucursal = new Sucursal
            {
                TenantId = tenant.TenantId, Nombre = sucursalNombre, Direccion = sucursalDireccion,
                Telefono = datos.Telefono, EsPrincipal = true, Activo = true, CreatedAt = ahora
            };
            _db.Sucursales.Add(sucursal);
            // Toda empresa nace con sus áreas de preparación básicas (se editan en la Carta).
            _db.Areas.Add(new AreaPreparacion { TenantId = tenant.TenantId, Nombre = "Cocina", Color = "#801D31", Orden = 1, CreatedAt = ahora });
            _db.Areas.Add(new AreaPreparacion { TenantId = tenant.TenantId, Nombre = "Bar", Color = "#B08834", Orden = 2, CreatedAt = ahora });
            await _db.SaveChangesAsync();

            var admin = new Usuario
            {
                TenantId = tenant.TenantId, SucursalId = sucursal.SucursalId, RolId = RolIds.Administrador,
                Nombre = adminNombre, Email = adminEmail, Dni = adminDni, Telefono = adminTel,
                PasswordHash = Clave.Hash(password), Activo = true, DebeCambiarPassword = true, CreatedAt = ahora
            };
            _db.Usuarios.Add(admin);
            await _bitacora.AgregarAsync(tenant.TenantId, "EMPRESA_CREADA", "Empresa", tenant.TenantId,
                $"Alta de {tenant.NombreComercial} con plan {plan.Nombre}, sucursal {sucursalNombre} y Administrador {adminNombre} ({adminEmail}).");
            await _db.SaveChangesAsync();

            tenant.Plan = plan;
            (_, advertencia) = await _suscripciones.RegistrarEnTransaccionAsync(tenant, cobro);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            tenantId = tenant.TenantId;
        });

        return new EmpresaCreadaDto(await DetalleAsync(tenantId), temporal, advertencia);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // DATOS, NOTAS, PLAN Y ESTADO
    // ═════════════════════════════════════════════════════════════════════════
    public async Task<EmpresaDetalleDto> ActualizarAsync(int id, EmpresaEditarDto dto)
    {
        var t = await TenantAsync(id);
        var d = ValidarDatos(dto);
        await ValidarUnicosAsync(id, d.NombreComercial!, d.Ruc);

        var cambios = new List<string>();
        void Cambio(string campo, string? antes, string? despues)
        {
            if ((antes ?? "") != (despues ?? "")) cambios.Add($"{campo}: '{antes}' → '{despues}'");
        }
        Cambio("Nombre", t.NombreComercial, d.NombreComercial);
        Cambio("Razón social", t.RazonSocial, d.RazonSocial);
        Cambio("RUC", t.Ruc, d.Ruc);
        Cambio("Dirección", t.Direccion, d.Direccion);
        Cambio("Teléfono", t.Telefono, d.Telefono);
        Cambio("Correo", t.Email, d.Email);
        Cambio("Contacto", t.ContactoNombre, d.ContactoNombre);
        Cambio("Tipo", t.TipoServicio, d.TipoServicio);

        if (t.Ruc != d.Ruc) t.RucVerificadoEn = null;
        t.NombreComercial = d.NombreComercial!;
        t.RazonSocial = d.RazonSocial;
        t.Ruc = d.Ruc;
        t.Direccion = d.Direccion;
        t.Telefono = d.Telefono;
        t.Email = d.Email;
        t.ContactoNombre = d.ContactoNombre;
        t.TipoServicio = d.TipoServicio!;
        t.UpdatedAt = DateTime.UtcNow;
        if (cambios.Count > 0)
            await _bitacora.AgregarAsync(id, "EMPRESA_EDITADA", "Empresa", id, string.Join("; ", cambios));
        await _db.SaveChangesAsync();
        return await DetalleAsync(id);
    }

    public async Task GuardarNotasAsync(int id, NotasDto dto)
    {
        var t = await TenantAsync(id);
        // Se respetan los saltos de línea: son notas libres del SuperAdmin.
        var notas = dto.Notas?.Trim();
        if (notas?.Length > 4000) throw new ReglaNegocioException("Las notas admiten hasta 4000 caracteres.");
        t.NotasInternas = string.IsNullOrEmpty(notas) ? null : notas;
        t.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<EmpresaDetalleDto> CambiarPlanAsync(int id, CambiarPlanDto dto)
    {
        var t = await TenantAsync(id);
        if (t.PlanId == dto.PlanId) throw new ReglaNegocioException("La empresa ya está en ese plan.");
        var nuevo = await _planes.ObtenerAsync(dto.PlanId);
        if (!nuevo.Activo) throw new ReglaNegocioException($"El plan {nuevo.Nombre} está desactivado.");

        var uso = (await _limite.UsoAsync([id]))[id];
        var problemas = LimitePlan.Problemas(uso, nuevo.MaxSucursales, nuevo.MaxUsuarios,
            nuevo.Cupos.Select(c => (c.RolId, c.Maximo)), await _limite.NombresRolAsync());
        if (problemas.Count > 0)
            throw new ReglaNegocioException(
                $"{t.NombreComercial} no entra en el plan {nuevo.Nombre}: {string.Join("; ", problemas)}. Desactiva lo que sobra primero.",
                400, new { problemas });

        var anterior = t.Plan?.Nombre ?? "sin plan";
        t.PlanId = nuevo.PlanId;
        t.UpdatedAt = DateTime.UtcNow;
        await _bitacora.AgregarAsync(id, "EMPRESA_CAMBIO_PLAN", "Empresa", id,
            $"Plan {anterior} → {nuevo.Nombre}. El vencimiento no cambia ({t.PlanFechaVencimiento:dd/MM/yyyy}); el nuevo precio rige desde el próximo cobro.");
        await _db.SaveChangesAsync();
        _cache.Empresa(id);
        return await DetalleAsync(id);
    }

    public async Task<EmpresaDetalleDto> SuspenderAsync(int id, MotivoDto dto)
    {
        var motivo = Texto.Requerido(dto.Motivo, "Motivo de la suspensión", 300, 5);
        var t = await TenantAsync(id);
        if (!t.Activo) throw new ReglaNegocioException("La empresa ya está suspendida.");
        t.Activo = false;
        t.MotivoSuspension = motivo;
        t.FechaSuspension = DateTime.UtcNow;
        t.UpdatedAt = DateTime.UtcNow;
        await _bitacora.AgregarAsync(id, "EMPRESA_SUSPENDIDA", "Empresa", id, $"Suspendida: {motivo}");
        await _db.SaveChangesAsync();
        _cache.Empresa(id);
        return await DetalleAsync(id);
    }

    public async Task<EmpresaDetalleDto> ReactivarAsync(int id)
    {
        var t = await TenantAsync(id);
        if (t.Activo) throw new ReglaNegocioException("La empresa ya está activa.");
        t.Activo = true;
        t.MotivoSuspension = null;
        t.FechaSuspension = null;
        t.UpdatedAt = DateTime.UtcNow;
        await _bitacora.AgregarAsync(id, "EMPRESA_REACTIVADA", "Empresa", id,
            PlanVigencia.EstaVencido(t.PlanFechaVencimiento)
                ? "Reactivada, pero su plan está vencido: no podrá entrar hasta registrar un cobro."
                : "Reactivada.");
        await _db.SaveChangesAsync();
        _cache.Empresa(id);
        return await DetalleAsync(id);
    }

    public async Task<int> CerrarSesionesEmpresaAsync(int id)
    {
        await TenantAsync(id);
        var ids = await _db.Usuarios.IgnoreQueryFilters().Where(u => u.TenantId == id).Select(u => u.UsuarioId).ToListAsync();
        await _db.Usuarios.IgnoreQueryFilters().Where(u => u.TenantId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.SesionVersion, u => u.SesionVersion + 1));
        await _db.RefreshTokens.Where(r => ids.Contains(r.UsuarioId) && !r.Revoked)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Revoked, true));
        foreach (var u in ids) _cache.Usuario(u);
        await _bitacora.AgregarAsync(id, "EMPRESA_SESIONES_CERRADAS", "Empresa", id, $"Se cerraron las sesiones de {ids.Count} usuario(s).");
        await _db.SaveChangesAsync();
        return ids.Count;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // SUCURSALES
    // ═════════════════════════════════════════════════════════════════════════
    private async Task<Sucursal> SucursalAsync(int tenantId, int sucursalId) =>
        await _db.Sucursales.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.TenantId == tenantId && s.SucursalId == sucursalId)
        ?? throw ReglaNegocioException.NoEncontrado("Sucursal");

    private async Task ValidarNombreSucursalAsync(int tenantId, string nombre, int? excluir)
    {
        if (await _db.Sucursales.IgnoreQueryFilters().AnyAsync(s => s.TenantId == tenantId
                && s.Nombre.ToLower() == nombre.ToLower() && s.SucursalId != excluir))
            throw new ReglaNegocioException($"Ya existe una sucursal llamada {nombre} en esta empresa.");
    }

    public async Task<EmpresaDetalleDto> CrearSucursalAsync(int tenantId, SucursalGuardarDto dto)
    {
        await TenantAsync(tenantId);
        var nombre = Texto.Requerido(dto.Nombre, "Nombre de la sucursal", 100, 2);
        await ValidarNombreSucursalAsync(tenantId, nombre, null);
        var s = new Sucursal
        {
            TenantId = tenantId, Nombre = nombre, Direccion = Texto.Opcional(dto.Direccion, "Dirección", 250),
            Telefono = Documento.NormalizarTelefono(dto.Telefono), Activo = true, CreatedAt = DateTime.UtcNow,
            EsPrincipal = !await _db.Sucursales.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId && x.EsPrincipal)
        };
        _db.Sucursales.Add(s);
        await _bitacora.AgregarAsync(tenantId, "SUCURSAL_CREADA", "Sucursal", null, $"Nueva sucursal {nombre}.");
        await _db.SaveChangesAsync();
        return await DetalleAsync(tenantId);
    }

    public async Task<EmpresaDetalleDto> EditarSucursalAsync(int tenantId, int sucursalId, SucursalGuardarDto dto)
    {
        var s = await SucursalAsync(tenantId, sucursalId);
        var nombre = Texto.Requerido(dto.Nombre, "Nombre de la sucursal", 100, 2);
        await ValidarNombreSucursalAsync(tenantId, nombre, sucursalId);
        if (s.Nombre != nombre)
            await _bitacora.AgregarAsync(tenantId, "SUCURSAL_EDITADA", "Sucursal", sucursalId, $"Sucursal {s.Nombre} → {nombre}.");
        s.Nombre = nombre;
        s.Direccion = Texto.Opcional(dto.Direccion, "Dirección", 250);
        s.Telefono = Documento.NormalizarTelefono(dto.Telefono);
        await _db.SaveChangesAsync();
        return await DetalleAsync(tenantId);
    }

    public async Task<EmpresaDetalleDto> EstadoSucursalAsync(int tenantId, int sucursalId, EstadoDto dto)
    {
        var s = await SucursalAsync(tenantId, sucursalId);
        if (s.Activo == dto.Activo) throw new ReglaNegocioException(dto.Activo ? "La sucursal ya está activa." : "La sucursal ya está desactivada.");
        if (!dto.Activo)
        {
            if (s.EsPrincipal) throw new ReglaNegocioException("La sucursal principal no se desactiva: marca otra como principal primero.");
            var asignados = await _db.Usuarios.IgnoreQueryFilters().CountAsync(u => u.SucursalId == sucursalId && u.Activo);
            if (asignados > 0)
                throw new ReglaNegocioException($"La sucursal tiene {asignados} usuario(s) activo(s): muévelos o desactívalos primero.");
        }
        s.Activo = dto.Activo;
        await _bitacora.AgregarAsync(tenantId, dto.Activo ? "SUCURSAL_ACTIVADA" : "SUCURSAL_DESACTIVADA", "Sucursal", sucursalId,
            $"Sucursal {s.Nombre} {(dto.Activo ? "activada" : "desactivada")}.");
        await _db.SaveChangesAsync();
        return await DetalleAsync(tenantId);
    }

    public async Task<EmpresaDetalleDto> MarcarPrincipalAsync(int tenantId, int sucursalId)
    {
        var s = await SucursalAsync(tenantId, sucursalId);
        if (!s.Activo) throw new ReglaNegocioException("Una sucursal desactivada no puede ser la principal.");
        if (s.EsPrincipal) return await DetalleAsync(tenantId);
        await _db.Sucursales.IgnoreQueryFilters().Where(x => x.TenantId == tenantId && x.EsPrincipal)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.EsPrincipal, false));
        s.EsPrincipal = true;
        await _bitacora.AgregarAsync(tenantId, "SUCURSAL_PRINCIPAL", "Sucursal", sucursalId, $"{s.Nombre} es ahora la sucursal principal.");
        await _db.SaveChangesAsync();
        return await DetalleAsync(tenantId);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // USUARIOS DE LA EMPRESA
    // ═════════════════════════════════════════════════════════════════════════
    private async Task<Usuario> UsuarioAsync(int tenantId, int usuarioId) =>
        await _db.Usuarios.IgnoreQueryFilters().Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.UsuarioId == usuarioId)
        ?? throw ReglaNegocioException.NoEncontrado("Usuario");

    private async Task ValidarEmailLibreAsync(string email, int? excluir)
    {
        if (await _db.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Email.ToLower() == email && u.UsuarioId != excluir))
            throw new ReglaNegocioException($"El correo {email} ya está registrado en ValexaFood.");
    }

    private async Task<int> ValidarSucursalUsuarioAsync(int tenantId, int? sucursalId)
    {
        if (sucursalId == null) throw new ReglaNegocioException("Elige la sucursal del usuario.");
        var s = await SucursalAsync(tenantId, sucursalId.Value);
        if (!s.Activo) throw new ReglaNegocioException("La sucursal elegida está desactivada.");
        return s.SucursalId;
    }

    public async Task<PasswordTemporalDto?> CrearUsuarioAsync(int tenantId, UsuarioGuardarDto dto)
    {
        await TenantAsync(tenantId);
        if (dto.RolId is < RolIds.Administrador or > RolIds.Delivery) throw new ReglaNegocioException("Rol no válido.");
        if (dto.RolId == RolIds.Administrador && await _db.Usuarios.IgnoreQueryFilters()
                .AnyAsync(u => u.TenantId == tenantId && u.RolId == RolIds.Administrador && u.Activo))
            throw new ReglaNegocioException("La empresa ya tiene un Administrador activo: el Administrador es único.");

        var nombre = Texto.Requerido(dto.Nombre, "Nombre", 120, 3);
        var email = Documento.NormalizarEmail(dto.Email);
        await ValidarEmailLibreAsync(email, null);
        var sucursal = await ValidarSucursalUsuarioAsync(tenantId, dto.SucursalId);

        string? temporal = null;
        string password;
        if (string.IsNullOrEmpty(dto.Password)) password = temporal = Clave.GenerarTemporal();
        else { Clave.ValidarFortaleza(dto.Password); password = dto.Password; }

        var u = new Usuario
        {
            TenantId = tenantId, SucursalId = sucursal, RolId = dto.RolId, Nombre = nombre, Email = email,
            Dni = Documento.NormalizarDni(dto.Dni), Telefono = Documento.NormalizarTelefono(dto.Telefono),
            PasswordHash = Clave.Hash(password), Activo = true, DebeCambiarPassword = true, CreatedAt = DateTime.UtcNow
        };
        _db.Usuarios.Add(u);
        var rol = await _db.Roles.Where(r => r.RolId == dto.RolId).Select(r => r.Nombre).FirstAsync();
        await _bitacora.AgregarAsync(tenantId, "USUARIO_CREADO", "Usuario", null, $"Nuevo usuario {nombre} ({email}) como {rol}.");
        await _db.SaveChangesAsync(); // el trigger de la base aplica el cupo del plan
        return temporal == null ? null : new PasswordTemporalDto(temporal,
            $"Entrégale a {nombre} esta contraseña temporal: la cambiará en su primer ingreso.");
    }

    public async Task EditarUsuarioAsync(int tenantId, int usuarioId, UsuarioGuardarDto dto)
    {
        var u = await UsuarioAsync(tenantId, usuarioId);
        var nombre = Texto.Requerido(dto.Nombre, "Nombre", 120, 3);
        var email = Documento.NormalizarEmail(dto.Email);
        await ValidarEmailLibreAsync(email, usuarioId);
        var sucursal = await ValidarSucursalUsuarioAsync(tenantId, dto.SucursalId);

        if (dto.RolId != u.RolId)
        {
            if (u.RolId == RolIds.Administrador)
                throw new ReglaNegocioException("El Administrador no cambia de rol: crea otro Administrador tras desactivar a este.");
            if (dto.RolId is < RolIds.Cajero or > RolIds.Delivery)
                throw new ReglaNegocioException("Solo se puede cambiar entre roles operativos (Cajero, Cocinero, Mesero, Delivery).");
        }

        var cambios = new List<string>();
        if (u.Nombre != nombre) cambios.Add($"nombre '{u.Nombre}' → '{nombre}'");
        if (u.Email != email) cambios.Add($"correo {u.Email} → {email}");
        if (u.RolId != dto.RolId) cambios.Add($"rol {u.Rol?.Nombre} → {await _db.Roles.Where(r => r.RolId == dto.RolId).Select(r => r.Nombre).FirstAsync()}");
        if (u.SucursalId != sucursal) cambios.Add("sucursal cambiada");

        var cierraSesion = u.RolId != dto.RolId || u.Email != email;
        u.Nombre = nombre;
        u.Email = email;
        u.Dni = Documento.NormalizarDni(dto.Dni);
        u.Telefono = Documento.NormalizarTelefono(dto.Telefono);
        u.RolId = dto.RolId;
        u.SucursalId = sucursal;
        // Un cambio de rol o de correo invalida los permisos del token vigente.
        if (cierraSesion) await _auth.CerrarSesionesAsync(u.UsuarioId, u);
        if (cambios.Count > 0)
            await _bitacora.AgregarAsync(tenantId, "USUARIO_EDITADO", "Usuario", usuarioId, $"{nombre}: {string.Join("; ", cambios)}.");
        await _db.SaveChangesAsync();
    }

    public async Task EstadoUsuarioAsync(int tenantId, int usuarioId, EstadoDto dto)
    {
        var u = await UsuarioAsync(tenantId, usuarioId);
        if (u.Activo == dto.Activo) throw new ReglaNegocioException(dto.Activo ? "El usuario ya está activo." : "El usuario ya está desactivado.");
        if (!dto.Activo && u.RolId == RolIds.Administrador)
            throw new ReglaNegocioException("El Administrador no se desactiva: la empresa quedaría sin nadie que la gestione. Para reemplazarlo, edita sus datos.");
        if (dto.Activo && u.RolId == RolIds.Administrador && await _db.Usuarios.IgnoreQueryFilters()
                .AnyAsync(x => x.TenantId == tenantId && x.RolId == RolIds.Administrador && x.Activo))
            throw new ReglaNegocioException("La empresa ya tiene un Administrador activo.");
        if (dto.Activo && u.SucursalId.HasValue &&
            !await _db.Sucursales.IgnoreQueryFilters().AnyAsync(s => s.SucursalId == u.SucursalId && s.Activo))
            throw new ReglaNegocioException("Su sucursal está desactivada: asígnale otra antes de reactivarlo.");

        u.Activo = dto.Activo;
        if (!dto.Activo) await _auth.CerrarSesionesAsync(u.UsuarioId, u);
        await _bitacora.AgregarAsync(tenantId, dto.Activo ? "USUARIO_ACTIVADO" : "USUARIO_DESACTIVADO", "Usuario", usuarioId,
            $"{u.Nombre} ({u.Rol?.Nombre}) {(dto.Activo ? "reactivado" : "desactivado")}.");
        await _db.SaveChangesAsync(); // reactivar vuelve a pasar por el cupo del plan
        _cache.Usuario(usuarioId);
    }

    public async Task<PasswordTemporalDto> ResetPasswordAsync(int tenantId, int usuarioId)
    {
        var u = await UsuarioAsync(tenantId, usuarioId);
        var temporal = Clave.GenerarTemporal();
        u.PasswordHash = Clave.Hash(temporal);
        u.DebeCambiarPassword = true;
        u.IntentosFallidos = 0;
        u.BloqueadoHasta = null;
        await _auth.CerrarSesionesAsync(u.UsuarioId, u);
        await _bitacora.AgregarAsync(tenantId, "USUARIO_PASSWORD_RESETEADA", "Usuario", usuarioId,
            $"Se generó una contraseña temporal para {u.Nombre}; sus sesiones abiertas se cerraron.");
        await _db.SaveChangesAsync();
        return new PasswordTemporalDto(temporal, $"Entrégale a {u.Nombre} esta contraseña temporal: la cambiará al ingresar. No se volverá a mostrar.");
    }

    public async Task DesbloquearAsync(int tenantId, int usuarioId)
    {
        var u = await UsuarioAsync(tenantId, usuarioId);
        if (!(u.BloqueadoHasta > DateTime.UtcNow)) throw new ReglaNegocioException("El usuario no está bloqueado.");
        u.BloqueadoHasta = null;
        u.IntentosFallidos = 0;
        await _bitacora.AgregarAsync(tenantId, "USUARIO_DESBLOQUEADO", "Usuario", usuarioId, $"{u.Nombre} desbloqueado.");
        await _db.SaveChangesAsync();
    }

    public async Task CerrarSesionesUsuarioAsync(int tenantId, int usuarioId)
    {
        var u = await UsuarioAsync(tenantId, usuarioId);
        await _auth.CerrarSesionesAsync(u.UsuarioId, u);
        await _bitacora.AgregarAsync(tenantId, "USUARIO_SESIONES_CERRADAS", "Usuario", usuarioId, $"Se cerraron las sesiones de {u.Nombre}.");
        await _db.SaveChangesAsync();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // VALIDACIONES
    // ═════════════════════════════════════════════════════════════════════════
    private static EmpresaEditarDto ValidarDatos(EmpresaEditarDto dto)
    {
        var tipo = (dto.TipoServicio ?? "RESTAURANTE").Trim().ToUpperInvariant();
        if (!TiposServicio.Todos.Contains(tipo)) throw new ReglaNegocioException("Tipo de negocio no válido.");
        var email = Texto.Limpio(dto.Email) == null ? null : Documento.NormalizarEmail(dto.Email, "Correo de la empresa");
        return new EmpresaEditarDto(
            Texto.Requerido(dto.NombreComercial, "Nombre comercial", 120, 2),
            Texto.Opcional(dto.RazonSocial, "Razón social", 200),
            Documento.NormalizarRuc(dto.Ruc),
            Texto.Opcional(dto.Direccion, "Dirección", 250),
            Documento.NormalizarTelefono(dto.Telefono),
            email,
            Texto.Opcional(dto.ContactoNombre, "Contacto", 120),
            tipo);
    }

    private async Task ValidarUnicosAsync(int? id, string nombre, string? ruc)
    {
        if (await _db.Tenants.AnyAsync(t => t.TenantId != id && t.NombreComercial.ToLower() == nombre.ToLower()))
            throw new ReglaNegocioException($"Ya existe una empresa llamada {nombre}.");
        if (ruc != null && await _db.Tenants.AnyAsync(t => t.TenantId != id && t.Ruc == ruc))
            throw new ReglaNegocioException($"El RUC {ruc} ya pertenece a otra empresa.");
    }
}
