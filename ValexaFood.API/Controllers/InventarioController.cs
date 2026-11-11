using Microsoft.AspNetCore.Mvc;
using ValexaFood.Application.Services;

namespace ValexaFood.API.Controllers;

[Route("api/inventario")]
public class InventarioController : BaseController
{
    private readonly InventarioService _svc;
    public InventarioController(InventarioService svc) => _svc = svc;

    // Auditoría de cuadre: devuelve solo los insumos cuya existencia no coincide con su kardex.
    [HttpGet("cuadre"), RequierePermiso("INVENTARIO_VER", "INVENTARIO_GESTIONAR")]
    public Task<CuadreInventarioDto> Cuadre() => _svc.CuadreAsync();

    // La lista de unidades con la que se arma el formulario del insumo. Es un catálogo
    // global: el factor de conversión ya no se escribe a mano en ningún lado.
    [HttpGet("unidades"), RequierePermiso("INVENTARIO_VER", "INVENTARIO_GESTIONAR")]
    public Task<List<UnidadMedidaDto>> Unidades() => _svc.UnidadesAsync();

    [HttpGet("insumos"), RequierePermiso("INVENTARIO_VER", "INVENTARIO_GESTIONAR")]
    public Task<List<InsumoDto>> Insumos([FromQuery] string? buscar) => _svc.ListarAsync(buscar);

    [HttpPost("insumos"), RequierePermiso("INVENTARIO_GESTIONAR")]
    public Task<InsumoDto> CrearInsumo(InsumoGuardarDto dto) => _svc.GuardarInsumoAsync(null, dto);

    [HttpPut("insumos/{id:int}"), RequierePermiso("INVENTARIO_GESTIONAR")]
    public Task<InsumoDto> EditarInsumo(int id, InsumoGuardarDto dto) => _svc.GuardarInsumoAsync(id, dto);

    [HttpPut("insumos/{id:int}/reposicion"), RequierePermiso("INVENTARIO_GESTIONAR")]
    public Task<InsumoDto> ConfigurarReposicion(int id, InsumoReposicionDto dto) => _svc.ConfigurarReposicionAsync(id, dto);

    [HttpGet("productos/{productoId:int}/receta"), RequierePermiso("INVENTARIO_VER", "INVENTARIO_GESTIONAR")]
    public Task<List<RecetaProductoDto>> Receta(int productoId) => _svc.RecetaAsync(productoId);

    [HttpPut("productos/{productoId:int}/receta"), RequierePermiso("INVENTARIO_GESTIONAR")]
    public Task<List<RecetaProductoDto>> GuardarReceta(int productoId, RecetaProductoGuardarDto dto) => _svc.GuardarRecetaAsync(productoId, dto);

    [HttpPost("insumos/{id:int}/movimientos"), RequierePermiso("INVENTARIO_GESTIONAR")]
    public Task<MovimientoInventarioFilaDto> Movimiento(int id, MovimientoInventarioDto dto) => _svc.RegistrarMovimientoAsync(id, dto);

    [HttpGet("movimientos"), RequierePermiso("INVENTARIO_VER", "INVENTARIO_GESTIONAR")]
    public Task<List<MovimientoInventarioFilaDto>> Movimientos([FromQuery] int? insumoId, [FromQuery] int limite = 100,
        [FromQuery] long? antesDe = null)
        => _svc.MovimientosAsync(insumoId, limite, antesDe);

    [HttpGet("proveedores"), RequierePermiso("INVENTARIO_VER", "INVENTARIO_GESTIONAR")]
    public Task<List<ProveedorInsumoDto>> Proveedores([FromQuery] string? buscar) => _svc.ProveedoresAsync(buscar);

    [HttpPost("proveedores"), RequierePermiso("INVENTARIO_GESTIONAR")]
    public Task<ProveedorInsumoDto> CrearProveedor(ProveedorInsumoGuardarDto dto) => _svc.GuardarProveedorAsync(dto);

    [HttpGet("compras"), RequierePermiso("INVENTARIO_VER", "INVENTARIO_GESTIONAR")]
    public Task<List<RecepcionCompraDto>> Compras([FromQuery] int limite = 100, [FromQuery] long? antesDe = null)
        => _svc.RecepcionesCompraAsync(limite, antesDe);

    [HttpPost("compras"), RequierePermiso("INVENTARIO_GESTIONAR")]
    public Task<RecepcionCompraDto> RegistrarCompra(RecepcionCompraGuardarDto dto) => _svc.RegistrarRecepcionCompraAsync(dto);

    // ── Almacenes: donde esta cada insumo dentro del local ──
    [HttpGet("almacenes"), RequierePermiso("INVENTARIO_VER", "INVENTARIO_GESTIONAR")]
    public Task<List<AlmacenDto>> Almacenes([FromQuery] bool incluirInactivos = false)
        => _svc.AlmacenesAsync(incluirInactivos);

    [HttpPost("almacenes"), RequierePermiso("INVENTARIO_GESTIONAR")]
    public Task<List<AlmacenDto>> CrearAlmacen(AlmacenGuardarDto dto) => _svc.GuardarAlmacenAsync(null, dto);

    [HttpPut("almacenes/{id:int}"), RequierePermiso("INVENTARIO_GESTIONAR")]
    public Task<List<AlmacenDto>> EditarAlmacen(int id, AlmacenGuardarDto dto) => _svc.GuardarAlmacenAsync(id, dto);

    [HttpGet("stock-almacenes"), RequierePermiso("INVENTARIO_VER", "INVENTARIO_GESTIONAR")]
    public Task<List<StockPorAlmacenDto>> StockPorAlmacen([FromQuery] int? insumoId = null, [FromQuery] bool soloConStock = false)
        => _svc.StockPorAlmacenAsync(insumoId, soloConStock);

    [HttpPost("reubicaciones"), RequierePermiso("INVENTARIO_GESTIONAR")]
    public Task<ReubicacionDto> Reubicar(ReubicacionGuardarDto dto) => _svc.ReubicarAsync(dto);

    [HttpGet("conteos"), RequierePermiso("INVENTARIO_VER", "INVENTARIO_GESTIONAR")]
    public Task<List<ConteoInventarioDto>> Conteos([FromQuery] int limite = 50, [FromQuery] long? antesDe = null)
        => _svc.ConteosAsync(limite, antesDe);

    [HttpPost("conteos"), RequierePermiso("INVENTARIO_GESTIONAR")]
    public Task<ConteoInventarioDto> RegistrarConteo(ConteoInventarioGuardarDto dto) => _svc.RegistrarConteoAsync(dto);

    [HttpGet("traslados"), RequierePermiso("INVENTARIO_VER", "INVENTARIO_GESTIONAR")]
    public Task<List<TrasladoDto>> Traslados([FromQuery] int limite = 100, [FromQuery] long? antesDe = null)
        => _svc.TrasladosAsync(limite, antesDe);

    [HttpPost("traslados"), RequierePermiso("INVENTARIO_GESTIONAR")]
    public Task<TrasladoDto> CrearTraslado(TrasladoGuardarDto dto) => _svc.RegistrarTrasladoAsync(dto);

    [HttpPost("traslados/{id:long}/recibir"), RequierePermiso("INVENTARIO_GESTIONAR")]
    public Task<TrasladoDto> RecibirTraslado(long id, TrasladoRecibirDto dto) => _svc.RecibirTrasladoAsync(id, dto);

    [HttpGet("analitica-costos"), RequierePermiso("INVENTARIO_VER", "INVENTARIO_GESTIONAR")]
    public Task<AnaliticaCostoRecetaDto> AnaliticaCostos([FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta,
        [FromQuery] bool consolidado = false, [FromQuery] int paginaProductos = 1,
        [FromQuery] bool priorizarPendientes = false)
        => _svc.AnaliticaCostosAsync(desde, hasta, consolidado, paginaProductos, priorizarPendientes);
}
