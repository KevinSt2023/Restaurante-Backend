using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using ValexaFood.Application.Services;

namespace ValexaFood.API.Controllers;

// ─────────────────────────────────────────────────────────────────────────────
// La única puerta sin llave del sistema.
//
// No hereda de BaseController a propósito: ese lleva [Authorize] y aquí entra
// un comensal con el celular, sin cuenta y sin sucursal en la cabecera. Todo lo
// que necesita saber el servidor viaja en el código de la URL.
//
// Solo lectura. Desde aquí no se pide nada ni se toca ningún dato.
// ─────────────────────────────────────────────────────────────────────────────
[ApiController]
[AllowAnonymous]
[EnableRateLimiting("publico")]
[Route("api/publico")]
public class PublicoController : ControllerBase
{
    private readonly CartaPublicaService _svc;
    public PublicoController(CartaPublicaService svc) => _svc = svc;

    [HttpGet("carta/{codigo}")]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any)]
    public Task<CartaPublicaDto> Carta(string codigo) => _svc.VerAsync(codigo);
}
