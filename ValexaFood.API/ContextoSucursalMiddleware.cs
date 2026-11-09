using System.Security.Claims;
using ValexaFood.Application.Services;
using ValexaFood.Domain.Entities;

namespace ValexaFood.API;

// La sucursal del header es una selección, nunca una autorización. Toda llamada
// operativa la valida contra el usuario y tenant antes de ejecutar el controller.
public sealed class ContextoSucursalMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        if (context.User.Identity?.IsAuthenticated != true
            || path.StartsWithSegments("/api/auth")
            || path.StartsWithSegments("/api/health", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/operacion/sucursales-disponibles", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        if (int.TryParse(context.User.FindFirstValue("rol_id"), out var rolId) && rolId == RolIds.SuperAdmin)
        {
            await next(context);
            return;
        }

        if (!int.TryParse(context.User.FindFirstValue("tenant_id"), out var tenantId)
            || !int.TryParse(context.User.FindFirstValue("usuario_id"), out var usuarioId))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { mensaje = "La sesión no tiene empresa y usuario válidos." });
            return;
        }

        var header = context.Request.Headers["X-Sucursal-Id"].FirstOrDefault();
        int? sucursalId;
        if (context.Request.Headers.ContainsKey("X-Sucursal-Id"))
        {
            if (!int.TryParse(header, out var seleccionada) || seleccionada <= 0)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { mensaje = "La sucursal seleccionada no es válida." });
                return;
            }
            sucursalId = seleccionada;
        }
        else
        {
            sucursalId = int.TryParse(context.User.FindFirstValue("sucursal_id"), out var predeterminada)
                ? predeterminada : null;
        }

        if (sucursalId is null)
        {
            context.Response.StatusCode = StatusCodes.Status428PreconditionRequired;
            await context.Response.WriteAsJsonAsync(new { mensaje = "Selecciona una sucursal antes de operar." });
            return;
        }

        var acceso = context.RequestServices.GetRequiredService<ContextoSucursalService>();
        if (!await acceso.TieneAccesoAsync(tenantId, usuarioId, rolId, sucursalId.Value))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { mensaje = "No tienes acceso a esa sucursal o está inactiva." });
            return;
        }

        await next(context);
    }
}
