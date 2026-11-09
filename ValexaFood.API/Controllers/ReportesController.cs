using Microsoft.AspNetCore.Mvc;
using ValexaFood.Application.Services;

namespace ValexaFood.API.Controllers;

[Route("api/reportes"), RequierePermiso("REPORTES_BASICOS")]
public class ReportesController : BaseController
{
    private readonly ReporteVentasService _svc;
    public ReportesController(ReporteVentasService svc) => _svc = svc;

    [HttpGet("ventas")]
    public Task<ReporteVentasDto> Ventas([FromQuery] DateOnly? fecha, [FromQuery] bool consolidado = false) =>
        _svc.VentasAsync(fecha, consolidado);
}
