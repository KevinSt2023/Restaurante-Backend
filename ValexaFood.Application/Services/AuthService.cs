using Microsoft.EntityFrameworkCore;
using ValexaFood.Application.Common;
using ValexaFood.Domain.Entities;
using ValexaFood.Infrastructure.Context;

namespace ValexaFood.Application.Services;

public record LoginDto(string? Email, string? Password);
public record RefreshDto(string? RefreshToken);
public record CambiarPasswordDto(string? Actual, string? Nueva);

public record UsuarioSesionDto(int UsuarioId, string Nombre, string Email, int RolId, string RolNombre,
    int? TenantId, string? Empresa, int? SucursalId, string? Sucursal, bool DebeCambiarPassword);
public record PlanSesionDto(int PlanId, string Nombre, DateOnly? Vencimiento, int? DiasParaVencer);
public record SesionDto(string Token, string RefreshToken, UsuarioSesionDto Usuario, List<string> Permisos,
    List<string> Funciones, List<int> RolesQueOpera, PlanSesionDto? Plan);
public record PerfilDto(UsuarioSesionDto Usuario, List<string> Permisos, List<string> Funciones,
    List<int> RolesQueOpera, PlanSesionDto? Plan);

public class AuthService
{
    private const int MaxIntentos = 5;
    private static readonly TimeSpan Bloqueo = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan VidaRefresh = TimeSpan.FromDays(7);
    // Hash de relleno: si el correo no existe se verifica igual, para que el tiempo
    // de respuesta no delate qué correos están registrados.
    private const string HashRelleno = "$2a$11$C6UzMDM.H6dfI/f/IKcEeO/oiKrKQiTL8sJ5xVQ3KH2FtQwZ8pNwK";

    private readonly ValexaFoodContext _db;
    private readonly JwtService _jwt;
    private readonly PermisosEfectivos _permisos;
    private readonly BitacoraService _bitacora;
    private readonly CacheSesiones _cache;

    public AuthService(ValexaFoodContext db, JwtService jwt, PermisosEfectivos permisos, BitacoraService bitacora, CacheSesiones cache)
    {
        _db = db;
        _jwt = jwt;
        _permisos = permisos;
        _bitacora = bitacora;
        _cache = cache;
    }

    private IQueryable<Usuario> UsuariosCompletos() => _db.Usuarios.IgnoreQueryFilters()
        .Include(u => u.Rol).Include(u => u.Sucursal).Include(u => u.Tenant).ThenInclude(t => t!.Plan);

    public async Task<SesionDto> LoginAsync(LoginDto dto)
    {
        var email = (dto.Email ?? "").Trim().ToLowerInvariant();
        var password = dto.Password ?? "";
        if (email.Length == 0 || password.Length == 0)
            throw new ReglaNegocioException("Ingresa tu correo y tu contraseña.");

        var usuario = await UsuariosCompletos().FirstOrDefaultAsync(u => u.Email.ToLower() == email);

        if (usuario == null)
        {
            BCrypt.Net.BCrypt.Verify(password, HashRelleno);
            await RegistrarAccesoAsync(null, email, false, "NO_EXISTE");
            throw new ReglaNegocioException("Correo o contraseña incorrectos.", 401);
        }

        if (usuario.BloqueadoHasta > DateTime.UtcNow)
        {
            await RegistrarAccesoAsync(usuario, email, false, "BLOQUEADO");
            var minutos = (int)Math.Ceiling((usuario.BloqueadoHasta.Value - DateTime.UtcNow).TotalMinutes);
            throw new ReglaNegocioException($"Usuario bloqueado por intentos fallidos. Intenta en {minutos} minuto(s).", 423);
        }

        if (!BCrypt.Net.BCrypt.Verify(password, usuario.PasswordHash))
        {
            usuario.IntentosFallidos++;
            if (usuario.IntentosFallidos >= MaxIntentos) usuario.BloqueadoHasta = DateTime.UtcNow.Add(Bloqueo);
            await RegistrarAccesoAsync(usuario, email, false, "CLAVE_INCORRECTA");
            throw new ReglaNegocioException("Correo o contraseña incorrectos.", 401);
        }

        if (!usuario.Activo)
        {
            await RegistrarAccesoAsync(usuario, email, false, "USUARIO_INACTIVO");
            throw new ReglaNegocioException("Tu usuario está desactivado. Consulta con el administrador.", 401);
        }

        if (usuario.RolId != RolIds.SuperAdmin)
        {
            var t = usuario.Tenant!;
            if (!t.Activo)
            {
                await RegistrarAccesoAsync(usuario, email, false, "EMPRESA_SUSPENDIDA");
                throw new ReglaNegocioException("El servicio de tu empresa está suspendido. Comunícate con VK Software Solutions.", 401);
            }
            if (t.PlanId == null || PlanVigencia.EstaVencido(t.PlanFechaVencimiento))
            {
                await RegistrarAccesoAsync(usuario, email, false, "PLAN_VENCIDO");
                throw new ReglaNegocioException("El plan de tu empresa venció. Renueva para seguir usando ValexaFood.", 401,
                    new { planVencido = true });
            }
        }

        usuario.IntentosFallidos = 0;
        usuario.BloqueadoHasta = null;
        usuario.UltimoLogin = DateTime.UtcNow;
        await RegistrarAccesoAsync(usuario, email, true, null);
        return await EmitirSesionAsync(usuario);
    }

    public async Task<SesionDto> RefreshAsync(RefreshDto dto)
    {
        var existente = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.Token == (dto.RefreshToken ?? ""));
        if (existente == null || existente.ExpiresAt < DateTime.UtcNow)
            throw new ReglaNegocioException("Tu sesión expiró. Vuelve a ingresar.", 401);

        if (existente.Revoked)
        {
            // Reuso de un refresh ya rotado: alguien más lo tiene. Se cierran TODAS
            // las sesiones del usuario (el dueño legítimo solo tendrá que volver a entrar).
            await CerrarSesionesAsync(existente.UsuarioId);
            await _db.SaveChangesAsync();
            throw new ReglaNegocioException("Tu sesión se cerró por seguridad. Vuelve a ingresar.", 401);
        }

        var usuario = await UsuariosCompletos().FirstAsync(u => u.UsuarioId == existente.UsuarioId);
        if (!usuario.Activo) throw new ReglaNegocioException("Tu usuario está desactivado.", 401);
        if (usuario.RolId != RolIds.SuperAdmin)
        {
            var t = usuario.Tenant!;
            if (!t.Activo) throw new ReglaNegocioException("El servicio de tu empresa está suspendido.", 401);
            if (t.PlanId == null || PlanVigencia.EstaVencido(t.PlanFechaVencimiento))
                throw new ReglaNegocioException("El plan de tu empresa venció.", 401, new { planVencido = true });
        }

        existente.Revoked = true;
        return await EmitirSesionAsync(usuario);
    }

    public async Task LogoutAsync(RefreshDto dto)
    {
        await _db.RefreshTokens.Where(r => r.Token == (dto.RefreshToken ?? "") && !r.Revoked)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Revoked, true));
    }

    public async Task<SesionDto> CambiarPasswordAsync(int usuarioId, CambiarPasswordDto dto)
    {
        var usuario = await UsuariosCompletos().FirstAsync(u => u.UsuarioId == usuarioId);
        if (!BCrypt.Net.BCrypt.Verify(dto.Actual ?? "", usuario.PasswordHash))
            throw new ReglaNegocioException("La contraseña actual no es correcta.");
        Clave.ValidarFortaleza(dto.Nueva);
        if (dto.Nueva == dto.Actual) throw new ReglaNegocioException("La nueva contraseña debe ser distinta de la actual.");

        usuario.PasswordHash = Clave.Hash(dto.Nueva!);
        usuario.DebeCambiarPassword = false;
        // Cambiar la clave cierra las demás sesiones abiertas con la clave vieja.
        await CerrarSesionesAsync(usuario.UsuarioId, usuario);
        await _bitacora.AgregarAsync(usuario.TenantId, "PASSWORD_CAMBIADA", "Usuario", usuario.UsuarioId,
            $"{usuario.Nombre} cambió su contraseña.");
        return await EmitirSesionAsync(usuario);
    }

    public async Task<PerfilDto> PerfilAsync(int usuarioId)
    {
        var u = await UsuariosCompletos().AsNoTracking().FirstAsync(x => x.UsuarioId == usuarioId);
        var p = await _permisos.CalcularAsync(u.RolId, u.TenantId);
        return new PerfilDto(UsuarioDto(u), p.Permisos, p.Funciones, p.RolesQueOpera, PlanDto(u));
    }

    // Sube la versión de sesión (invalida los JWT vigentes) y revoca los refresh.
    public async Task CerrarSesionesAsync(int usuarioId, Usuario? cargado = null)
    {
        var u = cargado ?? await _db.Usuarios.IgnoreQueryFilters().FirstAsync(x => x.UsuarioId == usuarioId);
        u.SesionVersion++;
        await _db.RefreshTokens.Where(r => r.UsuarioId == usuarioId && !r.Revoked)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Revoked, true));
        _cache.Usuario(usuarioId);
    }

    private async Task<SesionDto> EmitirSesionAsync(Usuario usuario)
    {
        var refresh = new RefreshToken
        {
            UsuarioId = usuario.UsuarioId,
            Token = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
            ExpiresAt = DateTime.UtcNow.Add(VidaRefresh),
            Ip = _bitacora.Ip,
            CreatedAt = DateTime.UtcNow
        };
        _db.RefreshTokens.Add(refresh);
        await _db.SaveChangesAsync();
        _cache.Usuario(usuario.UsuarioId);

        var p = await _permisos.CalcularAsync(usuario.RolId, usuario.TenantId);
        return new SesionDto(_jwt.Generar(usuario), refresh.Token, UsuarioDto(usuario), p.Permisos, p.Funciones,
            p.RolesQueOpera, PlanDto(usuario));
    }

    private static UsuarioSesionDto UsuarioDto(Usuario u) => new(u.UsuarioId, u.Nombre, u.Email, u.RolId,
        u.Rol?.Nombre ?? "", u.TenantId, u.Tenant?.NombreComercial, u.SucursalId, u.Sucursal?.Nombre, u.DebeCambiarPassword);

    private static PlanSesionDto? PlanDto(Usuario u)
    {
        var t = u.Tenant;
        if (t?.Plan == null) return null;
        int? dias = t.PlanFechaVencimiento.HasValue ? t.PlanFechaVencimiento.Value.DayNumber - DiaLima.Hoy().DayNumber : null;
        return new PlanSesionDto(t.Plan.PlanId, t.Plan.Nombre, t.PlanFechaVencimiento, dias);
    }

    private async Task RegistrarAccesoAsync(Usuario? u, string email, bool exito, string? motivo)
    {
        _db.AccesosLog.Add(new AccesoLog
        {
            TenantId = u?.TenantId,
            UsuarioId = u?.UsuarioId,
            Email = email.Length > 120 ? email[..120] : email,
            Exito = exito,
            Motivo = motivo,
            Ip = _bitacora.Ip,
            Fecha = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }
}
