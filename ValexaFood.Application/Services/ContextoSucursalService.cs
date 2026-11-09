using Microsoft.EntityFrameworkCore;
using ValexaFood.Application.Common;
using ValexaFood.Domain.Entities;
using ValexaFood.Infrastructure.Context;
using ValexaFood.Infrastructure.Sesion;

namespace ValexaFood.Application.Services;

public record SucursalDisponibleDto(int SucursalId, string Nombre, bool EsPrincipal, bool EsActiva);
public record SucursalesAsignadasDto(int UsuarioId, List<int> SucursalIds);

public class ContextoSucursalService
{
    private readonly ValexaFoodContext _db;
    private readonly ISesionActual _sesion;
    private readonly BitacoraService _bitacora;

    public ContextoSucursalService(ValexaFoodContext db, ISesionActual sesion, BitacoraService bitacora)
    {
        _db = db;
        _sesion = sesion;
        _bitacora = bitacora;
    }

    public async Task<List<SucursalDisponibleDto>> DisponiblesAsync()
    {
        var tenantId = _sesion.TenantId ?? throw new ReglaNegocioException("La sesión no pertenece a una empresa.", 403);
        IQueryable<Sucursal> query = _db.Sucursales.Where(s => s.Activo && s.TenantId == tenantId);
        if (_sesion.RolId != RolIds.Administrador)
        {
            var usuarioId = _sesion.UsuarioId ?? throw new ReglaNegocioException("La sesión no identifica al usuario.", 401);
            var asignadas = _db.UsuarioSucursales.Where(x => x.TenantId == tenantId && x.UsuarioId == usuarioId)
                .Select(x => x.SucursalId);
            query = query.Where(s => asignadas.Contains(s.SucursalId));
        }

        return await query.AsNoTracking().OrderByDescending(s => s.EsPrincipal).ThenBy(s => s.Nombre)
            .Select(s => new SucursalDisponibleDto(s.SucursalId, s.Nombre, s.EsPrincipal, s.Activo)).ToListAsync();
    }

    public async Task<bool> TieneAccesoAsync(int tenantId, int usuarioId, int rolId, int sucursalId)
    {
        var sucursalActiva = await _db.Sucursales.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(s => s.TenantId == tenantId && s.SucursalId == sucursalId && s.Activo);
        if (!sucursalActiva) return false;
        if (rolId == RolIds.Administrador)
            return await _db.Usuarios.IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(u => u.UsuarioId == usuarioId && u.TenantId == tenantId && u.Activo && u.RolId == rolId);

        return await _db.UsuarioSucursales.AsNoTracking().AnyAsync(x =>
            x.TenantId == tenantId && x.UsuarioId == usuarioId && x.SucursalId == sucursalId);
    }

    public async Task AsignarAsync(int usuarioId, SucursalesAsignadasDto dto)
    {
        if (_sesion.RolId != RolIds.Administrador)
            throw new ReglaNegocioException("Solo el Administrador puede asignar sucursales al personal.", 403);

        var tenantId = _sesion.TenantId ?? throw new ReglaNegocioException("La sesión no pertenece a una empresa.", 403);
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.UsuarioId == usuarioId)
            ?? throw ReglaNegocioException.NoEncontrado("Usuario");
        if (!RolIds.EsOperativo(usuario.RolId))
            throw new ReglaNegocioException("Solo se asignan sucursales a usuarios operativos.");

        var ids = (dto.SucursalIds ?? []).Distinct().ToList();
        if (usuario.Activo && ids.Count == 0)
            throw new ReglaNegocioException("Un usuario activo debe tener al menos una sucursal asignada.");

        var activas = await _db.Sucursales.Where(s => s.Activo && ids.Contains(s.SucursalId))
            .Select(s => s.SucursalId).ToListAsync();
        if (activas.Count != ids.Count)
            throw new ReglaNegocioException("Una o más sucursales no existen, están inactivas o pertenecen a otra empresa.");

        var actuales = await _db.UsuarioSucursales.Where(x => x.UsuarioId == usuarioId).ToListAsync();
        var quitar = actuales.Where(x => !ids.Contains(x.SucursalId)).ToList();
        var agregar = ids.Where(id => actuales.All(x => x.SucursalId != id)).ToList();
        if (quitar.Count == 0 && agregar.Count == 0) return;

        _db.UsuarioSucursales.RemoveRange(quitar);
        _db.UsuarioSucursales.AddRange(agregar.Select(id => new UsuarioSucursal
        {
            TenantId = tenantId, UsuarioId = usuarioId, SucursalId = id, AsignadoEn = DateTime.UtcNow
        }));
        usuario.SucursalId = ids.Count > 0 ? ids[0] : null;

        await _bitacora.AgregarAsync(tenantId, "USUARIO_SUCURSALES", "Usuario", usuarioId,
            $"{usuario.Nombre}: acceso a sucursales [{string.Join(",", ids)}].");
        await _db.SaveChangesAsync();
    }
}
