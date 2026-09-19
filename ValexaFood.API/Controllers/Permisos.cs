using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ValexaFood.Application.Services;
using ValexaFood.Infrastructure.Sesion;

namespace ValexaFood.API.Controllers;

// Exige AL MENOS UNO de los permisos indicados, ya recortados por el plan de la
// empresa (rol × funciones del plan). Así "bajar" un plan apaga la función en la
// API, no solo en el menú. El SuperAdmin no opera restaurantes: 403.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class RequierePermisoAttribute : Attribute, IAsyncAuthorizationFilter
{
    public string[] Codigos { get; }
    public RequierePermisoAttribute(params string[] codigos) => Codigos = codigos;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext ctx)
    {
        if (ctx.HttpContext.User.Identity?.IsAuthenticated != true) return; // [Authorize] responde 401
        var sesion = ctx.HttpContext.RequestServices.GetRequiredService<ISesionActual>();
        if (sesion.TenantId == null)
        {
            ctx.Result = new ObjectResult(new { mensaje = "Esta sección es de los restaurantes." }) { StatusCode = 403 };
            return;
        }
        var permisos = ctx.HttpContext.RequestServices.GetRequiredService<PermisosEfectivos>();
        var r = await permisos.DeSesionAsync(sesion.RolId, sesion.TenantId);
        ctx.HttpContext.Items["permisos"] = r.Permisos;
        if (!Codigos.Any(r.Permisos.Contains))
            ctx.Result = new ObjectResult(new { mensaje = "Tu rol o tu plan no incluyen esta acción." }) { StatusCode = 403 };
    }
}
