using Microsoft.AspNetCore.Mvc;
using ValexaFood.Application.Common;
using ValexaFood.Application.Services;
using ValexaFood.Domain.Entities;
using ValexaFood.Infrastructure.Sesion;

namespace ValexaFood.API.Controllers;

/// <summary>
/// El Administrador del restaurante gestiona a su propio equipo.
///
/// Reusa exactamente los mismos métodos que el SuperAdmin: no hay una segunda
/// implementación que se pueda desincronizar. La única diferencia es de dónde sale
/// el tenant — aquí de la sesión, nunca de la URL — así que un Administrador no
/// puede tocar usuarios de otra empresa aunque adivine un id.
///
/// Los candados de siempre siguen puestos: el Administrador es único, el rol debe
/// existir, la sucursal debe ser suya y el cupo del plan lo aplica un trigger en
/// PostgreSQL, no solo el código.
/// </summary>
[Route("api/equipo")]
public class EquipoController : BaseController
{
    private readonly EmpresaService _svc;
    private readonly ISesionActual _sesion;

    public EquipoController(EmpresaService svc, ISesionActual sesion)
    {
        _svc = svc;
        _sesion = sesion;
    }

    private int Tenant => _sesion.TenantId
        ?? throw new ReglaNegocioException("Esta sección es de los restaurantes.", 403);

    [HttpGet, RequierePermiso("USUARIOS_GESTIONAR")]
    public Task<List<UsuarioEmpresaDto>> Listar() => _svc.UsuariosAsync(Tenant);

    /// <summary>
    /// Qué roles le permite su plan y cuánto cupo le queda de cada uno. Así el
    /// Administrador no ofrece un puesto que la base le va a rechazar.
    ///
    /// Los cupos "por local" se cuentan en la sucursal donde está parado: si el
    /// principal tiene sus dos meseros y el anexo ninguno, en el anexo todavía
    /// puede contratar.
    /// </summary>
    [HttpGet("roles"), RequierePermiso("USUARIOS_GESTIONAR")]
    public Task<List<UsoRolDto>> Roles() => _svc.RolesContratablesAsync(Tenant, _sesion.SucursalId);

    [HttpPost, RequierePermiso("USUARIOS_GESTIONAR")]
    public async Task<object> Crear(UsuarioGuardarDto dto)
    {
        // El Administrador no se crea a sí mismo ni crea otro: esa alta es del SuperAdmin.
        if (dto.RolId == RolIds.Administrador)
            throw new ReglaNegocioException(
                "El Administrador lo designa VK Software Solutions. Escríbenos para cambiarlo.");
        var temporal = await _svc.CrearUsuarioAsync(Tenant, dto);
        return new { temporal, usuarios = await _svc.UsuariosAsync(Tenant) };
    }

    [HttpPut("{usuarioId:int}"), RequierePermiso("USUARIOS_GESTIONAR")]
    public async Task<List<UsuarioEmpresaDto>> Editar(int usuarioId, UsuarioGuardarDto dto)
    {
        await ImpedirTocarAlAdministradorAsync(usuarioId);
        if (dto.RolId == RolIds.Administrador)
            throw new ReglaNegocioException("No puedes convertir a alguien en Administrador.");
        await _svc.EditarUsuarioAsync(Tenant, usuarioId, dto);
        return await _svc.UsuariosAsync(Tenant);
    }

    [HttpPost("{usuarioId:int}/estado"), RequierePermiso("USUARIOS_GESTIONAR")]
    public async Task<List<UsuarioEmpresaDto>> Estado(int usuarioId, EstadoDto dto)
    {
        await ImpedirTocarAlAdministradorAsync(usuarioId);
        await _svc.EstadoUsuarioAsync(Tenant, usuarioId, dto);
        return await _svc.UsuariosAsync(Tenant);
    }

    [HttpPost("{usuarioId:int}/reset-password"), RequierePermiso("USUARIOS_GESTIONAR")]
    public async Task<PasswordTemporalDto> ResetPassword(int usuarioId)
    {
        await ImpedirTocarAlAdministradorAsync(usuarioId);
        return await _svc.ResetPasswordAsync(Tenant, usuarioId);
    }

    [HttpPost("{usuarioId:int}/desbloquear"), RequierePermiso("USUARIOS_GESTIONAR")]
    public async Task<List<UsuarioEmpresaDto>> Desbloquear(int usuarioId)
    {
        await ImpedirTocarAlAdministradorAsync(usuarioId);
        await _svc.DesbloquearAsync(Tenant, usuarioId);
        return await _svc.UsuariosAsync(Tenant);
    }

    [HttpPost("{usuarioId:int}/cerrar-sesiones"), RequierePermiso("USUARIOS_GESTIONAR")]
    public async Task<List<UsuarioEmpresaDto>> CerrarSesiones(int usuarioId)
    {
        await ImpedirTocarAlAdministradorAsync(usuarioId);
        await _svc.CerrarSesionesUsuarioAsync(Tenant, usuarioId);
        return await _svc.UsuariosAsync(Tenant);
    }

    /// <summary>
    /// El Administrador no se edita, desactiva ni resetea a sí mismo desde aquí:
    /// quedarse fuera de su propio sistema no se puede deshacer sin soporte.
    /// </summary>
    private async Task ImpedirTocarAlAdministradorAsync(int usuarioId)
    {
        var equipo = await _svc.UsuariosAsync(Tenant);
        var objetivo = equipo.FirstOrDefault(u => u.UsuarioId == usuarioId)
            ?? throw ReglaNegocioException.NoEncontrado("Usuario");
        if (objetivo.RolId == RolIds.Administrador)
            throw new ReglaNegocioException(
                "La cuenta del Administrador se gestiona con VK Software Solutions, no desde aquí.");
    }
}
