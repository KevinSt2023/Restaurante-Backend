using Microsoft.AspNetCore.Mvc;
using ValexaFood.Application.Services;

namespace ValexaFood.API.Controllers;

/// <summary>
/// Delivery · Fase 1. El flujo completo y las decisiones del dueño están en
/// docs/14-FLUJO-DE-ATENCION.md.
///
/// Quién hace qué:
///  · Administrador y Gerente definen las zonas y sus tarifas.
///  · Administrador, Cajero y Gerente toman el pedido —el que llega por
///    WhatsApp al número del restaurante— y registran al cliente.
///  · El repartidor ve lo suyo, sale, entrega y declara lo que cobró.
///  · El cajero registra ese cobro contra su caja y recién ahí lo marca rendido.
/// </summary>
[Route("api/delivery")]
public class DeliveryController : BaseController
{
    private readonly DeliveryService _svc;
    public DeliveryController(DeliveryService svc) => _svc = svc;

    // ─── Zonas ───
    [HttpGet("zonas"), RequierePermiso("DELIVERY_ZONAS", "DELIVERY_REGISTRAR")]
    public Task<List<ZonaRepartoDto>> Zonas([FromQuery] bool soloActivas = false) => _svc.ZonasAsync(soloActivas);

    [HttpPost("zonas"), RequierePermiso("DELIVERY_ZONAS")]
    public Task<List<ZonaRepartoDto>> CrearZona(ZonaRepartoGuardarDto dto) => _svc.GuardarZonaAsync(null, dto);

    [HttpPut("zonas/{zonaId:int}"), RequierePermiso("DELIVERY_ZONAS")]
    public Task<List<ZonaRepartoDto>> EditarZona(int zonaId, ZonaRepartoGuardarDto dto) => _svc.GuardarZonaAsync(zonaId, dto);

    [HttpPost("zonas/{zonaId:int}/estado"), RequierePermiso("DELIVERY_ZONAS")]
    public Task<List<ZonaRepartoDto>> EstadoZona(int zonaId, EstadoDto dto) => _svc.EstadoZonaAsync(zonaId, dto.Activo);

    // ─── Clientes ───
    [HttpGet("clientes"), RequierePermiso("CLIENTES_GESTIONAR")]
    public Task<List<ClienteDto>> Clientes([FromQuery] string? buscar, [FromQuery] int limite = 20) =>
        _svc.BuscarClientesAsync(buscar, limite);

    [HttpPost("clientes"), RequierePermiso("CLIENTES_GESTIONAR")]
    public Task<ClienteDto> CrearCliente(ClienteGuardarDto dto) => _svc.GuardarClienteAsync(null, dto);

    [HttpPut("clientes/{clienteId:int}"), RequierePermiso("CLIENTES_GESTIONAR")]
    public Task<ClienteDto> EditarCliente(int clienteId, ClienteGuardarDto dto) => _svc.GuardarClienteAsync(clienteId, dto);

    [HttpPost("clientes/{clienteId:int}/direcciones"), RequierePermiso("CLIENTES_GESTIONAR")]
    public Task<ClienteDto> CrearDireccion(int clienteId, DireccionGuardarDto dto) =>
        _svc.GuardarDireccionAsync(clienteId, null, dto);

    [HttpPut("clientes/{clienteId:int}/direcciones/{direccionId:int}"), RequierePermiso("CLIENTES_GESTIONAR")]
    public Task<ClienteDto> EditarDireccion(int clienteId, int direccionId, DireccionGuardarDto dto) =>
        _svc.GuardarDireccionAsync(clienteId, direccionId, dto);

    // ─── El pedido a domicilio ───
    [HttpPost("pedidos"), RequierePermiso("DELIVERY_REGISTRAR")]
    public Task<PedidoDetalleDto> CrearPedido(PedidoDeliveryNuevoDto dto) => _svc.CrearPedidoAsync(dto);

    // ─── Despacho ───
    [HttpGet("pendientes"), RequierePermiso("DELIVERY_ASIGNAR", "DELIVERY_REGISTRAR")]
    public Task<List<EnvioDto>> Pendientes() => _svc.PendientesAsync();

    [HttpPost("{envioId:long}/asignar"), RequierePermiso("DELIVERY_ASIGNAR")]
    public Task<EnvioDto> Asignar(long envioId, AsignarRepartidorDto dto) => _svc.AsignarAsync(envioId, dto);

    // ─── Ruta ───
    [HttpGet("mis-envios"), RequierePermiso("DELIVERY_REPARTIR")]
    public Task<List<EnvioDto>> MisEnvios() => _svc.MisEnviosAsync();

    /// <summary>Sale con uno o con varios a la vez, de uno o de varios clientes.</summary>
    [HttpPost("salir"), RequierePermiso("DELIVERY_REPARTIR")]
    public Task<List<EnvioDto>> Salir(List<long> envioIds) => _svc.SalirAsync(envioIds);

    [HttpPost("{envioId:long}/entregar"), RequierePermiso("DELIVERY_REPARTIR")]
    public Task<EnvioDto> Entregar(long envioId, EntregaDto dto) => _svc.EntregarAsync(envioId, dto);

    [HttpPost("{envioId:long}/no-entregado"), RequierePermiso("DELIVERY_REPARTIR", "DELIVERY_ASIGNAR")]
    public Task<EnvioDto> NoEntregado(long envioId, NoEntregaDto dto) => _svc.NoEntregadoAsync(envioId, dto);

    // ─── Rendición ───
    [HttpGet("por-rendir"), RequierePermiso("DELIVERY_RENDIR")]
    public Task<List<EnvioDto>> PorRendir() => _svc.PorRendirAsync();

    /// <summary>
    /// Marca que la rendición ocurrió. NO mueve dinero: el cobro se registra
    /// antes por /operacion/pedidos/{id}/cobrar contra la caja del cajero.
    /// </summary>
    [HttpPost("{envioId:long}/rendido"), RequierePermiso("DELIVERY_RENDIR")]
    public Task<EnvioDto> Rendido(long envioId) => _svc.MarcarRendidoAsync(envioId);
}
