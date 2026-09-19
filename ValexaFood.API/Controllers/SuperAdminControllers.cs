using Microsoft.AspNetCore.Mvc;
using ValexaFood.Application.Services;
using ValexaFood.Domain.Entities;

namespace ValexaFood.API.Controllers;

// Todo lo de este archivo es exclusivo del SuperAdmin.

[Route("api/sa/empresas"), SoloRoles(RolIds.SuperAdmin)]
public class EmpresasController : BaseController
{
    private readonly EmpresaService _svc;
    private readonly SuscripcionService _susc;
    private readonly SaludService _salud;
    private readonly BitacoraService _bitacora;

    public EmpresasController(EmpresaService svc, SuscripcionService susc, SaludService salud, BitacoraService bitacora)
    {
        _svc = svc;
        _susc = susc;
        _salud = salud;
        _bitacora = bitacora;
    }

    [HttpGet] public Task<List<EmpresaListaDto>> Listar() => _svc.ListarAsync();
    [HttpGet("{id:int}")] public Task<EmpresaDetalleDto> Detalle(int id) => _svc.DetalleAsync(id);
    [HttpPost] public Task<EmpresaCreadaDto> Crear(EmpresaCrearDto dto) => _svc.CrearAsync(dto);
    [HttpPut("{id:int}")] public Task<EmpresaDetalleDto> Actualizar(int id, EmpresaEditarDto dto) => _svc.ActualizarAsync(id, dto);

    [HttpPut("{id:int}/notas")]
    public async Task<IActionResult> Notas(int id, NotasDto dto) { await _svc.GuardarNotasAsync(id, dto); return NoContent(); }

    [HttpPost("{id:int}/plan")] public Task<EmpresaDetalleDto> CambiarPlan(int id, CambiarPlanDto dto) => _svc.CambiarPlanAsync(id, dto);
    [HttpPost("{id:int}/suspender")] public Task<EmpresaDetalleDto> Suspender(int id, MotivoDto dto) => _svc.SuspenderAsync(id, dto);
    [HttpPost("{id:int}/reactivar")] public Task<EmpresaDetalleDto> Reactivar(int id) => _svc.ReactivarAsync(id);

    [HttpPost("{id:int}/cerrar-sesiones")]
    public async Task<object> CerrarSesiones(int id) => new { usuarios = await _svc.CerrarSesionesEmpresaAsync(id) };

    [HttpGet("{id:int}/accesos")] public Task<List<AccesoDto>> Accesos(int id, [FromQuery] int limite = 100) => _svc.AccesosAsync(id, limite);
    [HttpGet("{id:int}/movimientos")] public Task<List<MovimientoDto>> Movimientos(int id) => _susc.ListarAsync(id, null, 200);
    [HttpGet("{id:int}/salud")] public async Task<SaludEmpresaDto?> Salud(int id) => (await _salud.ListarAsync(id)).FirstOrDefault();

    [HttpGet("{id:int}/bitacora")]
    public Task<BitacoraPaginaDto> Bitacora(int id, [FromQuery] int pagina = 1, [FromQuery] int tamano = 30) =>
        _bitacora.ListarAsync(id, null, null, null, null, false, pagina, tamano);

    // ─── Sucursales ───
    [HttpPost("{id:int}/sucursales")]
    public Task<EmpresaDetalleDto> CrearSucursal(int id, SucursalGuardarDto dto) => _svc.CrearSucursalAsync(id, dto);
    [HttpPut("{id:int}/sucursales/{sid:int}")]
    public Task<EmpresaDetalleDto> EditarSucursal(int id, int sid, SucursalGuardarDto dto) => _svc.EditarSucursalAsync(id, sid, dto);
    [HttpPost("{id:int}/sucursales/{sid:int}/estado")]
    public Task<EmpresaDetalleDto> EstadoSucursal(int id, int sid, EstadoDto dto) => _svc.EstadoSucursalAsync(id, sid, dto);
    [HttpPost("{id:int}/sucursales/{sid:int}/principal")]
    public Task<EmpresaDetalleDto> Principal(int id, int sid) => _svc.MarcarPrincipalAsync(id, sid);

    // ─── Usuarios ───
    [HttpPost("{id:int}/usuarios")]
    public async Task<object> CrearUsuario(int id, UsuarioGuardarDto dto) =>
        new { temporal = await _svc.CrearUsuarioAsync(id, dto), empresa = await _svc.DetalleAsync(id) };
    [HttpPut("{id:int}/usuarios/{uid:int}")]
    public async Task<EmpresaDetalleDto> EditarUsuario(int id, int uid, UsuarioGuardarDto dto)
    { await _svc.EditarUsuarioAsync(id, uid, dto); return await _svc.DetalleAsync(id); }
    [HttpPost("{id:int}/usuarios/{uid:int}/estado")]
    public async Task<EmpresaDetalleDto> EstadoUsuario(int id, int uid, EstadoDto dto)
    { await _svc.EstadoUsuarioAsync(id, uid, dto); return await _svc.DetalleAsync(id); }
    [HttpPost("{id:int}/usuarios/{uid:int}/reset-password")]
    public Task<PasswordTemporalDto> ResetPassword(int id, int uid) => _svc.ResetPasswordAsync(id, uid);
    [HttpPost("{id:int}/usuarios/{uid:int}/desbloquear")]
    public async Task<EmpresaDetalleDto> Desbloquear(int id, int uid)
    { await _svc.DesbloquearAsync(id, uid); return await _svc.DetalleAsync(id); }
    [HttpPost("{id:int}/usuarios/{uid:int}/cerrar-sesiones")]
    public async Task<IActionResult> CerrarSesionesUsuario(int id, int uid)
    { await _svc.CerrarSesionesUsuarioAsync(id, uid); return NoContent(); }
}

[Route("api/sa/planes"), SoloRoles(RolIds.SuperAdmin)]
public class PlanesController : BaseController
{
    private readonly PlanService _svc;
    public PlanesController(PlanService svc) => _svc = svc;

    [HttpGet] public Task<List<PlanDto>> Listar() => _svc.ListarAsync();
    [HttpGet("{id:int}")] public Task<PlanDto> Obtener(int id) => _svc.ObtenerAsync(id);
    [HttpPost] public Task<PlanDto> Crear(PlanGuardarDto dto) => _svc.CrearAsync(dto);
    [HttpPut("{id:int}")] public Task<PlanDto> Actualizar(int id, PlanGuardarDto dto) => _svc.ActualizarAsync(id, dto);
    [HttpDelete("{id:int}")] public async Task<IActionResult> Eliminar(int id) { await _svc.EliminarAsync(id); return NoContent(); }
}

[Route("api/sa/suscripciones"), SoloRoles(RolIds.SuperAdmin)]
public class SuscripcionesController : BaseController
{
    private readonly SuscripcionService _svc;
    public SuscripcionesController(SuscripcionService svc) => _svc = svc;

    [HttpGet("tablero")] public Task<TableroDto> Tablero() => _svc.TableroAsync();
    [HttpGet("cobranza")] public Task<List<CobranzaDto>> Cobranza() => _svc.CobranzaAsync();
    [HttpGet("movimientos")] public Task<List<MovimientoDto>> Movimientos([FromQuery] int limite = 50) => _svc.ListarAsync(null, null, limite);
    [HttpGet("sugerencia/{tenantId:int}")] public Task<SugerenciaCobroDto> Sugerencia(int tenantId, [FromQuery] int meses = 1) => _svc.SugerirAsync(tenantId, meses);
    [HttpPost("{tenantId:int}")] public Task<ResultadoMovimientoDto> Registrar(int tenantId, RegistrarMovimientoDto dto) => _svc.RegistrarAsync(tenantId, dto);
    [HttpPost("movimientos/{id:int}/anular")] public Task<MovimientoDto> Anular(int id, AnularMovimientoDto dto) => _svc.AnularAsync(id, dto);
}

[Route("api/sa/salud"), SoloRoles(RolIds.SuperAdmin)]
public class SaludController : BaseController
{
    private readonly SaludService _svc;
    public SaludController(SaludService svc) => _svc = svc;
    [HttpGet] public Task<List<SaludEmpresaDto>> Listar() => _svc.ListarAsync();
}

[Route("api/sa/bitacora"), SoloRoles(RolIds.SuperAdmin)]
public class BitacoraController : BaseController
{
    private readonly BitacoraService _svc;
    public BitacoraController(BitacoraService svc) => _svc = svc;

    [HttpGet]
    public Task<BitacoraPaginaDto> Listar([FromQuery] int? tenantId, [FromQuery] string? accion, [FromQuery] string? buscar,
        [FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta, [FromQuery] bool soloSaas = false,
        [FromQuery] int pagina = 1, [FromQuery] int tamano = 50) =>
        _svc.ListarAsync(tenantId, accion, buscar, desde, hasta, soloSaas, pagina, tamano);

    [HttpGet("acciones")] public Task<List<string>> Acciones() => _svc.AccionesAsync();
}

[Route("api/sa/catalogos"), SoloRoles(RolIds.SuperAdmin)]
public class CatalogosController : BaseController
{
    private readonly CatalogoService _svc;
    public CatalogosController(CatalogoService svc) => _svc = svc;

    [HttpGet("roles")] public Task<List<RolDto>> Roles() => _svc.RolesAsync();
    [HttpGet("funciones")] public Task<List<FuncionDto>> Funciones() => _svc.FuncionesAsync();
    [HttpGet("permisos")] public Task<MatrizPermisosDto> Permisos() => _svc.MatrizAsync();
    [HttpPut("permisos/{rolId:int}")] public Task<MatrizPermisosDto> GuardarPermisos(int rolId, RolPermisosGuardarDto dto) => _svc.GuardarPermisosRolAsync(rolId, dto);
    [HttpGet("configuracion")] public Task<ConfiguracionSaasDto> Configuracion() => _svc.ConfiguracionAsync();
    [HttpPut("configuracion")] public Task<ConfiguracionSaasDto> GuardarConfiguracion(ConfiguracionSaasDto dto) => _svc.GuardarConfiguracionAsync(dto);
    [HttpGet("tipos-servicio")] public string[] TiposServicio() => ValexaFood.Application.Services.TiposServicio.Todos;
}
