using Microsoft.AspNetCore.Mvc;
using ValexaFood.Application.Services;

namespace ValexaFood.API.Controllers;

// Cada acción declara su permiso: el plan y el rol deciden, no el código.
[Route("api/carta")]
public class CartaController : BaseController
{
    private const string Gestionar = "CARTA_GESTIONAR";
    private const string Combos = "COMBOS_GESTIONAR";
    private readonly CartaService _svc;
    public CartaController(CartaService svc) => _svc = svc;

    // ─── Áreas de preparación ───
    [HttpGet("areas"), RequierePermiso(Gestionar)] public Task<List<AreaDto>> Areas() => _svc.AreasAsync();
    [HttpPost("areas"), RequierePermiso(Gestionar)] public Task<List<AreaDto>> CrearArea(AreaGuardarDto dto) => _svc.GuardarAreaAsync(null, dto);
    [HttpPut("areas/{id:int}"), RequierePermiso(Gestionar)] public Task<List<AreaDto>> EditarArea(int id, AreaGuardarDto dto) => _svc.GuardarAreaAsync(id, dto);
    [HttpPost("areas/{id:int}/estado"), RequierePermiso(Gestionar)] public Task<List<AreaDto>> EstadoArea(int id, EstadoDto dto) => _svc.EstadoAreaAsync(id, dto.Activo);
    [HttpDelete("areas/{id:int}"), RequierePermiso(Gestionar)] public Task<List<AreaDto>> EliminarArea(int id) => _svc.EliminarAreaAsync(id);
    [HttpPut("areas/orden"), RequierePermiso(Gestionar)] public Task<List<AreaDto>> OrdenarAreas(OrdenDto dto) => _svc.OrdenarAreasAsync(dto);

    // ─── Categorías ───
    [HttpGet("categorias"), RequierePermiso(Gestionar)] public Task<List<CategoriaDto>> Categorias() => _svc.CategoriasAsync();
    [HttpPost("categorias"), RequierePermiso(Gestionar)] public Task<List<CategoriaDto>> CrearCategoria(CategoriaGuardarDto dto) => _svc.GuardarCategoriaAsync(null, dto);
    [HttpPut("categorias/{id:int}"), RequierePermiso(Gestionar)] public Task<List<CategoriaDto>> EditarCategoria(int id, CategoriaGuardarDto dto) => _svc.GuardarCategoriaAsync(id, dto);
    [HttpPost("categorias/{id:int}/estado"), RequierePermiso(Gestionar)] public Task<List<CategoriaDto>> EstadoCategoria(int id, EstadoDto dto) => _svc.EstadoCategoriaAsync(id, dto.Activo);
    [HttpDelete("categorias/{id:int}"), RequierePermiso(Gestionar)] public Task<List<CategoriaDto>> EliminarCategoria(int id) => _svc.EliminarCategoriaAsync(id);
    [HttpPut("categorias/orden"), RequierePermiso(Gestionar)] public Task<List<CategoriaDto>> OrdenarCategorias(OrdenDto dto) => _svc.OrdenarCategoriasAsync(dto);

    // ─── Modificadores (función Combos y variantes) ───
    [HttpGet("modificadores"), RequierePermiso(Gestionar, Combos)] public Task<List<GrupoDto>> Grupos() => _svc.GruposAsync();
    [HttpPost("modificadores"), RequierePermiso(Combos)] public Task<List<GrupoDto>> CrearGrupo(GrupoGuardarDto dto) => _svc.GuardarGrupoAsync(null, dto);
    [HttpPut("modificadores/{id:int}"), RequierePermiso(Combos)] public Task<List<GrupoDto>> EditarGrupo(int id, GrupoGuardarDto dto) => _svc.GuardarGrupoAsync(id, dto);
    [HttpPost("modificadores/{id:int}/estado"), RequierePermiso(Combos)] public Task<List<GrupoDto>> EstadoGrupo(int id, EstadoDto dto) => _svc.EstadoGrupoAsync(id, dto.Activo);
    [HttpDelete("modificadores/{id:int}"), RequierePermiso(Combos)] public Task<List<GrupoDto>> EliminarGrupo(int id) => _svc.EliminarGrupoAsync(id);

    // ─── Platos ───
    [HttpGet("productos"), RequierePermiso(Gestionar)]
    public Task<List<ProductoListaDto>> Productos([FromQuery] int? categoriaId, [FromQuery] string? tipo, [FromQuery] string? buscar,
        [FromQuery] bool incluirInactivos = false) => _svc.ProductosAsync(categoriaId, tipo, buscar, incluirInactivos);
    [HttpGet("productos/{id:int}"), RequierePermiso(Gestionar)] public Task<ProductoDetalleDto> Producto(int id) => _svc.ProductoAsync(id);
    [HttpPost("productos"), RequierePermiso(Gestionar)] public Task<ProductoDetalleDto> CrearProducto(ProductoGuardarDto dto) => _svc.GuardarProductoAsync(null, dto);
    [HttpPut("productos/{id:int}"), RequierePermiso(Gestionar)] public Task<ProductoDetalleDto> EditarProducto(int id, ProductoGuardarDto dto) => _svc.GuardarProductoAsync(id, dto);
    [HttpPost("productos/{id:int}/estado"), RequierePermiso(Gestionar)] public Task<ProductoDetalleDto> EstadoProducto(int id, EstadoDto dto) => _svc.EstadoProductoAsync(id, dto.Activo);
    [HttpPost("productos/{id:int}/duplicar"), RequierePermiso(Gestionar)] public Task<ProductoDetalleDto> Duplicar(int id) => _svc.DuplicarAsync(id);
    [HttpPut("productos/orden"), RequierePermiso(Gestionar)] public Task<List<ProductoListaDto>> OrdenarProductos(OrdenDto dto) => _svc.OrdenarProductosAsync(dto);

    [HttpDelete("productos/{id:int}"), RequierePermiso(Gestionar)]
    public async Task<IActionResult> EliminarProducto(int id) { await _svc.EliminarProductoAsync(id); return NoContent(); }

    [HttpPost("productos/{id:int}/imagen"), RequierePermiso(Gestionar), RequestSizeLimit(3 * 1024 * 1024)]
    public Task<ProductoDetalleDto> Imagen(int id, IFormFile? archivo) => _svc.GuardarImagenAsync(id, archivo);
    [HttpDelete("productos/{id:int}/imagen"), RequierePermiso(Gestionar)] public Task<ProductoDetalleDto> QuitarImagen(int id) => _svc.QuitarImagenAsync(id);

    // ─── Sucursales, disponibilidad y carta de venta ───
    [HttpGet("sucursales"), RequierePermiso(Gestionar, "CARTA_VER", "CARTA_DISPONIBILIDAD")]
    public Task<List<SucursalSimpleDto>> Sucursales() => _svc.SucursalesAsync();

    [HttpGet("disponibilidad"), RequierePermiso("CARTA_DISPONIBILIDAD")]
    public Task<DisponibilidadDto> Disponibilidad([FromQuery] int? sucursalId) => _svc.DisponibilidadAsync(sucursalId);
    [HttpPost("disponibilidad"), RequierePermiso("CARTA_DISPONIBILIDAD")]
    public Task<DisponibilidadDto> MarcarAgotado(MarcarAgotadoDto dto) => _svc.MarcarAgotadoAsync(dto);

    [HttpGet("menu"), RequierePermiso("CARTA_VER")]
    public Task<MenuDto> Menu([FromQuery] int? sucursalId) => _svc.MenuAsync(sucursalId);
}
