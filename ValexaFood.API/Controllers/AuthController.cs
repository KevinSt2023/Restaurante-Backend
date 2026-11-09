using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ValexaFood.Application.Services;

namespace ValexaFood.API.Controllers;

[Route("api/auth")]
public class AuthController : BaseController
{
    private readonly AuthService _auth;
    public AuthController(AuthService auth) => _auth = auth;

    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<SesionDto> Login(LoginDto dto) => await _auth.LoginAsync(dto);

    [HttpPost("refresh"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<SesionDto> Refresh(RefreshDto dto) => await _auth.RefreshAsync(dto);

    [HttpPost("logout"), AllowAnonymous]
    public async Task<IActionResult> Logout(RefreshDto dto)
    {
        await _auth.LogoutAsync(dto);
        return NoContent();
    }

    [HttpGet("perfil")]
    public async Task<PerfilDto> Perfil() => await _auth.PerfilAsync(UsuarioId);

    // Catálogo de permisos en castellano: lo usa la pantalla de Inicio para explicar
    // qué puede hacer cada rol sin mostrar códigos crudos.
    [HttpGet("catalogo-permisos")]
    public Task<List<PermisoCatalogoDto>> CatalogoPermisos() => _auth.CatalogoPermisosAsync();

    [HttpPost("cambiar-password"), EnableRateLimiting("auth")]
    public async Task<SesionDto> CambiarPassword(CambiarPasswordDto dto) => await _auth.CambiarPasswordAsync(UsuarioId, dto);
}
