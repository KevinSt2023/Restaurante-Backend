using Microsoft.AspNetCore.Mvc;
using ValexaFood.Application.Services;

namespace ValexaFood.API.Controllers;

[Route("api/caja")]
public class CajaController : BaseController
{
    private readonly CajaService _svc;
    public CajaController(CajaService svc) => _svc = svc;

    [HttpGet, RequierePermiso("CAJA_ABRIR_CERRAR")]
    public Task<List<CajaListaDto>> Listar() => _svc.ListarAsync();

    [HttpGet("abiertas-cobro"), RequierePermiso("COBRAR")]
    public Task<List<CajaCobroDisponibleDto>> SesionesAbiertasCobro() => _svc.SesionesAbiertasCobroAsync();

    [HttpGet("limites-ajuste"), RequierePermiso("CONFIGURACION")]
    public Task<AjusteVentaLimitesDto> LimitesAjuste() => _svc.LimitesAjusteAsync();
    [HttpPut("limites-ajuste"), RequierePermiso("CONFIGURACION")]
    public Task<AjusteVentaLimitesDto> GuardarLimitesAjuste(AjusteVentaLimitesDto dto) => _svc.GuardarLimitesAjusteAsync(dto);

    [HttpPost, RequierePermiso("CONFIGURACION")]
    public Task<CajaListaDto> Crear(CajaGuardarDto dto) => _svc.CrearAsync(dto);

    [HttpPost("{cajaId:int}/estado"), RequierePermiso("CONFIGURACION")]
    public Task<List<CajaListaDto>> Estado(int cajaId, CajaEstadoDto dto) => _svc.EstadoAsync(cajaId, dto);

    [HttpPost("{cajaId:int}/sesiones"), RequierePermiso("CAJA_ABRIR_CERRAR")]
    public Task<SesionCajaResumenDto> Abrir(int cajaId, CajaAbrirDto dto) => _svc.AbrirAsync(cajaId, dto);

    [HttpPost("sesiones/{sesionCajaId:long}/movimientos"), RequierePermiso("CAJA_EGRESOS")]
    public Task<CajaMovimientoDto> Movimiento(long sesionCajaId, CajaMovimientoGuardarDto dto) =>
        _svc.MovimientoAsync(sesionCajaId, dto);

    [HttpGet("sesiones/{sesionCajaId:long}/movimientos"), RequierePermiso("CAJA_EGRESOS")]
    public Task<List<CajaMovimientoDto>> Movimientos(long sesionCajaId) => _svc.MovimientosAsync(sesionCajaId);

    // Arqueo en vivo: el cajero ve el cuadre ANTES de contar y cerrar. Solo lectura.
    [HttpGet("sesiones/{sesionCajaId:long}/arqueo"), RequierePermiso("CAJA_ARQUEO", "CAJA_ABRIR_CERRAR")]
    public Task<CajaArqueoDto> Arqueo(long sesionCajaId) => _svc.ArqueoAsync(sesionCajaId);

    [HttpPut("sesiones/{sesionCajaId:long}/cerrar"), RequierePermiso("CAJA_ABRIR_CERRAR")]
    public Task<CajaCierreResultadoDto> Cerrar(long sesionCajaId, CajaCerrarDto dto) =>
        _svc.CerrarAsync(sesionCajaId, dto);
}
