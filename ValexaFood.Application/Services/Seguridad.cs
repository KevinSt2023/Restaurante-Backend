using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ValexaFood.Application.Common;
using ValexaFood.Domain.Entities;
using ValexaFood.Infrastructure.Context;

namespace ValexaFood.Application.Services;

// ─────────────────────────────────────────────────────────────────────────────
// JWT
// ─────────────────────────────────────────────────────────────────────────────
public class JwtService
{
    public const int MinutosAcceso = 30;
    private readonly string _key, _issuer, _audience;

    public JwtService(IConfiguration config)
    {
        _key = Environment.GetEnvironmentVariable("JWT_KEY") ?? config["Jwt:Key"]
            ?? throw new InvalidOperationException("Falta Jwt:Key.");
        _issuer = config["Jwt:Issuer"] ?? "ValexaFoodAPI";
        _audience = config["Jwt:Audience"] ?? "ValexaFoodUsers";
    }

    public string Generar(Usuario u)
    {
        var claims = new List<Claim>
        {
            new("usuario_id", u.UsuarioId.ToString()),
            new("rol_id", u.RolId.ToString()),
            new("sv", u.SesionVersion.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };
        if (u.TenantId.HasValue) claims.Add(new("tenant_id", u.TenantId.Value.ToString()));
        if (u.SucursalId.HasValue) claims.Add(new("sucursal_id", u.SucursalId.Value.ToString()));

        var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(_issuer, _audience, claims,
            expires: DateTime.UtcNow.AddMinutes(MinutosAcceso), signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public static class PlanVigencia
{
    public static bool EstaVencido(DateOnly? vencimiento) => vencimiento.HasValue && vencimiento.Value < DiaLima.Hoy();
}

// ─────────────────────────────────────────────────────────────────────────────
// Validación de sesión en cada petición autenticada.
// Un token firmado no basta: el usuario tiene que seguir activo, con la misma
// versión de sesión (cerrar sesiones la sube), y su empresa activa y al día.
// Se cachea 30 s; las acciones del SuperAdmin invalidan la caché al instante.
// ─────────────────────────────────────────────────────────────────────────────
public record ResultadoSesion(bool Valida, string? Motivo, bool PlanVencido);

public class ValidadorSesion
{
    private static readonly TimeSpan Duracion = TimeSpan.FromSeconds(30);
    private readonly ValexaFoodContext _db;
    private readonly IMemoryCache _cache;

    public ValidadorSesion(ValexaFoodContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    private record EstadoUsuario(bool Activo, int SesionVersion);
    private record EstadoEmpresa(bool Existe, bool Activa, bool Vencida);

    public static string ClaveUsuario(int id) => $"ses_u_{id}";
    public static string ClaveEmpresa(int id) => $"ses_t_{id}";

    public async Task<ResultadoSesion> ValidarAsync(ClaimsPrincipal p)
    {
        if (!int.TryParse(p.FindFirst("usuario_id")?.Value, out var usuarioId)
            || !int.TryParse(p.FindFirst("sv")?.Value, out var sv)
            || !int.TryParse(p.FindFirst("rol_id")?.Value, out var rolId))
            return new(false, "Token incompleto", false);

        var usuario = await _cache.GetOrCreateAsync(ClaveUsuario(usuarioId), async e =>
        {
            e.AbsoluteExpirationRelativeToNow = Duracion;
            var u = await _db.Usuarios.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.UsuarioId == usuarioId)
                .Select(x => new { x.Activo, x.SesionVersion }).FirstOrDefaultAsync();
            return u == null ? new EstadoUsuario(false, -1) : new EstadoUsuario(u.Activo, u.SesionVersion);
        });
        if (usuario == null || !usuario.Activo) return new(false, "Usuario inactivo", false);
        if (usuario.SesionVersion != sv) return new(false, "Sesión cerrada", false);

        if (rolId == RolIds.SuperAdmin) return new(true, null, false);

        if (!int.TryParse(p.FindFirst("tenant_id")?.Value, out var tenantId)) return new(false, "Token sin empresa", false);
        var empresa = await EstadoEmpresaAsync(tenantId);
        if (!empresa.Existe || !empresa.Activa) return new(false, "Empresa suspendida", false);
        if (empresa.Vencida) return new(false, "Plan vencido", true);
        return new(true, null, false);
    }

    private async Task<EstadoEmpresa> EstadoEmpresaAsync(int tenantId) =>
        (await _cache.GetOrCreateAsync(ClaveEmpresa(tenantId), async e =>
        {
            e.AbsoluteExpirationRelativeToNow = Duracion;
            var t = await _db.Tenants.AsNoTracking().Where(x => x.TenantId == tenantId)
                .Select(x => new { x.Activo, x.PlanId, x.PlanFechaVencimiento }).FirstOrDefaultAsync();
            return t == null
                ? new EstadoEmpresa(false, false, true)
                : new EstadoEmpresa(true, t.Activo, t.PlanId == null || PlanVigencia.EstaVencido(t.PlanFechaVencimiento));
        }))!;
}

public class CacheSesiones
{
    private readonly IMemoryCache _cache;
    public CacheSesiones(IMemoryCache cache) => _cache = cache;
    public void Usuario(int id) => _cache.Remove(ValidadorSesion.ClaveUsuario(id));
    public void Empresa(int id)
    {
        _cache.Remove(ValidadorSesion.ClaveEmpresa(id));
        // Cambió el plan o su estado: los permisos efectivos se recalculan ya.
        for (var rol = RolIds.Administrador; rol <= RolIds.Delivery; rol++) _cache.Remove(PermisosEfectivos.Clave(id, rol));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Permisos efectivos = permisos del rol (+ los de los roles que el plan le deja
// acumular al Administrador), recortados por las funciones del plan.
// ─────────────────────────────────────────────────────────────────────────────
public class PermisosEfectivos
{
    private readonly ValexaFoodContext _db;
    private readonly IMemoryCache _cache;
    public PermisosEfectivos(ValexaFoodContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public record Resultado(List<string> Permisos, List<string> Funciones, List<int> RolesQueOpera);

    public static string Clave(int tenantId, int rolId) => $"perm_{tenantId}_{rolId}";

    // Para cada petición: mismo resultado para toda la empresa y rol, 30 s en caché.
    public async Task<Resultado> DeSesionAsync(int rolId, int? tenantId)
    {
        if (tenantId == null) return await CalcularAsync(rolId, null);
        return (await _cache.GetOrCreateAsync(Clave(tenantId.Value, rolId), async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
            return await CalcularAsync(rolId, tenantId);
        }))!;
    }

    public async Task<Resultado> CalcularAsync(int rolId, int? tenantId)
    {
        if (rolId == RolIds.SuperAdmin) return new(["SAAS"], [], [RolIds.SuperAdmin]);

        var plan = tenantId == null ? null : await _db.Tenants.AsNoTracking()
            .Where(t => t.TenantId == tenantId).Select(t => t.Plan).FirstOrDefaultAsync();
        var funciones = plan == null ? [] : await _db.PlanFunciones.AsNoTracking()
            .Where(f => f.PlanId == plan.PlanId).Select(f => f.FuncionCodigo).ToListAsync();

        var roles = new List<int> { rolId };
        if (rolId == RolIds.Administrador && plan != null) roles.AddRange(plan.AdminAcumula);

        var permisos = await (from rp in _db.RolPermisos
                              join p in _db.Permisos on rp.PermisoId equals p.PermisoId
                              where roles.Contains(rp.RolId)
                                 && (p.FuncionRequerida == null || funciones.Contains(p.FuncionRequerida))
                              select p.Codigo).Distinct().ToListAsync();

        return new(permisos.OrderBy(x => x).ToList(), funciones, roles);
    }
}
