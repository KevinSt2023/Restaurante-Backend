using Microsoft.EntityFrameworkCore;
using ValexaFood.Application.Common;
using ValexaFood.Domain.Entities;
using ValexaFood.Infrastructure.Context;

namespace ValexaFood.Application.Services;

public record CupoDto(int RolId, string RolNombre, int Maximo, bool PorSucursal);
public record PlanDto(int PlanId, string Nombre, string Descripcion, decimal PrecioMensual, decimal? PrecioAnual,
    decimal PrecioSucursalAdicional, decimal? PrecioSucursalAdicionalAnual,
    int MaxSucursales, int MaxUsuarios, int[] AdminAcumula, List<CupoDto> Cupos, List<string> Funciones,
    bool Destacado, bool ACotizar, int Orden, bool Activo, int Empresas, int EmpresasActivas);
/// <param name="PorSucursal">
/// null = deja el valor que ese rol ya tenía en este plan. Un cliente que no
/// conoce el campo no puede apagar sin querer un "2 meseros por local", que es
/// una promesa comercial y no se cambia por omisión.
/// </param>
public record CupoGuardarDto(int RolId, int Maximo, bool? PorSucursal = null);
public record PlanGuardarDto(string? Nombre, string? Descripcion, decimal PrecioMensual, decimal? PrecioAnual,
    int MaxSucursales, int MaxUsuarios, int[]? AdminAcumula, List<CupoGuardarDto>? Cupos, List<string>? Funciones,
    bool Destacado, bool ACotizar, int Orden, bool Activo, bool ConfirmarExcedidas = false,
    // null = conserva lo que el plan ya cobraba por local. Misma regla que
    // CupoGuardarDto.PorSucursal: un precio de lista no se apaga por omisión.
    decimal? PrecioSucursalAdicional = null, decimal? PrecioSucursalAdicionalAnual = null,
    bool QuitarPrecioSucursalAnual = false);
public record EmpresaExcedidaDto(int TenantId, string Nombre, List<string> Problemas);

// ─────────────────────────────────────────────────────────────────────────────
// EL PLAN MANDA. Uso real de cada empresa y qué le sobra para un plan dado.
// El candado final está en la base (fn_limite_plan_*); esto da el mensaje claro
// antes de llegar ahí y sirve para comparar planes.
// ─────────────────────────────────────────────────────────────────────────────
/// <param name="UsuariosPorRol">Activos por rol en toda la empresa.</param>
/// <param name="PicoPorRol">
/// Cuántos hay de ese rol en el local que más tiene. Es lo que un cupo "por
/// local" debe mirar: con 2 meseros por local, un local con 3 ya está excedido
/// aunque el otro esté vacío.
/// </param>
public record UsoEmpresa(int TenantId, int Sucursales, Dictionary<int, int> UsuariosPorRol, bool TieneAdmin,
    Dictionary<int, int>? PicoPorRol = null, Dictionary<(int Rol, int Sucursal), int>? PorRolYLocal = null)
{
    public int UsuariosOperativos => UsuariosPorRol.Values.Sum();

    /// <summary>Lo que hay de ese rol en el local que más tiene: el que topa.</summary>
    public int PicoDe(int rolId) => PicoPorRol?.GetValueOrDefault(rolId) ?? UsuariosPorRol.GetValueOrDefault(rolId);

    /// <summary>
    /// Lo que hay de ese rol en un local concreto, o el pico si no se pide uno.
    /// El dueño parado en una sede necesita el número de ESA sede; el SuperAdmin,
    /// que mira la empresa entera, necesita el pico.
    /// </summary>
    public int EnLocal(int rolId, int? sucursalId) => sucursalId is int s
        ? PorRolYLocal?.GetValueOrDefault((rolId, s)) ?? 0
        : PicoDe(rolId);
}

public class LimitePlan
{
    private readonly ValexaFoodContext _db;
    public LimitePlan(ValexaFoodContext db) => _db = db;

    public async Task<Dictionary<int, UsoEmpresa>> UsoAsync(IEnumerable<int>? tenantIds = null)
    {
        var ids = tenantIds?.ToList();
        // Se agrupa por local para poder responder las dos preguntas: cuántos hay
        // en la empresa y cuántos hay en el local que más tiene.
        var porLocal = await _db.Usuarios.IgnoreQueryFilters().AsNoTracking()
            .Where(u => u.Activo && u.TenantId != null && (ids == null || ids.Contains(u.TenantId!.Value)))
            .GroupBy(u => new { u.TenantId, u.RolId, u.SucursalId })
            .Select(g => new { g.Key.TenantId, g.Key.RolId, g.Key.SucursalId, N = g.Count() }).ToListAsync();
        var usuarios = porLocal.GroupBy(x => new { x.TenantId, x.RolId })
            .Select(g => new { g.Key.TenantId, g.Key.RolId, N = g.Sum(x => x.N) }).ToList();
        var sedes = await _db.Sucursales.IgnoreQueryFilters().AsNoTracking()
            .Where(s => s.Activo && (ids == null || ids.Contains(s.TenantId)))
            .GroupBy(s => s.TenantId).Select(g => new { TenantId = g.Key, N = g.Count() }).ToListAsync();

        var todos = ids ?? usuarios.Select(u => u.TenantId!.Value).Concat(sedes.Select(s => s.TenantId)).Distinct().ToList();
        return todos.ToDictionary(id => id, id => new UsoEmpresa(id,
            sedes.FirstOrDefault(s => s.TenantId == id)?.N ?? 0,
            usuarios.Where(u => u.TenantId == id && RolIds.EsOperativo(u.RolId)).ToDictionary(u => u.RolId, u => u.N),
            usuarios.Any(u => u.TenantId == id && u.RolId == RolIds.Administrador),
            porLocal.Where(x => x.TenantId == id && RolIds.EsOperativo(x.RolId))
                .GroupBy(x => x.RolId).ToDictionary(g => g.Key, g => g.Max(x => x.N)),
            porLocal.Where(x => x.TenantId == id && RolIds.EsOperativo(x.RolId) && x.SucursalId != null)
                .ToDictionary(x => (x.RolId, x.SucursalId!.Value), x => x.N)));
    }

    /// <param name="precioSucursalAdicional">
    /// Si es mayor que cero el plan VENDE locales: entonces maxSucursales son los
    /// que incluye el precio base, no un tope, y tener más no es un problema sino
    /// un cargo. Ver scripts/26.
    /// </param>
    public static List<string> Problemas(UsoEmpresa uso, int maxSucursales, int maxUsuarios,
        IEnumerable<(int RolId, int Maximo, bool PorSucursal)> cupos, IReadOnlyDictionary<int, string> nombresRol,
        decimal precioSucursalAdicional = 0)
    {
        var p = new List<string>();
        var cuposDic = cupos.ToDictionary(c => c.RolId, c => (c.Maximo, c.PorSucursal));
        if (maxSucursales > 0 && precioSucursalAdicional <= 0 && uso.Sucursales > maxSucursales)
            p.Add($"tiene {uso.Sucursales} sucursales activas y el plan permite {maxSucursales}");
        if (maxUsuarios > 0 && uso.UsuariosOperativos > maxUsuarios)
            p.Add($"tiene {uso.UsuariosOperativos} usuarios (sin contar al Administrador) y el plan permite {maxUsuarios}");
        foreach (var (rol, n) in uso.UsuariosPorRol.Where(x => x.Value > 0))
        {
            var nombre = nombresRol.GetValueOrDefault(rol, $"rol {rol}");
            if (!cuposDic.TryGetValue(rol, out var cupo))
                p.Add($"tiene {n} {nombre}(s) y el plan no incluye ese rol");
            else if (cupo.Maximo == 0) { }
            else if (cupo.PorSucursal)
            {
                // El tope es por local: lo que importa es el local más cargado.
                var pico = uso.PicoDe(rol);
                if (pico > cupo.Maximo)
                    p.Add($"tiene {pico} {nombre}(s) en un mismo local y el plan permite {cupo.Maximo} por local");
            }
            else if (n > cupo.Maximo) p.Add($"tiene {n} {nombre}(s) y el plan permite {cupo.Maximo}");
        }
        return p;
    }

    public async Task<IReadOnlyDictionary<int, string>> NombresRolAsync() =>
        await _db.Roles.AsNoTracking().ToDictionaryAsync(r => r.RolId, r => r.Nombre);
}

public class PlanService
{
    private readonly ValexaFoodContext _db;
    private readonly LimitePlan _limite;
    private readonly BitacoraService _bitacora;
    private readonly CacheSesiones _cache;

    public PlanService(ValexaFoodContext db, LimitePlan limite, BitacoraService bitacora, CacheSesiones cache)
    {
        _db = db;
        _limite = limite;
        _bitacora = bitacora;
        _cache = cache;
    }

    public async Task<List<PlanDto>> ListarAsync(bool soloActivos = false)
    {
        var roles = await _limite.NombresRolAsync();
        var planes = await _db.Planes.AsNoTracking().Include(p => p.Cupos).Include(p => p.Funciones)
            .Where(p => !soloActivos || p.Activo).OrderBy(p => p.Orden).ThenBy(p => p.PlanId).ToListAsync();
        var conteo = await _db.Tenants.AsNoTracking().Where(t => t.PlanId != null)
            .GroupBy(t => t.PlanId).Select(g => new { PlanId = g.Key!.Value, N = g.Count(), Activas = g.Count(t => t.Activo) })
            .ToListAsync();
        return planes.Select(p =>
        {
            var c = conteo.FirstOrDefault(x => x.PlanId == p.PlanId);
            return Dto(p, roles, c?.N ?? 0, c?.Activas ?? 0);
        }).ToList();
    }

    public async Task<PlanDto> ObtenerAsync(int id) =>
        (await ListarAsync()).FirstOrDefault(p => p.PlanId == id) ?? throw ReglaNegocioException.NoEncontrado("Plan");

    public async Task<PlanDto> CrearAsync(PlanGuardarDto dto)
    {
        var plan = new Plan { CreatedAt = DateTime.UtcNow };
        await AplicarAsync(plan, dto, nuevo: true);
        return await ObtenerAsync(plan.PlanId);
    }

    public async Task<PlanDto> ActualizarAsync(int id, PlanGuardarDto dto)
    {
        var plan = await _db.Planes.Include(p => p.Cupos).Include(p => p.Funciones).FirstOrDefaultAsync(p => p.PlanId == id)
            ?? throw ReglaNegocioException.NoEncontrado("Plan");
        await AplicarAsync(plan, dto, nuevo: false);
        return await ObtenerAsync(id);
    }

    public async Task EliminarAsync(int id)
    {
        var plan = await _db.Planes.FirstOrDefaultAsync(p => p.PlanId == id) ?? throw ReglaNegocioException.NoEncontrado("Plan");
        if (await _db.Tenants.AnyAsync(t => t.PlanId == id))
            throw new ReglaNegocioException("El plan tiene empresas asignadas: desactívalo en lugar de eliminarlo.");
        if (await _db.SuscripcionMovimientos.AnyAsync(m => m.PlanId == id))
            throw new ReglaNegocioException("El plan tiene cobros registrados: desactívalo en lugar de eliminarlo.");
        _db.Planes.Remove(plan);
        await _bitacora.AgregarAsync(null, "PLAN_ELIMINADO", "Plan", id, $"Se eliminó el plan {plan.Nombre}.");
        await _db.SaveChangesAsync();
    }

    private async Task AplicarAsync(Plan plan, PlanGuardarDto dto, bool nuevo)
    {
        var nombre = Texto.Requerido(dto.Nombre, "Nombre del plan", 60, 3);
        var descripcion = Texto.Opcional(dto.Descripcion, "Descripción", 400) ?? "";
        if (dto.PrecioMensual < 0 || dto.PrecioMensual > 99999) throw new ReglaNegocioException("Precio mensual fuera de rango.");
        if (dto.PrecioAnual is < 0 or > 999999) throw new ReglaNegocioException("Precio anual fuera de rango.");
        if (Math.Round(dto.PrecioMensual, 2) != dto.PrecioMensual) throw new ReglaNegocioException("El precio admite 2 decimales.");
        if (dto.MaxSucursales is < 0 or > 999) throw new ReglaNegocioException("Máximo de sucursales fuera de rango (0 = ilimitado).");
        if (dto.PrecioSucursalAdicional is < 0 or > 99999) throw new ReglaNegocioException("El precio por local adicional está fuera de rango.");
        if (dto.PrecioSucursalAdicionalAnual is < 0 or > 999999) throw new ReglaNegocioException("El precio anual por local adicional está fuera de rango.");
        // Lo que el DTO no mande se conserva; solo un 0 explícito deja de cobrar.
        var porLocal = dto.PrecioSucursalAdicional ?? plan.PrecioSucursalAdicional;
        var porLocalAnual = dto.QuitarPrecioSucursalAnual
            ? null
            : dto.PrecioSucursalAdicionalAnual ?? plan.PrecioSucursalAdicionalAnual;
        // Con locales ilimitados no hay "local adicional" que cobrar: el cargo nunca se aplicaría.
        if (dto.MaxSucursales == 0 && porLocal > 0)
            throw new ReglaNegocioException("Un plan con locales ilimitados no puede cobrar por local adicional: pon cuántos incluye el precio base.");
        if (dto.MaxUsuarios is < 0 or > 9999) throw new ReglaNegocioException("Máximo de usuarios fuera de rango (0 = ilimitado).");
        if (dto.Orden is < 0 or > 999) throw new ReglaNegocioException("Orden fuera de rango.");

        var cupos = (dto.Cupos ?? []).ToList();
        if (cupos.Any(c => !RolIds.EsOperativo(c.RolId)))
            throw new ReglaNegocioException("Los cupos solo aplican a los roles operativos: Gerente de local, Cajero, Cocinero, Mesero y Delivery.");
        if (cupos.GroupBy(c => c.RolId).Any(g => g.Count() > 1)) throw new ReglaNegocioException("Hay un rol repetido en los cupos.");
        if (cupos.Any(c => c.Maximo is < 0 or > 999)) throw new ReglaNegocioException("El cupo por rol debe estar entre 0 (sin tope) y 999.");

        var acumula = (dto.AdminAcumula ?? []).Distinct().OrderBy(x => x).ToArray();
        if (acumula.Any(r => !RolIds.EsOperativo(r)))
            throw new ReglaNegocioException("El Administrador solo puede acumular roles operativos.");

        var funcionesValidas = await _db.Funciones.Select(f => f.Codigo).ToListAsync();
        var funciones = (dto.Funciones ?? []).Distinct().ToList();
        var invalida = funciones.FirstOrDefault(f => !funcionesValidas.Contains(f));
        if (invalida != null) throw new ReglaNegocioException($"Función desconocida: {invalida}.");
        // FE es un gancho comercial de ValexaFood y está disponible en todos
        // los planes; cada empresa configura su propio emisor y credenciales.
        if (!funcionesValidas.Contains("FACTURACION_SUNAT"))
            throw new InvalidOperationException("Falta registrar la función FACTURACION_SUNAT en el catálogo.");
        if (!funciones.Contains("FACTURACION_SUNAT")) funciones.Add("FACTURACION_SUNAT");

        // Coherencia: un plan que se puede vender tiene que poder operar.
        var rolesDelPlan = cupos.Select(c => c.RolId).Concat(acumula).ToHashSet();
        // Quien cobra: el Cajero, el Gerente de local, o el Administrador si el plan
        // le deja acumular alguno de los dos.
        var cobra = rolesDelPlan.Contains(RolIds.Cajero) || rolesDelPlan.Contains(RolIds.GerenteLocal);
        if (!cobra)
            throw new ReglaNegocioException("Nadie podría cobrar con este plan: incluye el rol Cajero o Gerente de local, o deja que el Administrador cobre.");
        if (rolesDelPlan.Contains(RolIds.Delivery) && !funciones.Contains("DELIVERY"))
            throw new ReglaNegocioException("El rol Delivery necesita la función Delivery en el plan.");
        if (funciones.Contains("DELIVERY") && !rolesDelPlan.Contains(RolIds.Delivery) && !cobra)
            throw new ReglaNegocioException("La función Delivery necesita alguien que registre los envíos.");
        if (!funciones.Contains("PEDIDOS_LOCAL"))
            throw new ReglaNegocioException("Todo plan debe incluir Pedidos en local: es el corazón del sistema.");

        var nombreRepetido = await _db.Planes.AnyAsync(p => p.PlanId != plan.PlanId && p.Nombre.ToLower() == nombre.ToLower());
        if (nombreRepetido) throw new ReglaNegocioException($"Ya existe un plan llamado {nombre}.");

        // Los cupos se reescriben enteros. Lo que el DTO no mande en PorSucursal se
        // conserva: un cliente que no conoce el campo no puede apagar sin querer un
        // "2 meseros por local", que es promesa comercial, no un detalle técnico.
        // Y sin tope (0) no hay nada que contar por local.
        var porLocalAntes = plan.Cupos.ToDictionary(c => c.RolId, c => c.PorSucursal);
        var finales = cupos.Select(c => (
            c.RolId, c.Maximo,
            PorSucursal: c.Maximo > 0 && (c.PorSucursal ?? porLocalAntes.GetValueOrDefault(c.RolId)))).ToList();

        // Bajar límites por debajo de lo que ya usa una empresa: se avisa primero.
        if (!nuevo)
        {
            var empresas = await _db.Tenants.AsNoTracking().Where(t => t.PlanId == plan.PlanId && t.Activo)
                .Select(t => new { t.TenantId, t.NombreComercial }).ToListAsync();
            if (empresas.Count > 0)
            {
                var uso = await _limite.UsoAsync(empresas.Select(e => e.TenantId));
                var roles = await _limite.NombresRolAsync();
                var excedidas = empresas.Select(e => new EmpresaExcedidaDto(e.TenantId, e.NombreComercial,
                        LimitePlan.Problemas(uso[e.TenantId], dto.MaxSucursales, dto.MaxUsuarios,
                            finales, roles, porLocal)))
                    .Where(x => x.Problemas.Count > 0).ToList();
                if (excedidas.Count > 0 && !dto.ConfirmarExcedidas)
                    throw new ReglaNegocioException(
                        $"{excedidas.Count} empresa(s) ya usan más de lo que permitiría el plan. Si guardas, conservan lo que tienen pero no podrán sumar más.",
                        409, new { empresasExcedidas = excedidas });
            }
        }

        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync();

            // "Más elegido" es único: se quita al anterior y se guarda ESO primero,
            // si no el índice parcial rechaza el SaveChanges conjunto.
            if (dto.Destacado)
            {
                await _db.Planes.Where(p => p.Destacado && p.PlanId != plan.PlanId)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.Destacado, false));
            }

            plan.Nombre = nombre;
            plan.Descripcion = descripcion;
            plan.PrecioMensual = dto.PrecioMensual;
            plan.PrecioAnual = dto.PrecioAnual;
            plan.PrecioSucursalAdicional = porLocal;
            plan.PrecioSucursalAdicionalAnual = porLocalAnual;
            plan.MaxSucursales = dto.MaxSucursales;
            plan.MaxUsuarios = dto.MaxUsuarios;
            plan.AdminAcumula = acumula;
            plan.Destacado = dto.Destacado;
            plan.ACotizar = dto.ACotizar;
            plan.Orden = dto.Orden;
            plan.Activo = dto.Activo;

            if (nuevo) _db.Planes.Add(plan);
            else
            {
                _db.PlanRolCupos.RemoveRange(plan.Cupos);
                _db.PlanFunciones.RemoveRange(plan.Funciones);
            }
            await _db.SaveChangesAsync();

            _db.PlanRolCupos.AddRange(finales.Select(c => new PlanRolCupo
            {
                PlanId = plan.PlanId, RolId = c.RolId, Maximo = c.Maximo, PorSucursal = c.PorSucursal,
            }));
            _db.PlanFunciones.AddRange(funciones.Select(f => new PlanFuncion { PlanId = plan.PlanId, FuncionCodigo = f }));
            await _bitacora.AgregarAsync(null, nuevo ? "PLAN_CREADO" : "PLAN_EDITADO", "Plan", plan.PlanId,
                $"{(nuevo ? "Se creó" : "Se editó")} el plan {nombre}: S/ {Texto.Soles(dto.PrecioMensual)}/mes, " +
                $"{(dto.MaxSucursales == 0 ? "sucursales ilimitadas" : $"{dto.MaxSucursales} sucursal(es)")}, " +
                $"{(dto.MaxUsuarios == 0 ? "usuarios ilimitados" : $"{dto.MaxUsuarios} usuario(s)")}, {funciones.Count} funciones.");
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        });

        // Las funciones del plan cambian permisos de sus empresas en vivo.
        var afectadas = await _db.Tenants.Where(t => t.PlanId == plan.PlanId).Select(t => t.TenantId).ToListAsync();
        foreach (var t in afectadas) _cache.Empresa(t);
    }

    private static PlanDto Dto(Plan p, IReadOnlyDictionary<int, string> roles, int empresas, int activas) => new(
        p.PlanId, p.Nombre, p.Descripcion, p.PrecioMensual, p.PrecioAnual,
        p.PrecioSucursalAdicional, p.PrecioSucursalAdicionalAnual, p.MaxSucursales, p.MaxUsuarios, p.AdminAcumula,
        p.Cupos.OrderBy(c => c.RolId).Select(c => new CupoDto(c.RolId, roles.GetValueOrDefault(c.RolId, ""), c.Maximo, c.PorSucursal)).ToList(),
        p.Funciones.Select(f => f.FuncionCodigo).ToList(), p.Destacado, p.ACotizar, p.Orden, p.Activo, empresas, activas);
}
