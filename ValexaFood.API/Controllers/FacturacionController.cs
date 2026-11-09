using Microsoft.AspNetCore.Mvc;
using ValexaFood.Application.Common;
using ValexaFood.Application.Services;

namespace ValexaFood.API.Controllers;

[Route("api/facturacion")]
public class FacturacionController : BaseController
{
    private readonly FacturacionService _svc;
    public FacturacionController(FacturacionService svc)=>_svc=svc;

    [HttpGet("comprobantes"),RequierePermiso("NOTA_VENTA_EMITIR","FACTURA_EMITIR")]
    public Task<List<ComprobanteDto>> Listar([FromQuery]DateOnly? desde,[FromQuery]DateOnly? hasta)=>_svc.ListarAsync(desde,hasta);

    [HttpGet("pendientes"),RequierePermiso("NOTA_VENTA_EMITIR","FACTURA_EMITIR")]
    public Task<List<VentaPendienteDto>> Pendientes()=>_svc.PendientesAsync();

    [HttpGet("conciliacion"),RequierePermiso("NOTA_VENTA_EMITIR","FACTURA_EMITIR")]
    public Task<List<ComprobanteDto>> Conciliacion()=>_svc.ConciliacionAsync();

    // Auditoría de cuadre: devuelve solo los comprobantes que NO calzan con su venta.
    [HttpGet("cuadre"),RequierePermiso("NOTA_VENTA_EMITIR","FACTURA_EMITIR")]
    public Task<CuadreFiscalDto> Cuadre([FromQuery]DateOnly? desde,[FromQuery]DateOnly? hasta)=>_svc.CuadreAsync(desde,hasta);

    [HttpGet("comprobantes/{id:long}"),RequierePermiso("NOTA_VENTA_EMITIR","FACTURA_EMITIR")]
    public Task<ComprobanteDto> Obtener(long id)=>_svc.ObtenerAsync(id);

    [HttpPost("comprobantes/{id:long}/reenviar"),RequierePermiso("CONFIGURACION")]
    public Task<ComprobanteDto> Reenviar(long id,ReenviarCpeDto dto)=>_svc.ReenviarTrasConciliacionAsync(id,dto);

    [HttpPost("pedidos/{pedidoId:long}/nota-venta"),RequierePermiso("NOTA_VENTA_EMITIR")]
    public Task<ComprobanteDto> NotaVenta(long pedidoId,EmitirComprobanteDto dto)=>_svc.EmitirAsync(pedidoId,dto with{Tipo="NOTA_VENTA"});

    [HttpPost("pedidos/{pedidoId:long}/electronico"),RequierePermiso("FACTURA_EMITIR")]
    public Task<ComprobanteDto> Electronico(long pedidoId,EmitirComprobanteDto dto)
    {
        if(dto.Tipo is not ("BOLETA" or "FACTURA")) throw new ReglaNegocioException("El tipo debe ser BOLETA o FACTURA.");
        return _svc.EmitirAsync(pedidoId,dto);
    }

    [HttpGet("configuracion"),RequierePermiso("CONFIGURACION")]
    public Task<ConfigFacturacionDto> Configuracion()=>_svc.ConfiguracionAsync();

    [HttpPut("configuracion"),RequierePermiso("CONFIGURACION")]
    public Task<ConfigFacturacionDto> GuardarConfiguracion(ConfigFacturacionDto dto)=>_svc.GuardarConfiguracionAsync(dto);
}
