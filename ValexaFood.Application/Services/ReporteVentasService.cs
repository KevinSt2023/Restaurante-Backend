using Microsoft.EntityFrameworkCore;
using ValexaFood.Application.Common;
using ValexaFood.Domain.Entities;
using ValexaFood.Infrastructure.Context;
using ValexaFood.Infrastructure.Sesion;

namespace ValexaFood.Application.Services;

public record ReporteSucursalVentasDto(int SucursalId, string Sucursal, decimal Ventas, int PedidosPagados,
    int PedidosPendientes, decimal PendienteCobro, decimal Efectivo, decimal Digital);
public record ReporteMedioPagoDto(string Medio, decimal Total, int Operaciones);
public record ReporteCanalVentasDto(string Canal, decimal Ventas, int PedidosConMovimientoPago,
    int PedidosPendientes, decimal PendienteCobro);
public record ReporteProductoVendidoDto(string Producto, int Unidades, decimal Total);
public record ReporteVentasDto(DateOnly Fecha, bool Consolidado, decimal TotalVentas, int PedidosPagados,
    int PedidosPendientes, decimal PendienteCobro, decimal Efectivo, decimal Digital,
    List<ReporteSucursalVentasDto> Sucursales, List<ReporteMedioPagoDto> MediosPago,
    List<ReporteCanalVentasDto> Canales, List<ReporteProductoVendidoDto> Productos);

public class ReporteVentasService
{
    private readonly ValexaFoodContext _db;
    private readonly ISesionActual _sesion;
    private readonly PermisosEfectivos _permisos;
    public ReporteVentasService(ValexaFoodContext db, ISesionActual sesion, PermisosEfectivos permisos)
    { _db = db; _sesion = sesion; _permisos = permisos; }

    private int Tenant => _sesion.TenantId ?? throw new ReglaNegocioException("La sesión no pertenece a una empresa.", 403);
    private int Sucursal => _sesion.SucursalId ?? throw new ReglaNegocioException("Selecciona una sucursal antes de consultar reportes.", 428);

    public async Task<ReporteVentasDto> VentasAsync(DateOnly? fechaPedida, bool consolidado)
    {
        var fecha = fechaPedida ?? DiaLima.Hoy();
        if (fecha > DiaLima.Hoy() || fecha < new DateOnly(2020, 1, 1))
            throw new ReglaNegocioException("La fecha debe estar entre 01/01/2020 y hoy.");
        var permisos = await _permisos.DeSesionAsync(_sesion.RolId, Tenant);
        if (consolidado)
        {
            if (_sesion.RolId != RolIds.Administrador || !permisos.Permisos.Contains("PANEL_CENTRALIZADO"))
                throw new ReglaNegocioException("El consolidado requiere Administrador y el permiso de panel centralizado.", 403);
        }

        var sucursales = _db.Sucursales.AsNoTracking();
        if (!consolidado) sucursales = sucursales.Where(s => s.SucursalId == Sucursal && s.Activo);
        var locales = await sucursales.OrderBy(s => s.Nombre)
            .Select(s => new { s.SucursalId, s.Nombre }).ToListAsync();
        if (locales.Count == 0) throw ReglaNegocioException.NoEncontrado("Sucursal activa");
        var ids = locales.Select(x => x.SucursalId).ToList();
        var desde = DiaLima.Inicio(fecha);
        var hasta = DiaLima.Inicio(fecha.AddDays(1));

        var pagosBrutos = await (from p in _db.Pagos.AsNoTracking()
            join pedido in _db.Pedidos.AsNoTracking() on new { p.PedidoId, p.SucursalId } equals new { pedido.PedidoId, pedido.SucursalId }
            where ids.Contains(p.SucursalId) && p.RegistradoEn >= desde && p.RegistradoEn < hasta
            select new { p.PagoId, p.PedidoId, p.SucursalId, p.Medio, p.Monto, pedido.Canal }).ToListAsync();
        var devoluciones = await (from a in _db.AjustesVenta.AsNoTracking()
            join p in _db.Pagos.AsNoTracking() on a.PagoId equals p.PagoId
            join pedido in _db.Pedidos.AsNoTracking() on new { p.PedidoId, p.SucursalId } equals new { pedido.PedidoId, pedido.SucursalId }
            where ids.Contains(a.SucursalId) && a.Tipo == "DEVOLUCION" && a.RegistradoEn >= desde && a.RegistradoEn < hasta
            select new { p.PagoId, p.PedidoId, a.SucursalId, p.Medio, Monto = -a.Monto, pedido.Canal }).ToListAsync();
        var pagos = pagosBrutos.Concat(devoluciones).ToList();
        var pedidosPagados = pagos.Select(p => p.PedidoId).Distinct().ToList();
        var porSucursalPago = pagos.GroupBy(p => p.SucursalId).ToDictionary(g => g.Key, g => new
        {
            Ventas = g.Sum(x => x.Monto), Pedidos = g.Select(x => x.PedidoId).Distinct().Count(),
            Efectivo = g.Where(x => x.Medio == "EFECTIVO").Sum(x => x.Monto),
            Digital = g.Where(x => x.Medio != "EFECTIVO").Sum(x => x.Monto)
        });
        var medios = pagos.GroupBy(p => p.Medio).Select(g => new ReporteMedioPagoDto(g.Key, g.Sum(x => x.Monto), g.Count()))
            .OrderByDescending(x => x.Total).ToList();

        var abiertos = await _db.Pedidos.AsNoTracking()
            .Where(p => ids.Contains(p.SucursalId) && p.CreadoEn >= desde && p.CreadoEn < hasta
                && (p.Estado == "ABIERTO" || p.Estado == "EN_PREPARACION" || p.Estado == "LISTO"))
            .Select(p => new { p.PedidoId, p.SucursalId, p.Canal }).ToListAsync();
        var openIds = abiertos.Select(p => p.PedidoId).ToList();
        var pendingByOrder = await _db.PedidoItems.AsNoTracking()
            .Where(i => ids.Contains(i.SucursalId) && openIds.Contains(i.PedidoId) && i.Estado != "ANULADO")
            .GroupBy(i => new { i.SucursalId, i.PedidoId })
            .Select(g => new { g.Key.SucursalId, g.Key.PedidoId, Total = g.Sum(i => i.Cantidad * i.PrecioUnitario) })
            .ToListAsync();
        var pendientes = abiertos.GroupBy(p => p.SucursalId).ToDictionary(g => g.Key, g => new
        {
            Pedidos = g.Select(x => x.PedidoId).Distinct().Count(),
            Total = pendingByOrder.Where(x => x.SucursalId == g.Key).Sum(x => x.Total)
        });

        var filasProductos = await _db.PedidoItems.AsNoTracking()
            .Where(i => ids.Contains(i.SucursalId) && pedidosPagados.Contains(i.PedidoId) && i.Estado != "ANULADO")
            .GroupBy(i => i.Nombre)
            .Select(g => new { Producto = g.Key, Unidades = g.Sum(i => i.Cantidad), Total = g.Sum(i => i.Cantidad * i.PrecioUnitario) })
            .OrderByDescending(x => x.Unidades).ThenByDescending(x => x.Total).Take(10).ToListAsync();
        var productos = filasProductos.Select(x => new ReporteProductoVendidoDto(x.Producto, x.Unidades, x.Total)).ToList();
        var canales = new[] { "SALON", "MOSTRADOR", "PARA_LLEVAR" }.Select(canal =>
        {
            var movimientos = pagos.Where(p => p.Canal == canal).ToList();
            var pendientesCanal = abiertos.Where(p => p.Canal == canal).ToList();
            var idsPendientes = pendientesCanal.Select(p => (p.SucursalId, p.PedidoId)).ToHashSet();
            var pendiente = pendingByOrder.Where(p => idsPendientes.Contains((p.SucursalId, p.PedidoId))).Sum(p => p.Total);
            return new ReporteCanalVentasDto(canal, movimientos.Sum(p => p.Monto),
                movimientos.Select(p => p.PedidoId).Distinct().Count(), pendientesCanal.Count, pendiente);
        }).ToList();

        var resumen = locales.Select(s =>
        {
            var venta = porSucursalPago.GetValueOrDefault(s.SucursalId);
            var pendiente = pendientes.GetValueOrDefault(s.SucursalId);
            return new ReporteSucursalVentasDto(s.SucursalId, s.Nombre, venta?.Ventas ?? 0m, venta?.Pedidos ?? 0,
                pendiente?.Pedidos ?? 0, pendiente?.Total ?? 0m, venta?.Efectivo ?? 0m, venta?.Digital ?? 0m);
        }).ToList();

        return new ReporteVentasDto(fecha, consolidado, pagos.Sum(p => p.Monto), pedidosPagados.Count,
            abiertos.Count, pendingByOrder.Sum(x => x.Total), pagos.Where(p => p.Medio == "EFECTIVO").Sum(p => p.Monto),
            pagos.Where(p => p.Medio != "EFECTIVO").Sum(p => p.Monto), resumen, medios, canales, productos);
    }
}
