using Microsoft.AspNetCore.Http;

namespace ValexaFood.Infrastructure.Sesion;

public interface ISesionActual
{
    int? UsuarioId { get; }
    int? TenantId { get; }
    int? SucursalId { get; }
    int RolId { get; }
    bool EsSuperAdmin { get; }

    // Valor que usa el filtro global por empresa. Sin sesión o con SuperAdmin es -1:
    // el filtro no deja pasar NADA (falla cerrado). El SuperAdmin ve otras empresas
    // solo con IgnoreQueryFilters() explícito en los servicios que lo necesitan.
    int FiltroTenantId { get; }
}

public class SesionActual : ISesionActual
{
    private readonly IHttpContextAccessor _accessor;

    public SesionActual(IHttpContextAccessor accessor) => _accessor = accessor;

    private string? Claim(string tipo) => _accessor.HttpContext?.User?.FindFirst(tipo)?.Value;

    public int? UsuarioId => int.TryParse(Claim("usuario_id"), out var v) ? v : null;
    public int? TenantId => int.TryParse(Claim("tenant_id"), out var v) ? v : null;
    // La sucursal enviada por el cliente nunca se considera autorizada hasta que
    // ContextoSucursalMiddleware valida su pertenencia al tenant y al usuario.
    public int? SucursalId
    {
        get
        {
            var header = _accessor.HttpContext?.Request.Headers["X-Sucursal-Id"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(header)) return int.TryParse(header, out var activa) ? activa : null;
            return int.TryParse(Claim("sucursal_id"), out var predeterminada) ? predeterminada : null;
        }
    }
    public int RolId => int.TryParse(Claim("rol_id"), out var v) ? v : -1;
    public bool EsSuperAdmin => RolId == 0 && UsuarioId.HasValue;
    public int FiltroTenantId => TenantId is > 0 ? TenantId.Value : -1;
}
