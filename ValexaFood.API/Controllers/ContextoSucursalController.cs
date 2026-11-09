using Microsoft.AspNetCore.Mvc;
using ValexaFood.Application.Services;

namespace ValexaFood.API.Controllers;

[Route("api/operacion")]
public class ContextoSucursalController : BaseController
{
    private readonly ContextoSucursalService _svc;
    public ContextoSucursalController(ContextoSucursalService svc) => _svc = svc;

    [HttpGet("sucursales-disponibles")]
    public Task<List<SucursalDisponibleDto>> Disponibles() => _svc.DisponiblesAsync();

    [HttpPut("usuarios/{usuarioId:int}/sucursales"), RequierePermiso("USUARIOS_GESTIONAR")]
    public async Task<IActionResult> Asignar(int usuarioId, SucursalesAsignadasDto dto)
    {
        await _svc.AsignarAsync(usuarioId, dto);
        return NoContent();
    }
}
