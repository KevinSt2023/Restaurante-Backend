using Microsoft.AspNetCore.Mvc;
using ValexaFood.Application.Services;

namespace ValexaFood.API.Controllers;

[Route("api/operacion")]
public class OperacionController : BaseController
{
    private readonly PedidoService _svc;
    public OperacionController(PedidoService svc) => _svc = svc;

    [HttpGet("mesas"), RequierePermiso("SALON_VER")]
    public Task<List<MesaDto>> Mesas() => _svc.MesasAsync();
    // Armar el salón no es configurar la empresa: quien se da cuenta de que falta
    // una mesa es el que está atendiendo. Lo hacen mesero, gerente y dueño.
    [HttpPost("mesas"), RequierePermiso("MESAS_GESTIONAR")]
    public Task<MesaDto> CrearMesa(MesaGuardarDto dto) => _svc.CrearMesaAsync(dto);
    [HttpPost("mesas/{id:int}/estado"), RequierePermiso("MESAS_GESTIONAR")]
    public Task<List<MesaDto>> EstadoMesa(int id, EstadoDto dto) => _svc.EstadoMesaAsync(id, dto.Activo);

    /// <summary>Lo que el negocio cobra por empacar lo que se llevan.</summary>
    [HttpGet("empaque"), RequierePermiso("PEDIDO_TOMAR", "CONFIGURACION")]
    public Task<EmpaqueConfigDto> Empaque() => _svc.EmpaqueAsync();

    [HttpPut("empaque"), RequierePermiso("CONFIGURACION")]
    public Task<EmpaqueConfigDto> GuardarEmpaque(EmpaqueConfigDto dto) => _svc.GuardarEmpaqueAsync(dto);

    /// <summary>Deja la mesa en limpieza tras cobrarla, o la vuelve a liberar.</summary>
    [HttpPost("mesas/{id:int}/limpieza"), RequierePermiso("MESA_LIMPIEZA")]
    public Task<List<MesaDto>> Limpieza(int id, EstadoDto dto) => _svc.MarcarLimpiezaAsync(id, dto.Activo);

    [HttpPost("pedidos"), RequierePermiso("PEDIDO_TOMAR")]
    public Task<PedidoDetalleDto> CrearPedido(PedidoNuevoDto dto) => _svc.CrearPedidoAsync(dto);
    [HttpPost("pedidos/{id:long}/items"), RequierePermiso("PEDIDO_TOMAR")]
    public Task<PedidoDetalleDto> AgregarItem(long id, PedidoItemNuevoDto dto) => _svc.AgregarItemAsync(id, dto);
    [HttpGet("pedidos/{id:long}"), RequierePermiso("SALON_VER")]
    public Task<PedidoDetalleDto> Detalle(long id) => _svc.DetalleAsync(id);
    [HttpPost("pedidos/{id:long}/enviar"), RequierePermiso("PEDIDO_TOMAR")]
    public Task<PedidoDetalleDto> Enviar(long id) => _svc.EnviarACocinaAsync(id);
    // Llevar el plato a la mesa es trabajo de salón, no de cocina. Antes exigía
    // COCINA_MARCAR y el mesero recibía 403 por entregar lo que él mismo sirve.
    [HttpPost("pedidos/{id:long}/entregar"), RequierePermiso("PEDIDO_ENTREGAR")]
    public Task<PedidoDetalleDto> Entregar(long id, EntregaMesaDto dto) => _svc.EntregarAsync(id, dto);
    [HttpPost("pedidos/{id:long}/anular-borrador"), RequierePermiso("PEDIDO_TOMAR")]
    public Task<PedidoDetalleDto> AnularBorrador(long id) => _svc.AnularBorradorAsync(id);
    [HttpPost("pedidos/{id:long}/cobrar"), RequierePermiso("COBRAR")]
    public Task<PagoDto> Cobrar(long id, PagoNuevoDto dto) => _svc.CobrarAsync(id, dto);
    [HttpPost("pedidos/{id:long}/descuentos"), RequierePermiso("DESCUENTO_APLICAR")]
    public Task<AjusteVentaDto> Descuento(long id, AjusteVentaNuevoDto dto) => _svc.AplicarDescuentoAsync(id, dto);
    // El servicio decide: el mesero corrige su borrador, la línea ya mandada a
    // cocina necesita PEDIDO_ANULAR_ITEM.
    [HttpPost("pedidos/{id:long}/items/{itemId:long}/anulacion"), RequierePermiso("PEDIDO_ANULAR_ITEM", "PEDIDO_TOMAR")]
    public Task<AjusteVentaDto> AnularItem(long id, long itemId, AjusteVentaNuevoDto dto) => _svc.AnularItemAsync(id, itemId, dto);
    [HttpPost("pagos/{id:long}/devoluciones"), RequierePermiso("PAGO_DEVOLVER")]
    public Task<AjusteVentaDto> Devolver(long id, AjusteVentaNuevoDto dto) => _svc.DevolverPagoAsync(id, dto);

    [HttpGet("cocina"), RequierePermiso("COCINA_VER")]
    public Task<List<ComandaCocinaDto>> Cocina() => _svc.CocinaAsync();
    [HttpPost("comandas/{id:long}/estado"), RequierePermiso("COCINA_MARCAR")]
    public Task<List<ComandaCocinaDto>> EstadoComanda(long id, EstadoComandaDto dto) => _svc.CambiarEstadoComandaAsync(id, dto);
    [HttpGet("impresiones"), RequierePermiso("COCINA_VER")]
    public Task<List<TrabajoImpresionDto>> ColaImpresion() => _svc.ColaImpresionAsync();
    [HttpPost("impresiones/{id:long}/solicitar"), RequierePermiso("COCINA_MARCAR")]
    public Task<TrabajoImpresionDto> SolicitarImpresion(long id, SolicitarImpresionDto dto) =>
        _svc.SolicitarImpresionAsync(id, dto.ClaveIdempotencia);
    [HttpPost("impresiones/{id:long}/resultado"), RequierePermiso("COCINA_MARCAR")]
    public Task<TrabajoImpresionDto> ResultadoImpresion(long id, ResultadoImpresionNuevoDto dto) =>
        _svc.RegistrarResultadoImpresionAsync(id, dto);
    [HttpGet("impresiones/{id:long}/eventos"), RequierePermiso("COCINA_VER")]
    public Task<List<TrabajoImpresionEventoDto>> EventosImpresion(long id) => _svc.HistorialImpresionAsync(id);
}
