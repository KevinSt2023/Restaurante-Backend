using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ValexaFood.Domain.Entities;
using ValexaFood.Infrastructure.Context;
using ValexaFood.Infrastructure.Sesion;

namespace ValexaFood.Application.Services;

public record BitacoraDto(long BitacoraId, int? TenantId, int? SucursalId, string? Empresa, int? UsuarioId, string UsuarioNombre,
    int? RolId, string Accion, string Entidad, string? EntidadId, string Detalle, string? Ip, DateTime Fecha);

public record BitacoraPaginaDto(List<BitacoraDto> Items, int Total, int Pagina, int TamanoPagina);

// Quién hizo qué. Se agrega al contexto SIN guardar: viaja en el mismo
// SaveChanges/transacción que la acción que describe (si la acción falla, no
// queda rastro de algo que no pasó).
public class BitacoraService
{
    private readonly ValexaFoodContext _db;
    private readonly ISesionActual _sesion;
    private readonly IHttpContextAccessor _http;

    public BitacoraService(ValexaFoodContext db, ISesionActual sesion, IHttpContextAccessor http)
    {
        _db = db;
        _sesion = sesion;
        _http = http;
    }

    public string? Ip => _http.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public async Task AgregarAsync(int? tenantAfectado, string accion, string entidad, object? entidadId, string detalle)
    {
        var usuarioId = _sesion.UsuarioId;
        var nombre = usuarioId == null ? "Sistema" : await _db.Usuarios.IgnoreQueryFilters()
            .Where(u => u.UsuarioId == usuarioId).Select(u => u.Nombre).FirstOrDefaultAsync() ?? "Desconocido";

        _db.Bitacora.Add(new Bitacora
        {
            TenantId = tenantAfectado,
            SucursalId = tenantAfectado == _sesion.TenantId ? _sesion.SucursalId : null,
            UsuarioId = usuarioId,
            UsuarioNombre = nombre,
            RolId = _sesion.RolId >= 0 ? _sesion.RolId : null,
            Accion = accion,
            Entidad = entidad,
            EntidadId = entidadId?.ToString(),
            Detalle = detalle.Length > 1000 ? detalle[..1000] : detalle,
            Ip = Ip,
            Fecha = DateTime.UtcNow
        });
    }

    public async Task<BitacoraPaginaDto> ListarAsync(int? tenantId, string? accion, string? buscar,
        DateOnly? desde, DateOnly? hasta, bool soloSaas, int pagina, int tamano)
    {
        tamano = Math.Clamp(tamano, 10, 200);
        pagina = Math.Max(1, pagina);

        var q = _db.Bitacora.AsNoTracking().AsQueryable();
        if (tenantId.HasValue) q = q.Where(b => b.TenantId == tenantId);
        if (soloSaas) q = q.Where(b => b.RolId == RolIds.SuperAdmin);
        if (!string.IsNullOrWhiteSpace(accion)) q = q.Where(b => b.Accion == accion);
        if (desde.HasValue) { var d = Common.DiaLima.Inicio(desde.Value); q = q.Where(b => b.Fecha >= d); }
        if (hasta.HasValue) { var h = Common.DiaLima.Inicio(hasta.Value.AddDays(1)); q = q.Where(b => b.Fecha < h); }
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var patron = $"%{buscar.Trim()}%";
            q = q.Where(b => EF.Functions.ILike(b.Detalle, patron) || EF.Functions.ILike(b.UsuarioNombre, patron));
        }

        var total = await q.CountAsync();
        var filas = await q.OrderByDescending(b => b.BitacoraId).Skip((pagina - 1) * tamano).Take(tamano).ToListAsync();

        var ids = filas.Where(f => f.TenantId.HasValue).Select(f => f.TenantId!.Value).Distinct().ToList();
        var nombres = await _db.Tenants.AsNoTracking().Where(t => ids.Contains(t.TenantId))
            .ToDictionaryAsync(t => t.TenantId, t => t.NombreComercial);

        return new(filas.Select(b => new BitacoraDto(b.BitacoraId, b.TenantId, b.SucursalId,
            b.TenantId.HasValue ? nombres.GetValueOrDefault(b.TenantId.Value) : null,
            b.UsuarioId, b.UsuarioNombre, b.RolId, b.Accion, b.Entidad, b.EntidadId, b.Detalle, b.Ip, b.Fecha)).ToList(),
            total, pagina, tamano);
    }

    public Task<List<string>> AccionesAsync() =>
        _db.Bitacora.AsNoTracking().Select(b => b.Accion).Distinct().OrderBy(a => a).ToListAsync();
}
