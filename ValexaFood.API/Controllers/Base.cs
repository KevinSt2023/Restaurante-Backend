using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ValexaFood.API.Controllers;

[ApiController]
[Authorize]
public abstract class BaseController : ControllerBase
{
    protected int UsuarioId => int.Parse(User.FindFirst("usuario_id")!.Value);
    protected int RolId => int.Parse(User.FindFirst("rol_id")!.Value);
}

// Control de acceso por rol en la API (no solo en el menú del frontend).
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class SoloRolesAttribute : Attribute, IAuthorizationFilter
{
    private readonly int[] _roles;
    public SoloRolesAttribute(params int[] roles) => _roles = roles;

    public void OnAuthorization(AuthorizationFilterContext ctx)
    {
        if (ctx.HttpContext.User.Identity?.IsAuthenticated != true) return; // lo resuelve [Authorize] con 401
        var rol = ctx.HttpContext.User.FindFirst("rol_id")?.Value;
        if (rol == null || !_roles.Contains(int.Parse(rol)))
            ctx.Result = new ObjectResult(new { mensaje = "No tienes permiso para esta acción." }) { StatusCode = 403 };
    }
}
