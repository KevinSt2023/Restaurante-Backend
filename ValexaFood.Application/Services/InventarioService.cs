using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ValexaFood.Application.Common;
using ValexaFood.Domain.Entities;
using ValexaFood.Infrastructure.Context;
using ValexaFood.Infrastructure.Sesion;

namespace ValexaFood.Application.Services;

public record InsumoGuardarDto(string? Codigo, string Nombre, string UnidadBase, string UnidadCompra,
    decimal FactorCompraABase, decimal StockMinimoBase = 0, decimal? StockObjetivoBase = null);
public record InsumoDto(int InsumoId, string? Codigo, string Nombre, string UnidadBase, string UnidadCompra,
    decimal FactorCompraABase, decimal CostoPromedioBase, bool CostoConfigurado, decimal StockActualBase, decimal StockMinimoBase,
    decimal StockObjetivoBase, bool Activo, decimal CantidadEntranteBase, bool BajoMinimo, bool ReposicionRequerida);
public record InsumoReposicionDto(decimal StockMinimoBase, decimal StockObjetivoBase);
/// <summary>Un insumo cuya existencia no coincide con la suma de su kardex.</summary>
public record CuadreInsumoDto(int InsumoId, string Nombre, string UnidadBase, decimal ExistenciaRegistrada,
    decimal SumaDelKardex, decimal Diferencia, int Movimientos, string Motivo);
public record CuadreInventarioDto(int Insumos, int Movimientos, int Observados, List<CuadreInsumoDto> Detalle);
public record RecetaInsumoGuardarDto(int InsumoId, decimal CantidadBase, int? VarianteId = null, int? OpcionId = null);
public record RecetaProductoGuardarDto(List<RecetaInsumoGuardarDto> Insumos);
public record RecetaProductoDto(long RecetaInsumoId, int ProductoId, int? VarianteId, int? OpcionId,
    int InsumoId, string NombreInsumo, string UnidadBase, decimal CantidadBase);
public record MovimientoInventarioDto(string Tipo, decimal CantidadCompra, decimal? CostoTotal, string? Motivo,
    string? Referencia, string? ClaveIdempotencia);
public record MovimientoInventarioFilaDto(long MovimientoInsumoId, int InsumoId, string NombreInsumo,
    string UnidadBase, string Tipo, decimal CantidadDeltaBase, decimal CantidadRegistrada,
    string UnidadRegistrada, bool CostoConfigurado, decimal? CostoUnitarioBase, decimal? CostoTotal,
    string? Motivo, string? Referencia, long? PedidoId, DateTime RegistradoEn);
public record ConteoInsumoGuardarDto(int InsumoId, decimal StockSistemaBase, decimal CantidadContadaBase);
public record ConteoInventarioGuardarDto(List<ConteoInsumoGuardarDto> Insumos, string? Motivo, string? ClaveIdempotencia);
public record ConteoInventarioLineaDto(long ConteoInventarioItemId, int InsumoId, string NombreInsumo,
    decimal StockSistemaBase, decimal CantidadContadaBase, decimal DiferenciaBase, string UnidadBase,
    bool CostoConfigurado, decimal? CostoUnitarioBase, decimal? CostoTotalDiferencia, long? MovimientoInsumoId);
public record ConteoInventarioDto(long ConteoInventarioId, int SucursalId, string Sucursal, string Estado,
    string Motivo, int RegistradoPor, DateTime RegistradoEn, int? AplicadoPor, DateTime? AplicadoEn,
    List<ConteoInventarioLineaDto> Insumos);
public record TrasladoLineaGuardarDto(int InsumoId, decimal CantidadBase);
public record TrasladoGuardarDto(int SucursalDestinoId, List<TrasladoLineaGuardarDto> Insumos,
    string? Motivo, string? ClaveIdempotencia);
public record TrasladoRecepcionLineaDto(long TrasladoItemId, decimal CantidadRecibidaBase, string? MotivoDiferencia = null);
public record TrasladoRecibirDto(string? ClaveIdempotencia, List<TrasladoRecepcionLineaDto>? Insumos = null);
public record TrasladoLineaDto(long TrasladoItemId, int InsumoId, string NombreInsumo, decimal CantidadBase,
    decimal? CantidadRecibidaBase, decimal CantidadFaltanteBase, string? MotivoDiferencia,
    string UnidadBase, decimal CostoUnitarioBase, decimal CostoTotal);
public record TrasladoDto(long TrasladoId, int SucursalOrigenId, string SucursalOrigen,
    int SucursalDestinoId, string SucursalDestino, string Estado, string? Motivo,
    int RegistradoPor, DateTime RegistradoEn, int? RecibidoPor, DateTime? RecibidoEn,
    List<TrasladoLineaDto> Insumos);
public record ProductoCostoInsumoDto(int InsumoId, string NombreInsumo, string UnidadBase,
    decimal CantidadBaseConsumida, decimal CostoConocido, int LineasSinCosto);
public record ProductoCostoRecetaDto(int ProductoId, string Producto, int Unidades, decimal VentaNetaAsignada,
    decimal CostoRecetaConocido, decimal ContribucionEstimada, int LineasSinConsumo, int LineasSinCosto,
    List<ProductoCostoInsumoDto> Insumos);
public record SucursalCostoRecetaDto(int SucursalId, string Sucursal, decimal VentasNetas,
    decimal CostoRecetaConocido, decimal ContribucionEstimada, int LineasConConsumo,
    int LineasConCostoCompleto, int LineasTotales, int LineasSinConsumo, int LineasSinCosto);
public record AnaliticaCostoRecetaDto(DateOnly Desde, DateOnly Hasta, bool Consolidado,
    decimal VentasNetas, decimal CostoRecetaConocido, decimal ContribucionEstimada,
    int LineasConConsumo, int LineasConCostoCompleto, int LineasTotales, int LineasSinConsumo,
    int LineasSinCosto, decimal CoberturaCostosPct, int ProductosTotal, int ProductosMostrados,
    int PaginaProductos, int PaginasProductos,
    List<SucursalCostoRecetaDto> Sucursales,
    List<ProductoCostoRecetaDto> Productos);
public record ProveedorInsumoGuardarDto(string? Ruc, string Nombre, string? Contacto, string? Telefono);
public record ProveedorInsumoDto(int ProveedorInsumoId, string? Ruc, string Nombre, string? Contacto, string? Telefono, bool Activo);
public record CompraInsumoLineaGuardarDto(int InsumoId, decimal CantidadCompra, decimal CostoTotal);
public record RecepcionCompraGuardarDto(int ProveedorInsumoId, string TipoDocumento, string? SerieDocumento,
    string? NumeroDocumento, DateOnly FechaDocumento, List<CompraInsumoLineaGuardarDto> Insumos,
    string? Motivo, string? ClaveIdempotencia);
public record RecepcionCompraLineaDto(long ItemId, int InsumoId, string NombreInsumo, decimal CantidadCompra,
    string UnidadCompra, decimal FactorCompraABase, decimal CantidadBase, decimal CostoUnitarioBase,
    decimal CostoTotal, string UnidadBase, long? MovimientoId);
public record RecepcionCompraDto(long RecepcionId, int SucursalId, int ProveedorId, string ProveedorNombre,
    string ProveedorRuc, string TipoDocumento, string? SerieDocumento, string? NumeroDocumento,
    DateOnly FechaDocumento, string Estado, decimal TotalInsumos, string? Motivo, int RegistradoPor,
    DateTime RegistradoEn, List<RecepcionCompraLineaDto> Insumos);

public class InventarioService
{
    private const decimal MaxBase = 9_999_999_999.9999m;
    private const decimal MaxCostoBase = 99_999_999.999999m;
    private readonly ValexaFoodContext _db;
    private readonly ISesionActual _sesion;
    private readonly BitacoraService _bitacora;
    private readonly PermisosEfectivos _permisos;
    private int Tenant => _sesion.TenantId ?? throw new ReglaNegocioException("La sesión no pertenece a una empresa.", 403);
    private int Sucursal => _sesion.SucursalId ?? throw new ReglaNegocioException("Selecciona una sucursal antes de operar.", 428);
    private int Usuario => _sesion.UsuarioId ?? throw new ReglaNegocioException("La sesión no identifica al usuario.", 401);

    public InventarioService(ValexaFoodContext db, ISesionActual sesion, BitacoraService bitacora, PermisosEfectivos permisos)
    { _db = db; _sesion = sesion; _bitacora = bitacora; _permisos = permisos; }

    /// <summary>
    /// Auditoría de cuadre del kardex. El stock lo mantiene la aplicación movimiento a
    /// movimiento, así que la única prueba de que es correcto es esta: la suma de TODOS los
    /// movimientos de un insumo en una sucursal tiene que dar su existencia actual.
    /// Devuelve solo lo que no calza. Solo lee.
    /// </summary>
    public async Task<CuadreInventarioDto> CuadreAsync()
    {
        var existencias = await _db.InsumosSucursal.AsNoTracking()
            .Where(e => e.SucursalId == Sucursal)
            .Select(e => new { e.InsumoId, e.StockActualBase })
            .ToListAsync();
        var movimientos = await _db.MovimientosInsumo.AsNoTracking()
            .Where(m => m.SucursalId == Sucursal)
            .GroupBy(m => m.InsumoId)
            .Select(g => new { InsumoId = g.Key, Suma = g.Sum(x => x.CantidadDeltaBase), Cuantos = g.Count() })
            .ToListAsync();
        var nombres = await _db.Insumos.AsNoTracking()
            .Select(i => new { i.InsumoId, i.Nombre, i.UnidadBase }).ToListAsync();

        var observados = new List<CuadreInsumoDto>();
        foreach (var e in existencias)
        {
            var k = movimientos.FirstOrDefault(m => m.InsumoId == e.InsumoId);
            var suma = k?.Suma ?? 0m;
            if (decimal.Round(suma, 4) == decimal.Round(e.StockActualBase, 4)) continue;
            var n = nombres.FirstOrDefault(x => x.InsumoId == e.InsumoId);
            observados.Add(new CuadreInsumoDto(e.InsumoId, n?.Nombre ?? $"Insumo {e.InsumoId}", n?.UnidadBase ?? "",
                e.StockActualBase, suma, decimal.Round(e.StockActualBase - suma, 4), k?.Cuantos ?? 0,
                "La existencia registrada no coincide con la suma de sus movimientos."));
        }
        // Movimientos de un insumo que ni siquiera tiene existencia creada en esta sucursal.
        foreach (var k in movimientos.Where(m => existencias.All(e => e.InsumoId != m.InsumoId)))
        {
            var n = nombres.FirstOrDefault(x => x.InsumoId == k.InsumoId);
            observados.Add(new CuadreInsumoDto(k.InsumoId, n?.Nombre ?? $"Insumo {k.InsumoId}", n?.UnidadBase ?? "",
                0m, k.Suma, decimal.Round(-k.Suma, 4), k.Cuantos,
                "Tiene movimientos pero no tiene existencia creada en esta sucursal."));
        }

        return new CuadreInventarioDto(existencias.Count, movimientos.Sum(m => m.Cuantos),
            observados.Count, observados.OrderBy(o => o.Nombre).ToList());
    }

    public async Task<List<InsumoDto>> ListarAsync(string? buscar = null)
    {
        // El filtro y el orden van sobre la entidad, ANTES de proyectar: EF no puede traducir
        // un Where/OrderBy sobre las propiedades de un DTO construido por constructor.
        var insumos = _db.Insumos.AsNoTracking().Where(i => i.Activo);
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var patron = $"%{buscar.Trim()}%";
            insumos = insumos.Where(i => EF.Functions.ILike(i.Nombre, patron)
                || (i.Codigo != null && EF.Functions.ILike(i.Codigo, patron)));
        }
        var q = from i in insumos
                join s in _db.InsumosSucursal.AsNoTracking().Where(x => x.SucursalId == Sucursal)
                    on new { i.TenantId, i.InsumoId } equals new { s.TenantId, s.InsumoId } into stock
                from s in stock.DefaultIfEmpty()
                orderby i.Nombre
                select new InsumoDto(i.InsumoId, i.Codigo, i.Nombre, i.UnidadBase, i.UnidadCompra,
                    i.FactorCompraABase, s == null ? 0m : s.CostoPromedioBase, s != null && s.CostoPromedioConfigurado,
                    s == null ? 0m : s.StockActualBase,
                    s == null ? 0m : s.StockMinimoBase, s == null ? 0m : s.StockObjetivoBase, i.Activo, 0m,
                    s != null && s.StockMinimoBase > 0 && s.StockActualBase <= s.StockMinimoBase,
                    s != null && s.StockObjetivoBase > 0 && s.StockActualBase < s.StockObjetivoBase);
        var filas = await q.ToListAsync();
        var entrantes = await (from traslado in _db.TrasladosInsumo.AsNoTracking()
                .Where(x => x.TenantId == Tenant && x.SucursalDestinoId == Sucursal && x.Estado == "EN_TRANSITO")
            join item in _db.TrasladosInsumoItems.AsNoTracking()
                on new { traslado.TenantId, traslado.TrasladoId } equals new { item.TenantId, item.TrasladoId }
            group item by item.InsumoId into grupo
            select new { InsumoId = grupo.Key, CantidadBase = grupo.Sum(x => x.CantidadBase) })
            .ToDictionaryAsync(x => x.InsumoId, x => x.CantidadBase);
        return filas.Select(x =>
        {
            var cantidadEntrante = entrantes.GetValueOrDefault(x.InsumoId);
            var bajoMinimo = x.StockMinimoBase > 0 && x.StockActualBase + cantidadEntrante <= x.StockMinimoBase;
            return x with
            {
                CantidadEntranteBase = cantidadEntrante,
                BajoMinimo = bajoMinimo,
                ReposicionRequerida = bajoMinimo
            };
        }).ToList();
    }

    public async Task<InsumoDto> GuardarInsumoAsync(int? id, InsumoGuardarDto dto)
    {
        var codigo = Texto.Opcional(dto.Codigo, "Código", 40);
        var nombre = Texto.Requerido(dto.Nombre, "Nombre", 120);
        var unidadBase = (dto.UnidadBase ?? "").Trim().ToUpperInvariant();
        var unidadCompra = (dto.UnidadCompra ?? "").Trim().ToUpperInvariant();
        if (!UnidadesInventario.Todos.Contains(unidadBase) || unidadCompra.Length is < 1 or > 12)
            throw new ReglaNegocioException("Selecciona una unidad base válida y una unidad de compra de hasta 12 caracteres.");
        if (dto.FactorCompraABase <= 0 || dto.FactorCompraABase > MaxCostoBase || decimal.Round(dto.FactorCompraABase, 6) != dto.FactorCompraABase
            || dto.StockMinimoBase < 0 || dto.StockMinimoBase > MaxBase || decimal.Round(dto.StockMinimoBase, 4) != dto.StockMinimoBase
            || dto.StockObjetivoBase.HasValue && (dto.StockObjetivoBase < dto.StockMinimoBase
                || dto.StockObjetivoBase > MaxBase || decimal.Round(dto.StockObjetivoBase.Value, 4) != dto.StockObjetivoBase.Value))
            throw new ReglaNegocioException("El factor debe ser positivo; el mínimo no puede ser negativo y el objetivo debe ser igual o mayor al mínimo.");
        var idGuardado = await EnTransaccionAsync(async () =>
        {
            Insumo item;
            if (id.HasValue)
            {
                item = await _db.Insumos.FirstOrDefaultAsync(x => x.InsumoId == id && x.Activo)
                    ?? throw ReglaNegocioException.NoEncontrado("Insumo");
                var tieneReceta = await _db.RecetasInsumo.AnyAsync(x => x.InsumoId == item.InsumoId);
                var tieneMovimiento = await _db.MovimientosInsumo.AnyAsync(x => x.InsumoId == item.InsumoId);
                if ((tieneReceta || tieneMovimiento) && item.UnidadBase != unidadBase)
                    throw new ReglaNegocioException("La unidad base no se puede cambiar después de usar el insumo en recetas o movimientos.");
                item.Codigo = codigo; item.Nombre = nombre; item.UnidadBase = unidadBase;
                item.UnidadCompra = unidadCompra; item.FactorCompraABase = dto.FactorCompraABase;
                item.ActualizadoEn = DateTime.UtcNow;
            }
            else
            {
                item = new Insumo { TenantId = Tenant, Codigo = codigo, Nombre = nombre, UnidadBase = unidadBase,
                    UnidadCompra = unidadCompra, FactorCompraABase = dto.FactorCompraABase, CreadoEn = DateTime.UtcNow, ActualizadoEn = DateTime.UtcNow };
                _db.Insumos.Add(item);
                await _db.SaveChangesAsync();
            }
            var stock = await _db.InsumosSucursal.FirstOrDefaultAsync(x => x.SucursalId == Sucursal && x.InsumoId == item.InsumoId);
            var objetivo = dto.StockObjetivoBase ?? Math.Max(stock?.StockObjetivoBase ?? dto.StockMinimoBase, dto.StockMinimoBase);
            if (stock == null)
                _db.InsumosSucursal.Add(new InsumoSucursal { TenantId = Tenant, SucursalId = Sucursal, InsumoId = item.InsumoId,
                    StockMinimoBase = dto.StockMinimoBase, StockObjetivoBase = objetivo, ActualizadoEn = DateTime.UtcNow });
            else { stock.StockMinimoBase = dto.StockMinimoBase; stock.StockObjetivoBase = objetivo; stock.ActualizadoEn = DateTime.UtcNow; }
            await _bitacora.AgregarAsync(Tenant, id.HasValue ? "INSUMO_ACTUALIZADO" : "INSUMO_CREADO", "Insumo", item.InsumoId, $"Insumo {nombre} guardado.");
            await _db.SaveChangesAsync();
            return item.InsumoId;
        });
        return (await ListarAsync()).Single(x => x.InsumoId == idGuardado);
    }

    public async Task<InsumoDto> ConfigurarReposicionAsync(int insumoId, InsumoReposicionDto dto)
    {
        if (insumoId <= 0 || dto is null || dto.StockMinimoBase < 0 || dto.StockObjetivoBase < dto.StockMinimoBase
            || dto.StockObjetivoBase > MaxBase || decimal.Round(dto.StockMinimoBase, 4) != dto.StockMinimoBase
            || decimal.Round(dto.StockObjetivoBase, 4) != dto.StockObjetivoBase)
            throw new ReglaNegocioException("Configura un mínimo no negativo y un objetivo igual o mayor, ambos con hasta cuatro decimales.");
        await EnTransaccionAsync(async () =>
        {
            var unidadBase = await _db.Insumos.Where(x => x.InsumoId == insumoId && x.Activo)
                .Select(x => x.UnidadBase).SingleOrDefaultAsync()
                ?? throw ReglaNegocioException.NoEncontrado("Insumo");
            var stock = await AsegurarStockBloqueadoAsync(insumoId);
            stock.StockMinimoBase = dto.StockMinimoBase;
            stock.StockObjetivoBase = dto.StockObjetivoBase;
            stock.ActualizadoEn = DateTime.UtcNow;
            await _bitacora.AgregarAsync(Tenant, "INSUMO_REPOSICION_CONFIGURADA", "Insumo", insumoId,
                $"Mínimo {dto.StockMinimoBase:0.####} y objetivo {dto.StockObjetivoBase:0.####} {unidadBase} en sucursal {Sucursal}.");
            await _db.SaveChangesAsync();
            return insumoId;
        });
        return (await ListarAsync()).Single(x => x.InsumoId == insumoId);
    }

    public async Task<List<RecetaProductoDto>> RecetaAsync(int productoId)
    {
        if (!await _db.Productos.AnyAsync(p => p.ProductoId == productoId)) throw ReglaNegocioException.NoEncontrado("Producto");
        return await (from r in _db.RecetasInsumo.AsNoTracking().Where(r => r.ProductoId == productoId)
                      join i in _db.Insumos.AsNoTracking() on new { r.TenantId, r.InsumoId } equals new { i.TenantId, i.InsumoId }
                      orderby r.VarianteId, r.OpcionId, i.Nombre
                      select new RecetaProductoDto(r.RecetaInsumoId, r.ProductoId, r.VarianteId, r.OpcionId,
                          r.InsumoId, i.Nombre, i.UnidadBase, r.CantidadBase)).ToListAsync();
    }

    public async Task<List<RecetaProductoDto>> GuardarRecetaAsync(int productoId, RecetaProductoGuardarDto dto)
    {
        if (!await _db.Productos.AnyAsync(p => p.ProductoId == productoId)) throw ReglaNegocioException.NoEncontrado("Producto");
        var filas = dto.Insumos ?? [];
        if (filas.Count > 250 || filas.Any(x => x.InsumoId <= 0 || x.CantidadBase <= 0 || x.CantidadBase > MaxBase
                || decimal.Round(x.CantidadBase, 4) != x.CantidadBase
                || (x.VarianteId.HasValue && x.OpcionId.HasValue))
            || filas.Select(x => (x.InsumoId, x.VarianteId, x.OpcionId)).Distinct().Count() != filas.Count)
            throw new ReglaNegocioException("La receta tiene cantidades, componentes o insumos repetidos no válidos.");
        var insumoIds = filas.Select(x => x.InsumoId).Distinct().ToList();
        if (await _db.Insumos.CountAsync(i => insumoIds.Contains(i.InsumoId) && i.Activo) != insumoIds.Count)
            throw new ReglaNegocioException("Todos los insumos de la receta deben existir y estar activos.");
        await EnTransaccionAsync(async () =>
        {
            var anteriores = await _db.RecetasInsumo.Where(x => x.ProductoId == productoId).ToListAsync();
            _db.RecetasInsumo.RemoveRange(anteriores);
            _db.RecetasInsumo.AddRange(filas.Select(x => new RecetaInsumo { TenantId = Tenant, ProductoId = productoId,
                VarianteId = x.VarianteId, OpcionId = x.OpcionId, InsumoId = x.InsumoId, CantidadBase = x.CantidadBase, ActualizadoEn = DateTime.UtcNow }));
            await _bitacora.AgregarAsync(Tenant, "RECETA_ACTUALIZADA", "Producto", productoId, $"Receta actualizada: {filas.Count} insumos.");
            await _db.SaveChangesAsync();
            return true;
        });
        return await RecetaAsync(productoId);
    }

    public async Task<MovimientoInventarioFilaDto> RegistrarMovimientoAsync(int insumoId, MovimientoInventarioDto dto)
    {
        var tipo = (dto.Tipo ?? "").Trim().ToUpperInvariant();
        if (tipo == "COMPRA") throw new ReglaNegocioException("Registra las compras desde el flujo de recepción para conservar proveedor, documento y trazabilidad.");
        if (tipo is not ("MERMA" or "AJUSTE_ENTRADA" or "AJUSTE_SALIDA"))
            throw new ReglaNegocioException("El tipo de movimiento no es válido.");
        var cantidad = dto.CantidadCompra;
        if (cantidad <= 0 || cantidad > 1_000_000 || decimal.Round(cantidad, 4) != cantidad)
            throw new ReglaNegocioException("La cantidad debe ser mayor a cero y tener máximo cuatro decimales.");
        if (tipo == "COMPRA" && (!dto.CostoTotal.HasValue || dto.CostoTotal < 0 || dto.CostoTotal > MaxBase
                || decimal.Round(dto.CostoTotal.Value, 2) != dto.CostoTotal.Value))
            throw new ReglaNegocioException("Indica el costo total válido de la compra.");
        if (tipo != "COMPRA" && dto.CostoTotal.HasValue) throw new ReglaNegocioException("El costo solo se informa en compras.");
        var key = Guid.TryParse(dto.ClaveIdempotencia, out var parsed) ? parsed.ToString("N") : throw new ReglaNegocioException("Envía una clave de idempotencia válida.");
        var motivo = Texto.Opcional(dto.Motivo, "Motivo", 300);
        var referencia = Texto.Opcional(dto.Referencia, "Referencia", 100);
        return await EnTransaccionAsync(async () =>
        {
        var insumos = await _db.Insumos.FromSqlInterpolated($"""
            SELECT * FROM insumo WHERE tenant_id={Tenant} AND insumo_id={insumoId} AND activo=true FOR UPDATE
            """).IgnoreQueryFilters().ToListAsync();
        var insumo = insumos.SingleOrDefault() ?? throw ReglaNegocioException.NoEncontrado("Insumo");
        var deltaAbs = decimal.Round(cantidad * insumo.FactorCompraABase, 4, MidpointRounding.AwayFromZero);
        if (deltaAbs <= 0 || deltaAbs > MaxBase)
            throw new ReglaNegocioException("La cantidad convertida supera el rango de inventario permitido.");
        var delta = tipo is "COMPRA" or "AJUSTE_ENTRADA" ? deltaAbs : -deltaAbs;
        var costoUnitario = tipo == "COMPRA" && deltaAbs > 0 ? dto.CostoTotal!.Value / deltaAbs : (decimal?)null;
        if (costoUnitario > MaxCostoBase) throw new ReglaNegocioException("El costo unitario supera el rango permitido.");
        var previo = await _db.MovimientosInsumo.AsNoTracking().FirstOrDefaultAsync(x => x.SucursalId == Sucursal && x.ClaveIdempotencia == key);
        if (previo != null)
        {
            if (previo.InsumoId != insumoId || previo.Tipo != tipo || previo.CantidadDeltaBase != delta
                || previo.CostoTotal != dto.CostoTotal || previo.Motivo != motivo || previo.Referencia != referencia)
                throw new ReglaNegocioException("La clave de movimiento ya se usó con datos distintos.");
            return MovimientoMap(previo);
        }
        var existencia = await AsegurarStockBloqueadoAsync(insumoId);
        if (existencia.StockActualBase + delta is > MaxBase or < -MaxBase)
            throw new ReglaNegocioException("El stock resultante supera el rango permitido.");
        if (tipo == "COMPRA")
        {
            var existenciaValorizada = existencia.StockActualBase > 0 ? existencia.StockActualBase : 0m;
            var stockAntes = existenciaValorizada;
            var nuevoStock = stockAntes + deltaAbs;
            existencia.CostoPromedioBase = nuevoStock == 0 ? 0 : decimal.Round((stockAntes * existencia.CostoPromedioBase + dto.CostoTotal!.Value) / nuevoStock, 6, MidpointRounding.AwayFromZero);
            existencia.CostoPromedioConfigurado = true;
        }
        var costoUnitarioSnapshot = tipo == "COMPRA" ? costoUnitario
            : existencia.CostoPromedioConfigurado ? existencia.CostoPromedioBase : (decimal?)null;
        existencia.StockActualBase += delta;
        existencia.ActualizadoEn = DateTime.UtcNow;
        var mov = new MovimientoInsumo { TenantId = Tenant, SucursalId = Sucursal, InsumoId = insumoId, Tipo = tipo,
            CantidadDeltaBase = delta, CostoUnitarioBase = costoUnitarioSnapshot, CostoTotal = dto.CostoTotal,
            CostoConfigurado = tipo == "COMPRA" || existencia.CostoPromedioConfigurado,
            CantidadRegistrada = cantidad, UnidadRegistradaSnapshot = insumo.UnidadCompra,
            NombreInsumoSnapshot = insumo.Nombre, UnidadBaseSnapshot = insumo.UnidadBase,
            Motivo = motivo, Referencia = referencia,
            ClaveIdempotencia = key, RegistradoPor = Usuario, RegistradoEn = DateTime.UtcNow };
        _db.MovimientosInsumo.Add(mov);
        await _bitacora.AgregarAsync(Tenant, "INVENTARIO_MOVIMIENTO", "Insumo", insumoId, $"{tipo}: {delta:0.####} {insumo.UnidadBase}.");
        await _db.SaveChangesAsync();
        return MovimientoMap(mov);
        });
    }

    public async Task<List<MovimientoInventarioFilaDto>> MovimientosAsync(int? insumoId = null, int limite = 100, long? antesDe = null)
    {
        limite = Math.Clamp(limite, 1, 250);
        var q = _db.MovimientosInsumo.AsNoTracking().Where(x => x.SucursalId == Sucursal);
        if (insumoId.HasValue) q = q.Where(x => x.InsumoId == insumoId);
        if (antesDe.HasValue) q = q.Where(x => x.MovimientoInsumoId < antesDe.Value);
        return (await q.OrderByDescending(x => x.MovimientoInsumoId).Take(limite).ToListAsync()).Select(MovimientoMap).ToList();
    }

    public async Task<List<ProveedorInsumoDto>> ProveedoresAsync(string? buscar = null)
    {
        var q = _db.ProveedoresInsumo.AsNoTracking().Where(x => x.Activo);
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var patron = $"%{buscar.Trim()}%";
            q = q.Where(x => EF.Functions.ILike(x.Nombre, patron) || (x.Ruc != null && EF.Functions.ILike(x.Ruc, patron)));
        }
        return await q.OrderBy(x => x.Nombre).Take(250).Select(x => new ProveedorInsumoDto(
            x.ProveedorInsumoId, x.Ruc, x.Nombre, x.Contacto, x.Telefono, x.Activo)).ToListAsync();
    }

    public async Task<ProveedorInsumoDto> GuardarProveedorAsync(ProveedorInsumoGuardarDto dto)
    {
        var nombre = Texto.Requerido(dto.Nombre, "Nombre del proveedor", 120);
        var ruc = string.IsNullOrWhiteSpace(dto.Ruc) ? null : Documento.NormalizarRuc(dto.Ruc);
        var contacto = Texto.Opcional(dto.Contacto, "Contacto", 120);
        var telefono = Texto.Opcional(dto.Telefono, "Teléfono", 20);
        var proveedor = new ProveedorInsumo { TenantId = Tenant, Ruc = ruc, Nombre = nombre,
            Contacto = contacto, Telefono = telefono, Activo = true, CreadoEn = DateTime.UtcNow, ActualizadoEn = DateTime.UtcNow };
        try
        {
            await EnTransaccionAsync(async () =>
            {
                _db.ProveedoresInsumo.Add(proveedor);
                await _db.SaveChangesAsync();
                await _bitacora.AgregarAsync(Tenant, "PROVEEDOR_INSUMO_CREADO", "ProveedorInsumo",
                    proveedor.ProveedorInsumoId, $"Proveedor {nombre} creado.");
                await _db.SaveChangesAsync();
                return true;
            });
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        { throw new ReglaNegocioException("Ya existe un proveedor con ese RUC en esta empresa.", 409); }
        return new(proveedor.ProveedorInsumoId, proveedor.Ruc, proveedor.Nombre, proveedor.Contacto, proveedor.Telefono, proveedor.Activo);
    }

    public async Task<List<RecepcionCompraDto>> RecepcionesCompraAsync(int limite = 100, long? antesDe = null)
    {
        var q = _db.RecepcionesCompraInsumo.AsNoTracking().Where(x => x.SucursalId == Sucursal);
        if (antesDe.HasValue) q = q.Where(x => x.RecepcionCompraInsumoId < antesDe.Value);
        var filas = await q.OrderByDescending(x => x.RecepcionCompraInsumoId)
            .Take(Math.Clamp(limite, 1, 101)).ToListAsync();
        return await RecepcionesCompraMapAsync(filas);
    }

    public async Task<RecepcionCompraDto> RegistrarRecepcionCompraAsync(RecepcionCompraGuardarDto dto)
    {
        if (dto is null) throw new ReglaNegocioException("Indica los datos de la compra.");
        var clave = NormalizarClave(dto.ClaveIdempotencia, "Envía una clave de idempotencia válida para la compra.");
        var tipo = (dto.TipoDocumento ?? "").Trim().ToUpperInvariant();
        if (tipo is not ("FACTURA" or "BOLETA" or "GUIA" or "SIN_DOCUMENTO"))
            throw new ReglaNegocioException("El tipo de documento de compra no es válido.");
        var serie = tipo == "SIN_DOCUMENTO" ? null : Texto.Requerido(dto.SerieDocumento, "Serie", 20).ToUpperInvariant();
        var numero = tipo == "SIN_DOCUMENTO" ? null : Texto.Requerido(dto.NumeroDocumento, "Número", 20).ToUpperInvariant();
        var motivo = Texto.Opcional(dto.Motivo, "Motivo", 300);
        if (dto.FechaDocumento == default) throw new ReglaNegocioException("Indica una fecha de documento válida.");
        if (dto.ProveedorInsumoId <= 0 || dto.Insumos is null || dto.Insumos.Count is < 1 or > 100
            || dto.Insumos.Any(x => x is null || x.InsumoId <= 0 || x.CantidadCompra <= 0 || x.CantidadCompra > 1_000_000
                || decimal.Round(x.CantidadCompra, 4) != x.CantidadCompra || x.CostoTotal < 0 || x.CostoTotal > MaxBase
                || decimal.Round(x.CostoTotal, 2) != x.CostoTotal)
            || dto.Insumos.Select(x => x.InsumoId).Distinct().Count() != dto.Insumos.Count)
            throw new ReglaNegocioException("La compra requiere de 1 a 100 insumos distintos, con cantidades y costos válidos.");
        var lineas = dto.Insumos.OrderBy(x => x.InsumoId).ToList();
        var total = lineas.Sum(x => x.CostoTotal);
        if (total > MaxBase) throw new ReglaNegocioException("El total de la compra supera el rango permitido.");
        var payload = JsonSerializer.Serialize(new { dto.ProveedorInsumoId, tipo, serie, numero, dto.FechaDocumento,
            motivo, insumos = lineas.Select(x => new { x.InsumoId, x.CantidadCompra, x.CostoTotal }) });
        var huella = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        var previo = await _db.RecepcionesCompraInsumo.AsNoTracking().FirstOrDefaultAsync(x => x.SucursalId == Sucursal && x.ClaveIdempotencia == clave);
        if (previo != null) return await IdempotenciaCompraAsync(previo, huella);

        try
        {
            var recepcionId = await EnTransaccionAsync(async () =>
            {
                var repetida = await _db.RecepcionesCompraInsumo.AsNoTracking().FirstOrDefaultAsync(x => x.SucursalId == Sucursal && x.ClaveIdempotencia == clave);
                if (repetida != null)
                {
                    if (repetida.HuellaSolicitud != huella) throw new ReglaNegocioException("La clave de compra ya se utilizó con datos distintos.", 409);
                    return repetida.RecepcionCompraInsumoId;
                }
                var proveedor = await _db.ProveedoresInsumo.FirstOrDefaultAsync(x => x.ProveedorInsumoId == dto.ProveedorInsumoId && x.Activo)
                    ?? throw ReglaNegocioException.NoEncontrado("Proveedor");
                if (tipo != "SIN_DOCUMENTO" && string.IsNullOrWhiteSpace(proveedor.Ruc))
                    throw new ReglaNegocioException("Registra el RUC del proveedor para asociar un comprobante o guía.");
                var articulos = new Dictionary<int, Insumo>();
                foreach (var linea in lineas)
                {
                    var articulo = await _db.Insumos.FromSqlInterpolated($"SELECT * FROM insumo WHERE tenant_id={Tenant} AND insumo_id={linea.InsumoId} AND activo=true FOR UPDATE")
                        .IgnoreQueryFilters().SingleOrDefaultAsync() ?? throw ReglaNegocioException.NoEncontrado("Insumo");
                    articulos.Add(linea.InsumoId, articulo);
                }
                var cabecera = new RecepcionCompraInsumo { TenantId = Tenant, SucursalId = Sucursal,
                    ProveedorInsumoId = proveedor.ProveedorInsumoId, ProveedorRucSnapshot = proveedor.Ruc ?? "",
                    ProveedorNombreSnapshot = proveedor.Nombre, TipoDocumento = tipo, SerieDocumento = serie, NumeroDocumento = numero,
                    FechaDocumento = dto.FechaDocumento, TotalInsumos = total, ClaveIdempotencia = clave,
                    HuellaSolicitud = huella, Motivo = motivo, RegistradoPor = Usuario, RegistradoEn = DateTime.UtcNow };
                _db.RecepcionesCompraInsumo.Add(cabecera);
                await _db.SaveChangesAsync();
                var preparados = new List<(RecepcionCompraInsumoItem Item, Insumo Articulo)>();
                foreach (var linea in lineas)
                {
                    var articulo = articulos[linea.InsumoId];
                    var baseQty = decimal.Round(linea.CantidadCompra * articulo.FactorCompraABase, 4, MidpointRounding.AwayFromZero);
                    var costoUnitario = baseQty == 0 ? 0 : decimal.Round(linea.CostoTotal / baseQty, 6, MidpointRounding.AwayFromZero);
                    if (baseQty <= 0 || baseQty > MaxBase || costoUnitario > MaxCostoBase)
                        throw new ReglaNegocioException("La conversión o el costo unitario supera el rango permitido.");
                    var item = new RecepcionCompraInsumoItem { TenantId = Tenant, RecepcionCompraInsumoId = cabecera.RecepcionCompraInsumoId,
                        InsumoId = articulo.InsumoId, CantidadCompra = linea.CantidadCompra, UnidadCompraSnapshot = articulo.UnidadCompra,
                        FactorCompraABaseSnapshot = articulo.FactorCompraABase, CantidadBase = baseQty, CostoUnitarioBase = costoUnitario,
                        CostoTotal = linea.CostoTotal, NombreInsumoSnapshot = articulo.Nombre, UnidadBaseSnapshot = articulo.UnidadBase };
                    _db.RecepcionesCompraInsumoItems.Add(item); preparados.Add((item, articulo));
                }
                await _db.SaveChangesAsync();
                foreach (var (item, articulo) in preparados)
                {
                    var existencia = await AsegurarStockBloqueadoAsync(item.InsumoId);
                    if (existencia.StockActualBase + item.CantidadBase > MaxBase) throw new ReglaNegocioException("El stock resultante supera el rango permitido.");
                    var anterior = Math.Max(0, existencia.StockActualBase);
                    var nuevo = anterior + item.CantidadBase;
                    existencia.CostoPromedioBase = decimal.Round((anterior * existencia.CostoPromedioBase + item.CostoTotal) / nuevo, 6, MidpointRounding.AwayFromZero);
                    existencia.CostoPromedioConfigurado = true; existencia.StockActualBase += item.CantidadBase; existencia.ActualizadoEn = DateTime.UtcNow;
                    _db.MovimientosInsumo.Add(new MovimientoInsumo { TenantId = Tenant, SucursalId = Sucursal, InsumoId = item.InsumoId,
                        Tipo = "COMPRA", CantidadDeltaBase = item.CantidadBase, CostoUnitarioBase = item.CostoUnitarioBase, CostoTotal = item.CostoTotal,
                        CostoConfigurado = true, CantidadRegistrada = item.CantidadCompra, UnidadRegistradaSnapshot = item.UnidadCompraSnapshot,
                        NombreInsumoSnapshot = item.NombreInsumoSnapshot, UnidadBaseSnapshot = item.UnidadBaseSnapshot,
                        Motivo = motivo ?? $"Recepción {tipo}", Referencia = serie == null ? null : $"{serie}-{numero}",
                        RecepcionCompraInsumoId = cabecera.RecepcionCompraInsumoId, RecepcionCompraInsumoItemId = item.RecepcionCompraInsumoItemId,
                        ClaveIdempotencia = $"{clave[..16]}{item.RecepcionCompraInsumoItemId:X16}", RegistradoPor = Usuario, RegistradoEn = DateTime.UtcNow });
                }
                await _db.SaveChangesAsync();
                cabecera.Estado = "APLICADO"; cabecera.AplicadoPor = Usuario; cabecera.AplicadoEn = DateTime.UtcNow;
                await _bitacora.AgregarAsync(Tenant, "COMPRA_INVENTARIO_APLICADA", "RecepcionCompraInsumo", cabecera.RecepcionCompraInsumoId,
                    $"Compra {tipo} aplicada por {total:0.00}.");
                await _db.SaveChangesAsync();
                return cabecera.RecepcionCompraInsumoId;
            });
            var creada = await _db.RecepcionesCompraInsumo.AsNoTracking().SingleAsync(x => x.RecepcionCompraInsumoId == recepcionId);
            return await RecepcionCompraMapAsync(creada);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            var concurrente = await _db.RecepcionesCompraInsumo.AsNoTracking().FirstOrDefaultAsync(x => x.SucursalId == Sucursal && x.ClaveIdempotencia == clave);
            if (concurrente != null) return await IdempotenciaCompraAsync(concurrente, huella);
            throw new ReglaNegocioException("El proveedor ya tiene registrada esa serie y número de documento.", 409);
        }
    }

    private async Task<RecepcionCompraDto> IdempotenciaCompraAsync(RecepcionCompraInsumo previa, string huella)
    {
        if (previa.HuellaSolicitud != huella) throw new ReglaNegocioException("La clave de compra ya se utilizó con datos distintos.", 409);
        return await RecepcionCompraMapAsync(previa);
    }

    private async Task<RecepcionCompraDto> RecepcionCompraMapAsync(RecepcionCompraInsumo x)
        => (await RecepcionesCompraMapAsync([x])).Single();

    private async Task<List<RecepcionCompraDto>> RecepcionesCompraMapAsync(List<RecepcionCompraInsumo> recepciones)
    {
        if (recepciones.Count == 0) return [];
        var ids = recepciones.Select(x => x.RecepcionCompraInsumoId).ToList();
        var items = await _db.RecepcionesCompraInsumoItems.AsNoTracking().Where(x => ids.Contains(x.RecepcionCompraInsumoId)).OrderBy(x => x.RecepcionCompraInsumoItemId).ToListAsync();
        var movimientos = await _db.MovimientosInsumo.AsNoTracking().Where(x => x.RecepcionCompraInsumoId.HasValue && ids.Contains(x.RecepcionCompraInsumoId.Value))
            .ToDictionaryAsync(x => x.RecepcionCompraInsumoItemId!.Value, x => x.MovimientoInsumoId);
        var porId = items.GroupBy(x => x.RecepcionCompraInsumoId).ToDictionary(g => g.Key, g => g.Select(i => new RecepcionCompraLineaDto(
            i.RecepcionCompraInsumoItemId, i.InsumoId, i.NombreInsumoSnapshot, i.CantidadCompra, i.UnidadCompraSnapshot,
            i.FactorCompraABaseSnapshot, i.CantidadBase, i.CostoUnitarioBase, i.CostoTotal, i.UnidadBaseSnapshot,
            movimientos.GetValueOrDefault(i.RecepcionCompraInsumoItemId))).ToList());
        return recepciones.Select(r => new RecepcionCompraDto(r.RecepcionCompraInsumoId, r.SucursalId, r.ProveedorInsumoId,
            r.ProveedorNombreSnapshot, r.ProveedorRucSnapshot, r.TipoDocumento, r.SerieDocumento, r.NumeroDocumento,
            r.FechaDocumento, r.Estado, r.TotalInsumos, r.Motivo, r.RegistradoPor, r.RegistradoEn,
            porId.GetValueOrDefault(r.RecepcionCompraInsumoId, []))).ToList();
    }

    public async Task<List<ConteoInventarioDto>> ConteosAsync(int limite = 50, long? antesDe = null)
    {
        var q = _db.ConteosInventario.AsNoTracking().Where(x => x.SucursalId == Sucursal);
        if (antesDe.HasValue) q = q.Where(x => x.ConteoInventarioId < antesDe.Value);
        var conteos = await q.OrderByDescending(x => x.ConteoInventarioId)
            .Take(Math.Clamp(limite, 1, 100)).ToListAsync();
        return await ConteosMapAsync(conteos);
    }

    public async Task<ConteoInventarioDto> RegistrarConteoAsync(ConteoInventarioGuardarDto dto)
    {
        if (dto is null) throw new ReglaNegocioException("Indica los insumos del conteo.");
        var clave = NormalizarClave(dto.ClaveIdempotencia, "Envía una clave de idempotencia válida para el conteo.");
        var motivo = Texto.Requerido(dto.Motivo, "Motivo", 300);
        if (dto.Insumos == null || dto.Insumos.Count is < 1 or > 100
            || dto.Insumos.Any(x => x is null || x.InsumoId <= 0 || x.StockSistemaBase is > MaxBase or < -MaxBase
                || x.CantidadContadaBase is < 0 or > MaxBase
                || decimal.Round(x.StockSistemaBase, 4) != x.StockSistemaBase
                || decimal.Round(x.CantidadContadaBase, 4) != x.CantidadContadaBase)
            || dto.Insumos.Select(x => x.InsumoId).Distinct().Count() != dto.Insumos.Count)
            throw new ReglaNegocioException("El conteo requiere de 1 a 100 insumos distintos con stock y cantidades válidas a cuatro decimales.");
        var lineas = dto.Insumos.OrderBy(x => x.InsumoId).ToList();
        if (lineas.Any(x => Math.Abs(x.CantidadContadaBase - x.StockSistemaBase) > MaxBase))
            throw new ReglaNegocioException("La diferencia del conteo supera el rango admitido.");
        var solicitud = JsonSerializer.Serialize(new
        {
            motivo,
            insumos = lineas.Select(x => new { x.InsumoId, x.StockSistemaBase, x.CantidadContadaBase })
        });
        var huella = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(solicitud))).ToLowerInvariant();
        var previo = await _db.ConteosInventario.AsNoTracking().FirstOrDefaultAsync(x =>
            x.SucursalId == Sucursal && x.ClaveIdempotencia == clave);
        if (previo != null) return await IdempotenciaConteoAsync(previo, huella);

        try
        {
            var conteoId = await EnTransaccionAsync(async () =>
            {
                var filas = new Dictionary<int, (InsumoSucursal Stock, Insumo Insumo)>();
                foreach (var linea in lineas)
                {
                    var stock = await AsegurarStockBloqueadoAsync(linea.InsumoId);
                    var insumo = await _db.Insumos.FirstOrDefaultAsync(x => x.InsumoId == linea.InsumoId && x.Activo)
                        ?? throw ReglaNegocioException.NoEncontrado("Insumo");
                    filas.Add(linea.InsumoId, (stock, insumo));
                }

                // A retry behind the first count must replay before evaluating its now-changed stock.
                var repetido = await _db.ConteosInventario.AsNoTracking().FirstOrDefaultAsync(x =>
                    x.SucursalId == Sucursal && x.ClaveIdempotencia == clave);
                if (repetido != null)
                {
                    if (repetido.HuellaSolicitud != huella)
                        throw new ReglaNegocioException("La clave del conteo ya se utilizó con insumos o cantidades distintas.", 409);
                    return repetido.ConteoInventarioId;
                }

                foreach (var linea in lineas)
                {
                    var (stock, _) = filas[linea.InsumoId];
                    if (stock.StockActualBase != linea.StockSistemaBase)
                        throw new ReglaNegocioException("El stock cambió desde que cargaste el conteo. Actualiza la pantalla y vuelve a contar.", 409);
                    var diferencia = linea.CantidadContadaBase - linea.StockSistemaBase;
                    if (Math.Abs(diferencia) > MaxBase)
                        throw new ReglaNegocioException("La diferencia del conteo supera el rango admitido.");
                    if (stock.CostoPromedioBase > MaxCostoBase
                        || (stock.CostoPromedioConfigurado && decimal.Round(Math.Abs(diferencia) * stock.CostoPromedioBase, 4,
                            MidpointRounding.AwayFromZero) > MaxBase))
                        throw new ReglaNegocioException("El costo de la diferencia supera el rango admitido.");
                }

                var ahora = DateTime.UtcNow;
                var conteo = new ConteoInventario
                {
                    TenantId = Tenant, SucursalId = Sucursal, Estado = "ABIERTO", ClaveIdempotencia = clave,
                    HuellaSolicitud = huella, Motivo = motivo, RegistradoPor = Usuario, RegistradoEn = ahora
                };
                _db.ConteosInventario.Add(conteo);
                await _db.SaveChangesAsync();
                var items = lineas.Select(linea =>
                {
                    var (stock, insumo) = filas[linea.InsumoId];
                    var diferencia = linea.CantidadContadaBase - linea.StockSistemaBase;
                    var costo = stock.CostoPromedioConfigurado ? stock.CostoPromedioBase : (decimal?)null;
                    return new ConteoInventarioItem
                    {
                        TenantId = Tenant, ConteoInventarioId = conteo.ConteoInventarioId, InsumoId = insumo.InsumoId,
                        StockSistemaBase = linea.StockSistemaBase, CantidadContadaBase = linea.CantidadContadaBase,
                        DiferenciaBase = diferencia, CostoUnitarioBase = costo, CostoConfigurado = costo.HasValue,
                        CostoTotalDiferencia = costo.HasValue
                            ? decimal.Round(Math.Abs(diferencia) * costo.Value, 4, MidpointRounding.AwayFromZero) : null,
                        NombreInsumoSnapshot = insumo.Nombre, UnidadBaseSnapshot = insumo.UnidadBase
                    };
                }).ToList();
                _db.ConteosInventarioItems.AddRange(items);
                await _db.SaveChangesAsync();

                foreach (var item in items)
                {
                    var (stock, _) = filas[item.InsumoId];
                    stock.StockActualBase = item.CantidadContadaBase;
                    stock.ActualizadoEn = ahora;
                    if (item.DiferenciaBase == 0) continue;
                    _db.MovimientosInsumo.Add(new MovimientoInsumo
                    {
                        TenantId = Tenant, SucursalId = Sucursal, InsumoId = item.InsumoId,
                        Tipo = item.DiferenciaBase > 0 ? "AJUSTE_ENTRADA" : "AJUSTE_SALIDA",
                        CantidadDeltaBase = item.DiferenciaBase, CostoUnitarioBase = item.CostoUnitarioBase,
                        CostoTotal = item.CostoTotalDiferencia, CostoConfigurado = item.CostoConfigurado,
                        CantidadRegistrada = Math.Abs(item.DiferenciaBase),
                        UnidadRegistradaSnapshot = item.UnidadBaseSnapshot,
                        NombreInsumoSnapshot = item.NombreInsumoSnapshot, UnidadBaseSnapshot = item.UnidadBaseSnapshot,
                        Motivo = motivo, Referencia = $"Conteo #{conteo.ConteoInventarioId}",
                        ConteoInventarioId = conteo.ConteoInventarioId, ConteoInventarioItemId = item.ConteoInventarioItemId,
                        ClaveIdempotencia = $"conteo-{conteo.ConteoInventarioId}-{item.ConteoInventarioItemId}",
                        RegistradoPor = Usuario, RegistradoEn = ahora
                    });
                }
                await _db.SaveChangesAsync();
                conteo.Estado = "APLICADO";
                conteo.AplicadoPor = Usuario;
                conteo.AplicadoEn = ahora;
                await _bitacora.AgregarAsync(Tenant, "INVENTARIO_CONTEO_APLICADO", "ConteoInventario",
                    conteo.ConteoInventarioId, $"Conteo aplicado a {items.Count} insumos; motivo: {motivo}.");
                await _db.SaveChangesAsync();
                return conteo.ConteoInventarioId;
            });
            var registrado = await _db.ConteosInventario.AsNoTracking()
                .FirstAsync(x => x.ConteoInventarioId == conteoId);
            return await ConteoMapAsync(registrado);
        }
        catch (DbUpdateException ex) when (ex.GetBaseException() is PostgresException pg && pg.SqlState == "23505")
        {
            _db.ChangeTracker.Clear();
            var concurrente = await _db.ConteosInventario.AsNoTracking().FirstOrDefaultAsync(x =>
                x.SucursalId == Sucursal && x.ClaveIdempotencia == clave);
            if (concurrente == null) throw;
            return await IdempotenciaConteoAsync(concurrente, huella);
        }
    }

    private async Task<ConteoInventarioDto> IdempotenciaConteoAsync(ConteoInventario conteo, string huella)
    {
        if (conteo.HuellaSolicitud != huella)
            throw new ReglaNegocioException("La clave del conteo ya se utilizó con insumos o cantidades distintas.", 409);
        return await ConteoMapAsync(conteo);
    }

    private async Task<ConteoInventarioDto> ConteoMapAsync(ConteoInventario conteo)
        => (await ConteosMapAsync([conteo])).Single();

    private async Task<List<ConteoInventarioDto>> ConteosMapAsync(List<ConteoInventario> conteos)
    {
        if (conteos.Count == 0) return [];
        var ids = conteos.Select(x => x.ConteoInventarioId).ToList();
        var sucursalIds = conteos.Select(x => x.SucursalId).Distinct().ToList();
        var sucursales = await _db.Sucursales.AsNoTracking().Where(x => sucursalIds.Contains(x.SucursalId))
            .ToDictionaryAsync(x => x.SucursalId, x => x.Nombre);
        var items = await _db.ConteosInventarioItems.AsNoTracking().Where(x => ids.Contains(x.ConteoInventarioId))
            .OrderBy(x => x.ConteoInventarioItemId).ToListAsync();
        var movimientos = await _db.MovimientosInsumo.AsNoTracking()
            .Where(x => x.ConteoInventarioId.HasValue && ids.Contains(x.ConteoInventarioId.Value))
            .ToDictionaryAsync(x => x.ConteoInventarioItemId!.Value, x => x.MovimientoInsumoId);
        var porConteo = items.GroupBy(x => x.ConteoInventarioId).ToDictionary(g => g.Key, g => g.Select(x =>
        {
            long? movimientoId = movimientos.TryGetValue(x.ConteoInventarioItemId, out var id) ? id : null;
            return new ConteoInventarioLineaDto(x.ConteoInventarioItemId, x.InsumoId, x.NombreInsumoSnapshot,
                x.StockSistemaBase, x.CantidadContadaBase, x.DiferenciaBase, x.UnidadBaseSnapshot,
                x.CostoConfigurado, x.CostoUnitarioBase, x.CostoTotalDiferencia, movimientoId);
        }).ToList());
        return conteos.Select(x => new ConteoInventarioDto(x.ConteoInventarioId, x.SucursalId,
            sucursales.GetValueOrDefault(x.SucursalId, "Sucursal"), x.Estado, x.Motivo,
            x.RegistradoPor, x.RegistradoEn, x.AplicadoPor, x.AplicadoEn,
            porConteo.GetValueOrDefault(x.ConteoInventarioId, []))).ToList();
    }

    public async Task<List<TrasladoDto>> TrasladosAsync(int limite = 100, long? antesDe = null)
    {
        var q = _db.TrasladosInsumo.AsNoTracking()
            .Where(x => x.SucursalOrigenId == Sucursal || x.SucursalDestinoId == Sucursal);
        if (antesDe.HasValue) q = q.Where(x => x.TrasladoId < antesDe.Value);
        var traslados = await q.OrderByDescending(x => x.TrasladoId)
            .Take(Math.Clamp(limite, 1, 200)).ToListAsync();
        return await TrasladosMapAsync(traslados);
    }

    public async Task<TrasladoDto> RegistrarTrasladoAsync(TrasladoGuardarDto dto)
    {
        if (dto is null) throw new ReglaNegocioException("Indica los datos del traslado.");
        var clave = NormalizarClave(dto.ClaveIdempotencia, "Envía una clave de idempotencia válida para el traslado.");
        if (dto.SucursalDestinoId <= 0 || dto.SucursalDestinoId == Sucursal)
            throw new ReglaNegocioException("Selecciona una sucursal de destino distinta a la sucursal actual.");
        var motivo = Texto.Opcional(dto.Motivo, "Motivo", 300);
        if (dto.Insumos == null || dto.Insumos.Count is < 1 or > 100)
            throw new ReglaNegocioException("Un traslado debe tener entre 1 y 100 insumos.");
        if (dto.Insumos.Any(x => x is null || x.InsumoId <= 0 || x.CantidadBase <= 0 || x.CantidadBase > MaxBase
            || decimal.Round(x.CantidadBase, 4) != x.CantidadBase))
            throw new ReglaNegocioException("Cada insumo requiere una cantidad base positiva de hasta cuatro decimales.");
        var lineas = dto.Insumos.GroupBy(x => x.InsumoId).Select(g => new TrasladoLineaGuardarDto(
            g.Key, decimal.Round(g.Sum(x => x.CantidadBase), 4, MidpointRounding.AwayFromZero))).OrderBy(x => x.InsumoId).ToList();
        if (lineas.Any(x => x.InsumoId <= 0 || x.CantidadBase <= 0 || x.CantidadBase > MaxBase
            || decimal.Round(x.CantidadBase, 4) != x.CantidadBase))
            throw new ReglaNegocioException("Cada insumo requiere una cantidad base positiva de hasta cuatro decimales.");

        var sucursalDestino = await _db.Sucursales.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SucursalId == dto.SucursalDestinoId && x.Activo)
            ?? throw ReglaNegocioException.NoEncontrado("Sucursal destino activa de esta empresa");
        if (_sesion.RolId != RolIds.Administrador && !await _db.UsuarioSucursales.AsNoTracking()
            .AnyAsync(x => x.UsuarioId == Usuario && x.SucursalId == sucursalDestino.SucursalId))
            throw new ReglaNegocioException("No tienes acceso operativo a la sucursal destino.", 403);

        var solicitud = JsonSerializer.Serialize(new
        {
            destino = sucursalDestino.SucursalId,
            motivo,
            insumos = lineas.Select(x => new { x.InsumoId, x.CantidadBase })
        });
        var huella = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(solicitud))).ToLowerInvariant();
        var previo = await _db.TrasladosInsumo.AsNoTracking().FirstOrDefaultAsync(x =>
            x.SucursalOrigenId == Sucursal && x.ClaveIdempotencia == clave);
        if (previo != null) return await IdempotenciaTrasladoAsync(previo, huella);

        try
        {
            return await EnTransaccionAsync(async () =>
            {
                var stocks = new Dictionary<int, (InsumoSucursal Stock, Insumo Insumo)>();
                foreach (var linea in lineas)
                {
                    var stock = await AsegurarStockBloqueadoAsync(linea.InsumoId);
                    var insumo = await _db.Insumos.FirstOrDefaultAsync(x => x.InsumoId == linea.InsumoId && x.Activo)
                        ?? throw ReglaNegocioException.NoEncontrado("Insumo");
                    stocks.Add(linea.InsumoId, (stock, insumo));
                }

                // A retry that waited behind the first request must replay its result
                // before checking stock, which the first request has already reduced.
                var yaRegistrado = await _db.TrasladosInsumo.AsNoTracking().FirstOrDefaultAsync(x =>
                    x.SucursalOrigenId == Sucursal && x.ClaveIdempotencia == clave);
                if (yaRegistrado != null) return await IdempotenciaTrasladoAsync(yaRegistrado, huella);

                foreach (var linea in lineas)
                {
                    var (stock, insumo) = stocks[linea.InsumoId];
                    if (stock.StockActualBase < linea.CantidadBase)
                        throw new ReglaNegocioException($"Stock insuficiente de {insumo.Nombre} en la sucursal origen.", 409);
                    if (!stock.CostoPromedioConfigurado)
                        throw new ReglaNegocioException($"Registra primero el costo de {insumo.Nombre} en la sucursal origen.", 409);
                    if (stock.CostoPromedioBase > MaxCostoBase
                        || decimal.Round(linea.CantidadBase * stock.CostoPromedioBase, 4, MidpointRounding.AwayFromZero) > MaxBase)
                        throw new ReglaNegocioException($"El costo del traslado de {insumo.Nombre} supera el rango permitido.");
                }

                var ahora = DateTime.UtcNow;
                var traslado = new TrasladoInsumo
                {
                    TenantId = Tenant, SucursalOrigenId = Sucursal, SucursalDestinoId = sucursalDestino.SucursalId,
                    Estado = "EN_TRANSITO", ClaveIdempotencia = clave, HuellaSolicitud = huella,
                    Motivo = motivo, RegistradoPor = Usuario, RegistradoEn = ahora
                };
                _db.TrasladosInsumo.Add(traslado);
                await _db.SaveChangesAsync();
                var items = lineas.Select(linea =>
                {
                    var (stock, insumo) = stocks[linea.InsumoId];
                    return new TrasladoInsumoItem
                    {
                        TenantId = Tenant, TrasladoId = traslado.TrasladoId, InsumoId = insumo.InsumoId,
                        CantidadBase = linea.CantidadBase, CostoUnitarioBase = stock.CostoPromedioBase,
                        CostoTotal = decimal.Round(linea.CantidadBase * stock.CostoPromedioBase, 4, MidpointRounding.AwayFromZero),
                        NombreInsumoSnapshot = insumo.Nombre, UnidadBaseSnapshot = insumo.UnidadBase
                    };
                }).ToList();
                _db.TrasladosInsumoItems.AddRange(items);
                await _db.SaveChangesAsync();

                foreach (var item in items)
                {
                    var (stock, _) = stocks[item.InsumoId];
                    stock.StockActualBase -= item.CantidadBase;
                    stock.ActualizadoEn = ahora;
                    _db.MovimientosInsumo.Add(new MovimientoInsumo
                    {
                        TenantId = Tenant, SucursalId = Sucursal, InsumoId = item.InsumoId,
                        Tipo = "TRANSFERENCIA_SALIDA", CantidadDeltaBase = -item.CantidadBase,
                        CostoUnitarioBase = item.CostoUnitarioBase, CostoTotal = item.CostoTotal,
                        CostoConfigurado = true, CantidadRegistrada = item.CantidadBase,
                        UnidadRegistradaSnapshot = item.UnidadBaseSnapshot, NombreInsumoSnapshot = item.NombreInsumoSnapshot,
                        UnidadBaseSnapshot = item.UnidadBaseSnapshot, Motivo = motivo,
                        Referencia = $"Traslado #{traslado.TrasladoId} a {sucursalDestino.Nombre}",
                        TrasladoId = traslado.TrasladoId, TrasladoItemId = item.TrasladoItemId,
                        ClaveIdempotencia = $"traslado-{traslado.TrasladoId}-{item.TrasladoItemId}-salida",
                        RegistradoPor = Usuario, RegistradoEn = ahora
                    });
                }
                await _bitacora.AgregarAsync(Tenant, "INVENTARIO_TRASLADO", "TrasladoInsumo", traslado.TrasladoId,
                    $"Traslado #{traslado.TrasladoId} enviado a {sucursalDestino.Nombre}.");
                await _db.SaveChangesAsync();
                return await TrasladoMapAsync(traslado);
            });
        }
        catch (DbUpdateException ex) when (ex.GetBaseException() is PostgresException pg && pg.SqlState == "23505")
        {
            _db.ChangeTracker.Clear();
            var concurrente = await _db.TrasladosInsumo.AsNoTracking().FirstOrDefaultAsync(x =>
                x.SucursalOrigenId == Sucursal && x.ClaveIdempotencia == clave);
            if (concurrente == null) throw;
            return await IdempotenciaTrasladoAsync(concurrente, huella);
        }
    }

    public async Task<TrasladoDto> RecibirTrasladoAsync(long trasladoId, TrasladoRecibirDto dto)
    {
        if (dto is null || trasladoId <= 0) throw new ReglaNegocioException("Indica el traslado y su clave de recepción.");
        var clave = NormalizarClave(dto.ClaveIdempotencia, "Envía una clave de idempotencia válida para la recepción.");
        var reciboEnOtroTraslado = await _db.TrasladosInsumo.AsNoTracking().AnyAsync(x =>
            x.SucursalDestinoId == Sucursal && x.ClaveRecepcion == clave && x.TrasladoId != trasladoId);
        if (reciboEnOtroTraslado) throw new ReglaNegocioException("La clave de recepción ya fue usada en otro traslado.", 409);

        try
        {
            return await EnTransaccionAsync(async () =>
            {
                var filas = await _db.TrasladosInsumo.FromSqlInterpolated($"""
                    SELECT * FROM traslado_insumo WHERE tenant_id={Tenant} AND sucursal_destino_id={Sucursal}
                        AND traslado_id={trasladoId} FOR UPDATE
                    """).IgnoreQueryFilters().ToListAsync();
                var traslado = filas.SingleOrDefault() ?? throw ReglaNegocioException.NoEncontrado("Traslado entrante");
                var items = await _db.TrasladosInsumoItems.AsNoTracking()
                    .Where(x => x.TrasladoId == trasladoId).OrderBy(x => x.TrasladoItemId).ToListAsync();
                if (items.Count == 0) throw new ReglaNegocioException("El traslado no tiene insumos para recibir.", 409);
                var solicitudRecepcion = NormalizarRecepcion(items, dto.Insumos);
                var huella = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(solicitudRecepcion.Select(x => new
                    {
                        x.TrasladoItemId, x.CantidadRecibidaBase, x.MotivoDiferencia
                    }))))).ToLowerInvariant();
                var itemsPorId = items.ToDictionary(x => x.TrasladoItemId);

                if (traslado.Estado != "EN_TRANSITO")
                {
                    if (traslado.ClaveRecepcion != clave)
                        throw new ReglaNegocioException("El traslado ya fue recibido con otra clave.", 409);
                    var repeticionCompletaAnterior = traslado.Estado == "RECIBIDO" && traslado.HuellaRecepcion == null
                        && solicitudRecepcion.All(x => x.CantidadRecibidaBase == itemsPorId[x.TrasladoItemId].CantidadBase
                            && x.MotivoDiferencia == null);
                    if (traslado.HuellaRecepcion != huella && !repeticionCompletaAnterior)
                        throw new ReglaNegocioException("La clave de recepción ya se utilizó con cantidades distintas.", 409);
                    return await TrasladoMapAsync(traslado);
                }

                var ahora = DateTime.UtcNow;
                foreach (var linea in solicitudRecepcion.Where(x => x.CantidadRecibidaBase > 0).OrderBy(x => x.TrasladoItemId))
                {
                    var item = itemsPorId[linea.TrasladoItemId];
                    var stock = await AsegurarStockBloqueadoAsync(item.InsumoId);
                    var stockPositivo = Math.Max(stock.StockActualBase, 0m);
                    var nuevoStockValorizado = stockPositivo + linea.CantidadRecibidaBase;
                    if (stock.StockActualBase + linea.CantidadRecibidaBase > MaxBase
                        || nuevoStockValorizado > MaxBase || item.CostoUnitarioBase > MaxCostoBase)
                        throw new ReglaNegocioException("El stock o costo recibido supera el rango permitido.");
                    stock.CostoPromedioBase = decimal.Round((stockPositivo * stock.CostoPromedioBase
                        + linea.CantidadRecibidaBase * item.CostoUnitarioBase) / nuevoStockValorizado,
                        6, MidpointRounding.AwayFromZero);
                    stock.CostoPromedioConfigurado = true;
                    stock.StockActualBase += linea.CantidadRecibidaBase;
                    stock.ActualizadoEn = ahora;
                }

                _db.TrasladosInsumoRecepcionItems.AddRange(solicitudRecepcion.Select(x =>
                    new TrasladoInsumoRecepcionItem
                    {
                        TenantId = Tenant, TrasladoId = traslado.TrasladoId,
                        TrasladoItemId = x.TrasladoItemId, CantidadRecibidaBase = x.CantidadRecibidaBase,
                        MotivoDiferencia = x.MotivoDiferencia
                    }));
                await _db.SaveChangesAsync();

                foreach (var linea in solicitudRecepcion.Where(x => x.CantidadRecibidaBase > 0))
                {
                    var item = itemsPorId[linea.TrasladoItemId];
                    _db.MovimientosInsumo.Add(new MovimientoInsumo
                    {
                        TenantId = Tenant, SucursalId = Sucursal, InsumoId = item.InsumoId,
                        Tipo = "TRANSFERENCIA_ENTRADA", CantidadDeltaBase = linea.CantidadRecibidaBase,
                        CostoUnitarioBase = item.CostoUnitarioBase,
                        CostoTotal = decimal.Round(linea.CantidadRecibidaBase * item.CostoUnitarioBase, 4, MidpointRounding.AwayFromZero),
                        CostoConfigurado = true, CantidadRegistrada = linea.CantidadRecibidaBase,
                        UnidadRegistradaSnapshot = item.UnidadBaseSnapshot, NombreInsumoSnapshot = item.NombreInsumoSnapshot,
                        UnidadBaseSnapshot = item.UnidadBaseSnapshot, Motivo = linea.MotivoDiferencia ?? traslado.Motivo,
                        Referencia = $"Traslado #{traslado.TrasladoId} recibido",
                        TrasladoId = traslado.TrasladoId, TrasladoItemId = item.TrasladoItemId,
                        ClaveIdempotencia = $"traslado-{traslado.TrasladoId}-{item.TrasladoItemId}-entrada",
                        RegistradoPor = Usuario, RegistradoEn = ahora
                    });
                }
                await _db.SaveChangesAsync();
                var hayDiferencia = solicitudRecepcion.Any(x => x.CantidadRecibidaBase < itemsPorId[x.TrasladoItemId].CantidadBase);
                traslado.Estado = hayDiferencia ? "RECIBIDO_CON_DIFERENCIA" : "RECIBIDO";
                traslado.ClaveRecepcion = clave;
                traslado.HuellaRecepcion = huella;
                traslado.RecibidoPor = Usuario;
                traslado.RecibidoEn = ahora;
                await _bitacora.AgregarAsync(Tenant,
                    hayDiferencia ? "INVENTARIO_TRASLADO_RECIBIDO_CON_DIFERENCIA" : "INVENTARIO_TRASLADO_RECIBIDO",
                    "TrasladoInsumo", traslado.TrasladoId,
                    hayDiferencia
                        ? $"Traslado #{traslado.TrasladoId} recibido con cantidades faltantes; revisar detalle por insumo."
                        : $"Traslado #{traslado.TrasladoId} recibido completo en sucursal {Sucursal}.");
                await _db.SaveChangesAsync();
                return await TrasladoMapAsync(traslado);
            });
        }
        catch (DbUpdateException ex) when (ex.GetBaseException() is PostgresException pg && pg.SqlState == "23505")
        {
            _db.ChangeTracker.Clear();
            var yaUsada = await _db.TrasladosInsumo.AsNoTracking().FirstOrDefaultAsync(x =>
                x.SucursalDestinoId == Sucursal && x.ClaveRecepcion == clave);
            if (yaUsada == null) throw;
            if (yaUsada.TrasladoId != trasladoId)
                throw new ReglaNegocioException("La clave de recepción ya fue usada en otro traslado.", 409);
            return await TrasladoMapAsync(yaUsada);
        }
    }

    public async Task<AnaliticaCostoRecetaDto> AnaliticaCostosAsync(DateOnly? desdePedida, DateOnly? hastaPedida,
        bool consolidado, int paginaProductos = 1, bool priorizarPendientes = false)
    {
        if (paginaProductos is < 1 or > 100000) throw new ReglaNegocioException("La página de productos debe ser mayor que cero.");
        const int tamanoPaginaProductos = 50;
        var desdeFecha = desdePedida ?? DiaLima.Hoy();
        var hastaFecha = hastaPedida ?? desdeFecha;
        if (desdeFecha < new DateOnly(2020, 1, 1) || hastaFecha < desdeFecha || hastaFecha > DiaLima.Hoy()
            || hastaFecha.DayNumber - desdeFecha.DayNumber > 365)
            throw new ReglaNegocioException("El rango debe estar entre 01/01/2020 y hoy y no superar 366 días.");

        var efectivos = await _permisos.DeSesionAsync(_sesion.RolId, Tenant);
        if (consolidado && (_sesion.RolId != RolIds.Administrador || !efectivos.Permisos.Contains("PANEL_CENTRALIZADO")))
            throw new ReglaNegocioException("El consolidado requiere Administrador y el permiso de panel centralizado.", 403);

        var localesQ = _db.Sucursales.AsNoTracking().Where(s => s.Activo);
        if (!consolidado) localesQ = localesQ.Where(s => s.SucursalId == Sucursal);
        var locales = await localesQ.OrderBy(s => s.Nombre).Select(s => new { s.SucursalId, s.Nombre }).ToListAsync();
        if (locales.Count == 0) throw ReglaNegocioException.NoEncontrado("Sucursal activa");
        var sucursalIds = locales.Select(s => s.SucursalId).ToList();
        var inicio = DiaLima.Inicio(desdeFecha);
        var fin = DiaLima.Inicio(hastaFecha.AddDays(1));
        var pedidosQ = _db.Pedidos.AsNoTracking()
            .Where(p => sucursalIds.Contains(p.SucursalId) && p.Estado == "PAGADO"
                && p.PagadoEn >= inicio && p.PagadoEn < fin);
        var pedidos = await pedidosQ
            .Select(p => new { p.PedidoId, p.SucursalId, p.PagadoEn }).ToListAsync();
        if (pedidos.Count == 0)
        {
            var vacias = locales.Select(s => new SucursalCostoRecetaDto(s.SucursalId, s.Nombre,
                0m, 0m, 0m, 0, 0, 0, 0, 0)).ToList();
            return new(desdeFecha, hastaFecha, consolidado, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, vacias, []);
        }

        var pedidoIdsQ = pedidosQ.Select(p => p.PedidoId);
        var pagos = await _db.Pagos.AsNoTracking().Where(p => pedidoIdsQ.Contains(p.PedidoId))
            .GroupBy(p => p.PedidoId).Select(g => new { PedidoId = g.Key, Total = g.Sum(p => p.Monto) }).ToListAsync();
        var devoluciones = await _db.AjustesVenta.AsNoTracking().Where(a => pedidoIdsQ.Contains(a.PedidoId) && a.Tipo == "DEVOLUCION")
            .GroupBy(a => a.PedidoId).Select(g => new { PedidoId = g.Key, Total = g.Sum(a => a.Monto) }).ToListAsync();
        var renglones = await _db.PedidoItems.AsNoTracking().Where(i => pedidoIdsQ.Contains(i.PedidoId) && i.Estado != "ANULADO")
            .Select(i => new { i.PedidoItemId, i.PedidoId, i.SucursalId, i.ProductoId, i.Nombre, i.Cantidad, i.PrecioUnitario })
            .ToListAsync();
        var consumosPorItem = await _db.MovimientosInsumo.AsNoTracking()
            .Where(m => m.PedidoId != null && pedidoIdsQ.Contains(m.PedidoId.Value) && m.Tipo == "CONSUMO_VENTA"
                && m.PedidoItemId != null)
            .GroupBy(m => m.PedidoItemId!.Value)
            .Select(g => new
            {
                PedidoItemId = g.Key,
                Costo = g.Where(m => m.CostoConfigurado).Sum(m => m.CostoTotal ?? 0m),
                Completo = g.All(m => m.CostoConfigurado)
            })
            .ToListAsync();
        var costoPorItem = consumosPorItem.ToDictionary(m => m.PedidoItemId, m => m.Costo);
        var itemConConsumo = consumosPorItem.Select(m => m.PedidoItemId).ToHashSet();
        var itemCostoCompleto = consumosPorItem.Where(m => m.Completo).Select(m => m.PedidoItemId).ToHashSet();
        var pagoPorPedido = pagos.ToDictionary(x => x.PedidoId, x => x.Total);
        var devolucionPorPedido = devoluciones.ToDictionary(x => x.PedidoId, x => x.Total);
        var lineasPorPedido = renglones.GroupBy(x => x.PedidoId).ToDictionary(g => g.Key, g => g.ToList());
        var ingresoPorItem = new Dictionary<long, decimal>();

        foreach (var pedido in pedidos)
        {
            var lineas = lineasPorPedido.GetValueOrDefault(pedido.PedidoId) ?? [];
            if (lineas.Count == 0) continue;
            var ventasNeta = pagoPorPedido.GetValueOrDefault(pedido.PedidoId) - devolucionPorPedido.GetValueOrDefault(pedido.PedidoId);
            var bruto = lineas.Sum(i => (decimal)i.Cantidad * i.PrecioUnitario);
            if (ventasNeta < 0 || bruto <= 0)
                throw new ReglaNegocioException($"El pedido {pedido.PedidoId} tiene ventas o pagos inconsistentes; revisa su conciliación.", 409);
            var brutoLineas = lineas.Select(i => (decimal)i.Cantidad * i.PrecioUnitario).ToList();
            var asignado = brutoLineas.Select(x => bruto == 0 ? 0m : decimal.Floor(ventasNeta * x / bruto * 100m) / 100m).ToArray();
            var centavosRestantes = decimal.ToInt32(decimal.Round((ventasNeta - asignado.Sum()) * 100m, 0, MidpointRounding.AwayFromZero));
            var prioridad = brutoLineas.Select((x, idx) => new
            {
                Indice = idx,
                Residuo = bruto == 0 ? 0m : ventasNeta * x / bruto - asignado[idx]
            }).OrderByDescending(x => x.Residuo).ThenBy(x => lineas[x.Indice].PedidoItemId).ToList();
            for (var idx = 0; idx < centavosRestantes; idx++)
                asignado[prioridad[idx % prioridad.Count].Indice] += 0.01m;
            for (var idx = 0; idx < lineas.Count; idx++)
            {
                ingresoPorItem[lineas[idx].PedidoItemId] = asignado[idx];
            }
        }

        var porSucursal = new List<SucursalCostoRecetaDto>();
        foreach (var local in locales)
        {
            var items = renglones.Where(i => i.SucursalId == local.SucursalId).ToList();
            var ingreso = items.Sum(i => ingresoPorItem.GetValueOrDefault(i.PedidoItemId));
            var costo = items.Sum(i => costoPorItem.GetValueOrDefault(i.PedidoItemId));
            var conConsumo = items.Count(i => itemConConsumo.Contains(i.PedidoItemId));
            var total = items.Count;
            var costoCompleto = items.Count(i => itemCostoCompleto.Contains(i.PedidoItemId));
            porSucursal.Add(new(local.SucursalId, local.Nombre, ingreso, costo, ingreso - costo,
                conConsumo, costoCompleto, total, total - conConsumo, conConsumo - costoCompleto));
        }

        var resumenProductos = renglones.GroupBy(i => i.ProductoId).Select(g =>
        {
            var ingreso = g.Sum(i => ingresoPorItem.GetValueOrDefault(i.PedidoItemId));
            var costo = g.Sum(i => costoPorItem.GetValueOrDefault(i.PedidoItemId));
            var sinConsumo = g.Count(i => !itemConConsumo.Contains(i.PedidoItemId));
            var sinCosto = g.Count(i => itemConConsumo.Contains(i.PedidoItemId) && !itemCostoCompleto.Contains(i.PedidoItemId));
            var nombre = g.GroupBy(i => i.Nombre).OrderByDescending(n => n.Count()).ThenBy(n => n.Key).First().Key;
            return new
            {
                ProductoId = g.Key, Producto = nombre, Unidades = g.Sum(i => i.Cantidad),
                VentaNetaAsignada = ingreso, CostoRecetaConocido = costo,
                ContribucionEstimada = ingreso - costo, LineasSinConsumo = sinConsumo, LineasSinCosto = sinCosto
            };
        }).OrderBy(x => priorizarPendientes && x.LineasSinConsumo + x.LineasSinCosto == 0 ? 1 : 0)
            .ThenBy(x => x.ContribucionEstimada).ThenBy(x => x.Producto).ThenBy(x => x.ProductoId).ToList();
        var productosTotal = resumenProductos.Count;
        var paginasProductos = (productosTotal + tamanoPaginaProductos - 1) / tamanoPaginaProductos;
        var paginaActual = Math.Min(paginaProductos, Math.Max(paginasProductos, 1));
        var resumenPagina = resumenProductos.Skip((paginaActual - 1) * tamanoPaginaProductos)
            .Take(tamanoPaginaProductos).ToList();
        var idsPagina = resumenPagina.Select(x => x.ProductoId).ToHashSet();
        var itemsPorProducto = renglones.Where(i => idsPagina.Contains(i.ProductoId))
            .GroupBy(i => i.ProductoId).ToDictionary(g => g.Key, g => g.Select(i => i.PedidoItemId).ToHashSet());
        var idsItemsPagina = itemsPorProducto.Values.SelectMany(x => x).ToHashSet();
        var consumosPagina = await _db.MovimientosInsumo.AsNoTracking()
            .Where(m => m.PedidoId != null && pedidoIdsQ.Contains(m.PedidoId.Value) && m.Tipo == "CONSUMO_VENTA"
                && m.PedidoItemId != null && idsItemsPagina.Contains(m.PedidoItemId.Value))
            .Select(m => new { m.PedidoId, m.PedidoItemId, m.InsumoId, m.NombreInsumoSnapshot,
                m.UnidadBaseSnapshot, m.CantidadDeltaBase, m.CostoTotal, m.CostoConfigurado })
            .ToListAsync();
        var productos = resumenPagina.Select(resumen =>
        {
            var itemIds = itemsPorProducto.GetValueOrDefault(resumen.ProductoId) ?? [];
            var insumos = consumosPagina.Where(m => m.PedidoItemId.HasValue && itemIds.Contains(m.PedidoItemId.Value))
                .GroupBy(m => new { m.InsumoId, m.NombreInsumoSnapshot, m.UnidadBaseSnapshot })
                .Select(ig => new ProductoCostoInsumoDto(ig.Key.InsumoId, ig.Key.NombreInsumoSnapshot,
                    ig.Key.UnidadBaseSnapshot, -ig.Sum(m => m.CantidadDeltaBase),
                    ig.Where(m => m.CostoConfigurado).Sum(m => m.CostoTotal ?? 0m),
                    ig.Where(m => !m.CostoConfigurado).Select(m => m.PedidoItemId).Distinct().Count()))
                .OrderByDescending(i => i.CostoConocido).ThenBy(i => i.NombreInsumo).ToList();
            return new ProductoCostoRecetaDto(resumen.ProductoId, resumen.Producto, resumen.Unidades,
                resumen.VentaNetaAsignada, resumen.CostoRecetaConocido, resumen.ContribucionEstimada,
                resumen.LineasSinConsumo, resumen.LineasSinCosto, insumos);
        }).ToList();

        var ventasNetaTotal = porSucursal.Sum(x => x.VentasNetas);
        var costoTotal = porSucursal.Sum(x => x.CostoRecetaConocido);
        var lineasConConsumo = porSucursal.Sum(x => x.LineasConConsumo);
        var lineasConCostoCompleto = porSucursal.Sum(x => x.LineasConCostoCompleto);
        var lineasTotales = porSucursal.Sum(x => x.LineasTotales);
        var lineasSinConsumo = porSucursal.Sum(x => x.LineasSinConsumo);
        var lineasSinCosto = porSucursal.Sum(x => x.LineasSinCosto);
        var cobertura = lineasTotales == 0 ? 0 : decimal.Round(100m * lineasConCostoCompleto / lineasTotales, 2, MidpointRounding.AwayFromZero);
        return new(desdeFecha, hastaFecha, consolidado, ventasNetaTotal, costoTotal, ventasNetaTotal - costoTotal,
            lineasConConsumo, lineasConCostoCompleto, lineasTotales, lineasSinConsumo, lineasSinCosto, cobertura,
            productosTotal, productos.Count, paginaActual, paginasProductos, porSucursal, productos);
    }

    // Llamado desde el cobro final dentro de la misma transacción de venta.
    public async Task ConsumirPedidoAsync(Pedido pedido, IReadOnlyCollection<PedidoItem> items)
    {
        foreach (var item in items.Where(x => x.Estado != "ANULADO"))
        {
            var seleccion = PedidoService.LeerSeleccion(item.SeleccionJson);
            var componentes = new List<(int Producto, int? Variante, int? Opcion, int Cantidad)> { (item.ProductoId, seleccion.VarianteId, null, item.Cantidad) };
            var consumo = new Dictionary<int, decimal>();
            foreach (var mod in seleccion.Modificadores)
                foreach (var op in mod.Opciones) componentes.Add((item.ProductoId, null, op.OpcionId, item.Cantidad));
            foreach (var opcion in seleccion.Combo.SelectMany(g => g.Opciones))
            {
                componentes.Add((opcion.ProductoId, opcion.VarianteId, null, item.Cantidad * opcion.Cantidad));
                foreach (var mod in opcion.Modificadores)
                    foreach (var op in mod.Opciones) componentes.Add((opcion.ProductoId, null, op.OpcionId, item.Cantidad * opcion.Cantidad));
            }
            foreach (var comp in componentes)
            {
                var recetaQuery = _db.RecetasInsumo.AsNoTracking().Where(r => r.ProductoId == comp.Producto);
                // La receta base y su variante se aplican una vez al producto.
                // Un adicional solo consume su propia receta: nunca vuelve a sumar
                // la receta base por cada opción seleccionada.
                var receta = await (comp.Opcion.HasValue
                    ? recetaQuery.Where(r => r.VarianteId == null && r.OpcionId == comp.Opcion)
                    : recetaQuery.Where(r => r.OpcionId == null && (r.VarianteId == null || r.VarianteId == comp.Variante)))
                    .Select(r => new { r.InsumoId, r.CantidadBase }).ToListAsync();
                foreach (var r in receta)
                {
                    var cantidad = decimal.Round(r.CantidadBase * comp.Cantidad, 4, MidpointRounding.AwayFromZero);
                    consumo[r.InsumoId] = consumo.GetValueOrDefault(r.InsumoId) + cantidad;
                }
            }
            foreach (var (insumoId, cantidad) in consumo)
            {
                var key = $"venta-{item.PedidoItemId}-{insumoId}";
                if (await _db.MovimientosInsumo.AnyAsync(m => m.SucursalId == pedido.SucursalId && m.ClaveIdempotencia == key)) continue;
                var stock = await AsegurarStockBloqueadoAsync(insumoId);
                var insumo = await _db.Insumos.FirstAsync(i => i.InsumoId == insumoId);
                if (cantidad <= 0 || cantidad > MaxBase || stock.StockActualBase - cantidad is > MaxBase or < -MaxBase)
                    throw new ReglaNegocioException("La receta o el stock resultante supera el rango permitido.");
                var costoConfigurado = stock.CostoPromedioConfigurado;
                var costoTotal = costoConfigurado ? decimal.Round(cantidad * stock.CostoPromedioBase, 4, MidpointRounding.AwayFromZero) : (decimal?)null;
                if (costoTotal > MaxBase || (costoConfigurado && stock.CostoPromedioBase > MaxCostoBase))
                    throw new ReglaNegocioException("El costo valorizado del consumo supera el rango permitido.");
                stock.StockActualBase -= cantidad; stock.ActualizadoEn = DateTime.UtcNow;
                _db.MovimientosInsumo.Add(new MovimientoInsumo { TenantId = Tenant, SucursalId = pedido.SucursalId,
                    InsumoId = insumoId, Tipo = "CONSUMO_VENTA", CantidadDeltaBase = -cantidad,
                    CostoUnitarioBase = costoConfigurado ? stock.CostoPromedioBase : null, CostoTotal = costoTotal,
                    CostoConfigurado = costoConfigurado,
                    CantidadRegistrada = cantidad, UnidadRegistradaSnapshot = insumo.UnidadBase,
                    NombreInsumoSnapshot = insumo.Nombre, UnidadBaseSnapshot = insumo.UnidadBase,
                    Motivo = "Consumo automático por venta", Referencia = $"Pedido #{pedido.PedidoId}",
                    PedidoId = pedido.PedidoId, PedidoItemId = item.PedidoItemId, ClaveIdempotencia = key,
                    RegistradoPor = Usuario, RegistradoEn = DateTime.UtcNow });
            }
        }
    }

    private async Task<InsumoSucursal> AsegurarStockBloqueadoAsync(int insumoId)
    {
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO insumo_sucursal(tenant_id,sucursal_id,insumo_id) VALUES ({Tenant},{Sucursal},{insumoId})
            ON CONFLICT (tenant_id,sucursal_id,insumo_id) DO NOTHING
            """);
        var filas = await _db.InsumosSucursal.FromSqlInterpolated($"""
            SELECT * FROM insumo_sucursal WHERE tenant_id={Tenant} AND sucursal_id={Sucursal} AND insumo_id={insumoId} FOR UPDATE
            """).IgnoreQueryFilters().ToListAsync();
        return filas.Single();
    }

    private static string NormalizarClave(string? valor, string mensaje)
        => Guid.TryParse(valor, out var guid) ? guid.ToString("N") : throw new ReglaNegocioException(mensaje);

    private async Task<TrasladoDto> IdempotenciaTrasladoAsync(TrasladoInsumo traslado, string huella)
    {
        if (traslado.HuellaSolicitud != huella)
            throw new ReglaNegocioException("La clave del traslado ya se utilizó con una solicitud distinta.", 409);
        return await TrasladoMapAsync(traslado);
    }

    private async Task<TrasladoDto> TrasladoMapAsync(TrasladoInsumo traslado)
        => (await TrasladosMapAsync([traslado])).Single();

    private async Task<List<TrasladoDto>> TrasladosMapAsync(List<TrasladoInsumo> traslados)
    {
        if (traslados.Count == 0) return [];
        var ids = traslados.Select(x => x.TrasladoId).ToList();
        var sucursalIds = traslados.SelectMany(x => new[] { x.SucursalOrigenId, x.SucursalDestinoId }).Distinct().ToList();
        var sucursales = await _db.Sucursales.AsNoTracking().Where(x => sucursalIds.Contains(x.SucursalId))
            .ToDictionaryAsync(x => x.SucursalId, x => x.Nombre);
        var items = await _db.TrasladosInsumoItems.AsNoTracking().Where(x => ids.Contains(x.TrasladoId))
            .OrderBy(x => x.TrasladoItemId).ToListAsync();
        var recepciones = await _db.TrasladosInsumoRecepcionItems.AsNoTracking()
            .Where(x => ids.Contains(x.TrasladoId)).ToDictionaryAsync(x => x.TrasladoItemId);
        var itemsPorTraslado = items.GroupBy(x => x.TrasladoId).ToDictionary(g => g.Key, g => g.Select(x =>
        {
            recepciones.TryGetValue(x.TrasladoItemId, out var recibida);
            var cantidadRecibida = recibida?.CantidadRecibidaBase;
            return new TrasladoLineaDto(x.TrasladoItemId, x.InsumoId, x.NombreInsumoSnapshot, x.CantidadBase,
                cantidadRecibida, recibida == null ? 0 : x.CantidadBase - recibida.CantidadRecibidaBase,
                recibida?.MotivoDiferencia, x.UnidadBaseSnapshot, x.CostoUnitarioBase, x.CostoTotal);
        }).ToList());
        return traslados.Select(x => new TrasladoDto(x.TrasladoId, x.SucursalOrigenId,
            sucursales.GetValueOrDefault(x.SucursalOrigenId, "Sucursal"), x.SucursalDestinoId,
            sucursales.GetValueOrDefault(x.SucursalDestinoId, "Sucursal"), x.Estado, x.Motivo,
            x.RegistradoPor, x.RegistradoEn, x.RecibidoPor, x.RecibidoEn,
            itemsPorTraslado.GetValueOrDefault(x.TrasladoId, []))).ToList();
    }

    private static List<TrasladoRecepcionLineaDto> NormalizarRecepcion(
        List<TrasladoInsumoItem> items, List<TrasladoRecepcionLineaDto>? lineas)
    {
        if (lineas == null)
            return items.OrderBy(x => x.TrasladoItemId)
                .Select(x => new TrasladoRecepcionLineaDto(x.TrasladoItemId, x.CantidadBase)).ToList();
        if (lineas.Count != items.Count || lineas.Any(x => x is null || x.TrasladoItemId <= 0
            || x.CantidadRecibidaBase < 0 || decimal.Round(x.CantidadRecibidaBase, 4) != x.CantidadRecibidaBase)
            || lineas.Select(x => x.TrasladoItemId).Distinct().Count() != items.Count)
            throw new ReglaNegocioException("Indica una cantidad recibida válida para cada insumo del traslado.");
        var itemsPorId = items.ToDictionary(x => x.TrasladoItemId);
        return lineas.OrderBy(x => x.TrasladoItemId).Select(x =>
        {
            if (!itemsPorId.TryGetValue(x.TrasladoItemId, out var item) || x.CantidadRecibidaBase > item.CantidadBase)
                throw new ReglaNegocioException("La cantidad recibida no puede superar lo despachado.");
            var motivo = x.CantidadRecibidaBase < item.CantidadBase
                ? Texto.Opcional(x.MotivoDiferencia, "Motivo de diferencia", 300)
                : null;
            if (x.CantidadRecibidaBase < item.CantidadBase && string.IsNullOrWhiteSpace(motivo))
                throw new ReglaNegocioException("Registra el motivo de la diferencia en cada insumo faltante.");
            return new TrasladoRecepcionLineaDto(x.TrasladoItemId, x.CantidadRecibidaBase, motivo);
        }).ToList();
    }

    private async Task<T> EnTransaccionAsync<T>(Func<Task<T>> accion)
    {
        T? resultado = default;
        var estrategia = _db.Database.CreateExecutionStrategy();
        await estrategia.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync();
            resultado = await accion();
            await tx.CommitAsync();
        });
        return resultado!;
    }

    private static MovimientoInventarioFilaDto MovimientoMap(MovimientoInsumo x) => new(x.MovimientoInsumoId,
        x.InsumoId, x.NombreInsumoSnapshot, x.UnidadBaseSnapshot, x.Tipo, x.CantidadDeltaBase,
        x.CantidadRegistrada, x.UnidadRegistradaSnapshot, x.CostoConfigurado, x.CostoUnitarioBase,
        x.CostoTotal, x.Motivo, x.Referencia, x.PedidoId, x.RegistradoEn);
}
