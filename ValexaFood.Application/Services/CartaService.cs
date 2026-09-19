using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ValexaFood.Application.Common;
using ValexaFood.Domain.Entities;
using ValexaFood.Infrastructure.Context;
using ValexaFood.Infrastructure.Sesion;

namespace ValexaFood.Application.Services;

// ─── DTOs ────────────────────────────────────────────────────────────────────
public record AreaDto(int AreaId, string Nombre, string Color, bool ImprimeComanda, int Orden, bool Activo, int Productos);
public record AreaGuardarDto(string? Nombre, string? Color, bool ImprimeComanda);
public record CategoriaDto(int CategoriaId, string Nombre, string? Descripcion, string Color, int Orden, bool Activo, int Productos);
public record CategoriaGuardarDto(string? Nombre, string? Descripcion, string? Color);
public record OrdenDto(List<int>? Ids);

public record OpcionDto(int OpcionId, string Nombre, decimal PrecioExtra, int Orden, bool Activo);
public record GrupoDto(int GrupoId, string Nombre, int Minimo, int Maximo, int Orden, bool Activo, List<OpcionDto> Opciones, int Productos);
public record OpcionGuardarDto(int? OpcionId, string? Nombre, decimal PrecioExtra, bool Activo = true);
public record GrupoGuardarDto(string? Nombre, int Minimo, int Maximo, List<OpcionGuardarDto>? Opciones);

public record ProductoListaDto(int ProductoId, string Nombre, string? Codigo, string Tipo, int CategoriaId, string Categoria,
    string CategoriaColor, int? AreaId, string? Area, decimal Precio, decimal? PrecioDesde, string? Imagen, bool Activo,
    int Variantes, int Grupos, int Sucursales, string[] Etiquetas, int Orden);
public record VarianteDto(int VarianteId, string Nombre, decimal Precio, int Orden, bool Activo);
public record ComboOpcionDto(int ProductoId, string Producto, decimal PrecioExtra, bool Activo);
public record ComboGrupoDto(int ComboGrupoId, string Nombre, int Cantidad, List<ComboOpcionDto> Opciones);
public record ProductoSucursalDto(int SucursalId, string Sucursal, bool Activa, decimal? Precio, bool AgotadoHoy);
public record ProductoDetalleDto(int ProductoId, int CategoriaId, int? AreaId, string Tipo, string? Codigo, string Nombre,
    string? Descripcion, decimal Precio, string AfectacionIgv, int? TiempoPreparacionMin, string[] Etiquetas, string? Imagen,
    bool Activo, List<VarianteDto> Variantes, List<int> GrupoIds, List<ComboGrupoDto> ComboGrupos,
    List<ProductoSucursalDto> Sucursales, DateTime UpdatedAt);

public record VarianteGuardarDto(int? VarianteId, string? Nombre, decimal Precio, bool Activo = true);
public record ComboOpcionGuardarDto(int ProductoId, decimal PrecioExtra);
public record ComboGrupoGuardarDto(string? Nombre, int Cantidad, List<ComboOpcionGuardarDto>? Opciones);
public record SucursalPrecioDto(int SucursalId, bool Activa, decimal? Precio);
public record ProductoGuardarDto(int CategoriaId, int? AreaId, string? Tipo, string? Codigo, string? Nombre, string? Descripcion,
    decimal Precio, string? AfectacionIgv, int? TiempoPreparacionMin, string[]? Etiquetas,
    List<VarianteGuardarDto>? Variantes, List<int>? GrupoIds, List<ComboGrupoGuardarDto>? ComboGrupos,
    List<SucursalPrecioDto>? Sucursales);

public record SucursalSimpleDto(int SucursalId, string Nombre, bool EsPrincipal);
public record DisponibilidadItemDto(int ProductoId, string Nombre, string Tipo, int CategoriaId, string Categoria, string CategoriaColor, string? Imagen, bool Agotado);
public record DisponibilidadDto(int SucursalId, string Sucursal, DateOnly Fecha, List<DisponibilidadItemDto> Productos);
public record MarcarAgotadoDto(int SucursalId, int ProductoId, bool Agotado);

public record MenuOpcionDto(int OpcionId, string Nombre, decimal PrecioExtra);
public record MenuGrupoDto(int GrupoId, string Nombre, int Minimo, int Maximo, List<MenuOpcionDto> Opciones);
public record MenuVarianteDto(int VarianteId, string Nombre, decimal Precio);
public record MenuComboOpcionDto(int ProductoId, string Nombre, decimal PrecioExtra, bool Agotado);
public record MenuComboGrupoDto(int ComboGrupoId, string Nombre, int Cantidad, List<MenuComboOpcionDto> Opciones);
public record MenuProductoDto(int ProductoId, string Nombre, string? Codigo, string Tipo, string? Descripcion, decimal Precio,
    string? Imagen, string[] Etiquetas, int? AreaId, bool Agotado, List<MenuVarianteDto> Variantes, List<MenuGrupoDto> Grupos,
    List<MenuComboGrupoDto> Combo);
public record MenuCategoriaDto(int CategoriaId, string Nombre, string Color, List<MenuProductoDto> Productos);
public record MenuDto(int SucursalId, string Sucursal, bool CombosYVariantes, List<MenuCategoriaDto> Categorias);

// ─────────────────────────────────────────────────────────────────────────────
// Carta, completa. Los planes menores apagan partes: sin la función
// COMBOS_VARIANTES no se editan variantes, modificadores ni combos (y lo que ya
// existía NO se borra: vuelve a aparecer si la empresa sube de plan).
// ─────────────────────────────────────────────────────────────────────────────
public class CartaService
{
    public const string FuncionCombos = "COMBOS_VARIANTES";
    private static readonly string[] Afectaciones = ["GRAVADO", "EXONERADO", "INAFECTO"];
    private static readonly Regex Color = new("^#[0-9A-Fa-f]{6}$");
    private const long MaxImagen = 2 * 1024 * 1024;

    private readonly ValexaFoodContext _db;
    private readonly ISesionActual _sesion;
    private readonly PermisosEfectivos _permisos;
    private readonly BitacoraService _bitacora;
    private readonly string _rutaArchivos;
    // Renombres pendientes: se aplican en la 2.ª fase del guardado (ver GuardarPorPartesAsync).
    private readonly List<Action> _nombresFinales = [];

    public CartaService(ValexaFoodContext db, ISesionActual sesion, PermisosEfectivos permisos, BitacoraService bitacora, IConfiguration config)
    {
        _db = db;
        _sesion = sesion;
        _permisos = permisos;
        _bitacora = bitacora;
        _rutaArchivos = config["Archivos:Ruta"] ?? Path.Combine(AppContext.BaseDirectory, "archivos");
    }

    private int Tenant => _sesion.TenantId ?? throw new ReglaNegocioException("Sesión sin empresa.", 403);
    private async Task<PermisosEfectivos.Resultado> MisPermisosAsync() => await _permisos.DeSesionAsync(_sesion.RolId, _sesion.TenantId);
    private async Task<bool> PuedeCombosAsync() => (await MisPermisosAsync()).Permisos.Contains("COMBOS_GESTIONAR");

    // ═════════════════════════════════════════════════════════════════════════
    // ÁREAS DE PREPARACIÓN
    // ═════════════════════════════════════════════════════════════════════════
    public async Task<List<AreaDto>> AreasAsync()
    {
        var conteo = await _db.Productos.Where(p => p.AreaId != null).GroupBy(p => p.AreaId)
            .Select(g => new { AreaId = g.Key!.Value, N = g.Count() }).ToListAsync();
        return (await _db.Areas.AsNoTracking().OrderBy(a => a.Orden).ThenBy(a => a.Nombre).ToListAsync())
            .Select(a => new AreaDto(a.AreaId, a.Nombre, a.Color, a.ImprimeComanda, a.Orden, a.Activo,
                conteo.FirstOrDefault(c => c.AreaId == a.AreaId)?.N ?? 0)).ToList();
    }

    public async Task<List<AreaDto>> GuardarAreaAsync(int? id, AreaGuardarDto dto)
    {
        var nombre = Texto.Requerido(dto.Nombre, "Nombre del área", 60, 2);
        var color = ValidarColor(dto.Color, "#801D31");
        if (await _db.Areas.AnyAsync(a => a.AreaId != id && a.Nombre.ToLower() == nombre.ToLower()))
            throw new ReglaNegocioException($"Ya existe un área llamada {nombre}.");
        AreaPreparacion a;
        if (id == null)
        {
            a = new AreaPreparacion { CreatedAt = DateTime.UtcNow, Orden = (await _db.Areas.MaxAsync(x => (int?)x.Orden) ?? 0) + 1 };
            _db.Areas.Add(a);
        }
        else a = await _db.Areas.FirstOrDefaultAsync(x => x.AreaId == id) ?? throw ReglaNegocioException.NoEncontrado("Área");
        a.Nombre = nombre;
        a.Color = color;
        a.ImprimeComanda = dto.ImprimeComanda;
        await _db.SaveChangesAsync();
        return await AreasAsync();
    }

    public async Task<List<AreaDto>> EstadoAreaAsync(int id, bool activo)
    {
        var a = await _db.Areas.FirstOrDefaultAsync(x => x.AreaId == id) ?? throw ReglaNegocioException.NoEncontrado("Área");
        if (!activo && await _db.Productos.AnyAsync(p => p.AreaId == id && p.Activo))
            throw new ReglaNegocioException($"El área {a.Nombre} tiene platos activos: muévelos a otra área antes de desactivarla.");
        a.Activo = activo;
        await _db.SaveChangesAsync();
        return await AreasAsync();
    }

    public async Task<List<AreaDto>> EliminarAreaAsync(int id)
    {
        var a = await _db.Areas.FirstOrDefaultAsync(x => x.AreaId == id) ?? throw ReglaNegocioException.NoEncontrado("Área");
        if (await _db.Productos.AnyAsync(p => p.AreaId == id))
            throw new ReglaNegocioException($"El área {a.Nombre} tiene platos asignados: muévelos o desactívala.");
        _db.Areas.Remove(a);
        await _db.SaveChangesAsync();
        return await AreasAsync();
    }

    public async Task<List<AreaDto>> OrdenarAreasAsync(OrdenDto dto)
    {
        var areas = await _db.Areas.ToListAsync();
        Reordenar(areas, dto.Ids, a => a.AreaId, (a, o) => a.Orden = o);
        await _db.SaveChangesAsync();
        return await AreasAsync();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // CATEGORÍAS
    // ═════════════════════════════════════════════════════════════════════════
    public async Task<List<CategoriaDto>> CategoriasAsync()
    {
        var conteo = await _db.Productos.GroupBy(p => p.CategoriaId).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
        return (await _db.Categorias.AsNoTracking().OrderBy(c => c.Orden).ThenBy(c => c.Nombre).ToListAsync())
            .Select(c => new CategoriaDto(c.CategoriaId, c.Nombre, c.Descripcion, c.Color, c.Orden, c.Activo,
                conteo.FirstOrDefault(x => x.Key == c.CategoriaId)?.N ?? 0)).ToList();
    }

    public async Task<List<CategoriaDto>> GuardarCategoriaAsync(int? id, CategoriaGuardarDto dto)
    {
        var nombre = Texto.Requerido(dto.Nombre, "Nombre de la categoría", 60, 2);
        if (await _db.Categorias.AnyAsync(c => c.CategoriaId != id && c.Nombre.ToLower() == nombre.ToLower()))
            throw new ReglaNegocioException($"Ya existe una categoría llamada {nombre}.");
        Categoria c;
        if (id == null)
        {
            c = new Categoria { CreatedAt = DateTime.UtcNow, Orden = (await _db.Categorias.MaxAsync(x => (int?)x.Orden) ?? 0) + 1 };
            _db.Categorias.Add(c);
        }
        else c = await _db.Categorias.FirstOrDefaultAsync(x => x.CategoriaId == id) ?? throw ReglaNegocioException.NoEncontrado("Categoría");
        c.Nombre = nombre;
        c.Descripcion = Texto.Opcional(dto.Descripcion, "Descripción", 200);
        c.Color = ValidarColor(dto.Color, "#B08834");
        await _db.SaveChangesAsync();
        return await CategoriasAsync();
    }

    public async Task<List<CategoriaDto>> EstadoCategoriaAsync(int id, bool activo)
    {
        var c = await _db.Categorias.FirstOrDefaultAsync(x => x.CategoriaId == id) ?? throw ReglaNegocioException.NoEncontrado("Categoría");
        c.Activo = activo;
        await _db.SaveChangesAsync();
        return await CategoriasAsync();
    }

    public async Task<List<CategoriaDto>> EliminarCategoriaAsync(int id)
    {
        var c = await _db.Categorias.FirstOrDefaultAsync(x => x.CategoriaId == id) ?? throw ReglaNegocioException.NoEncontrado("Categoría");
        if (await _db.Productos.AnyAsync(p => p.CategoriaId == id))
            throw new ReglaNegocioException($"La categoría {c.Nombre} tiene platos: muévelos o desactívala.");
        _db.Categorias.Remove(c);
        await _db.SaveChangesAsync();
        return await CategoriasAsync();
    }

    public async Task<List<CategoriaDto>> OrdenarCategoriasAsync(OrdenDto dto)
    {
        var lista = await _db.Categorias.ToListAsync();
        Reordenar(lista, dto.Ids, c => c.CategoriaId, (c, o) => c.Orden = o);
        await _db.SaveChangesAsync();
        return await CategoriasAsync();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // MODIFICADORES
    // ═════════════════════════════════════════════════════════════════════════
    public async Task<List<GrupoDto>> GruposAsync()
    {
        var uso = await _db.ProductoGrupos.GroupBy(x => x.GrupoId).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
        return (await _db.GruposModificador.AsNoTracking().Include(g => g.Opciones).OrderBy(g => g.Orden).ThenBy(g => g.Nombre).ToListAsync())
            .Select(g => new GrupoDto(g.GrupoId, g.Nombre, g.Minimo, g.Maximo, g.Orden, g.Activo,
                g.Opciones.OrderBy(o => o.Orden).Select(o => new OpcionDto(o.OpcionId, o.Nombre, o.PrecioExtra, o.Orden, o.Activo)).ToList(),
                uso.FirstOrDefault(u => u.Key == g.GrupoId)?.N ?? 0)).ToList();
    }

    public async Task<List<GrupoDto>> GuardarGrupoAsync(int? id, GrupoGuardarDto dto)
    {
        var nombre = Texto.Requerido(dto.Nombre, "Nombre del grupo", 80, 2);
        if (dto.Minimo is < 0 or > 20) throw new ReglaNegocioException("El mínimo va de 0 a 20.");
        if (dto.Maximo is < 1 or > 20) throw new ReglaNegocioException("El máximo va de 1 a 20.");
        if (dto.Minimo > dto.Maximo) throw new ReglaNegocioException("El mínimo no puede ser mayor que el máximo.");
        var opciones = dto.Opciones ?? [];
        if (opciones.Count == 0) throw new ReglaNegocioException("Agrega al menos una opción al grupo.");
        if (opciones.Count > 40) throw new ReglaNegocioException("Un grupo admite hasta 40 opciones.");
        var limpias = opciones.Select(o => (o.OpcionId, Nombre: Texto.Requerido(o.Nombre, "Nombre de la opción", 80), o.PrecioExtra, o.Activo)).ToList();
        if (limpias.GroupBy(o => o.Nombre.ToLower()).Any(g => g.Count() > 1)) throw new ReglaNegocioException("Hay opciones repetidas en el grupo.");
        if (limpias.Any(o => o.PrecioExtra is < 0 or > 9999 || Math.Round(o.PrecioExtra, 2) != o.PrecioExtra))
            throw new ReglaNegocioException("El precio extra va de 0 a 9 999, con 2 decimales como máximo.");
        var activas = limpias.Count(o => o.Activo);
        if (activas < dto.Minimo) throw new ReglaNegocioException($"El grupo exige elegir {dto.Minimo} pero solo tiene {activas} opción(es) activa(s).");
        if (await _db.GruposModificador.AnyAsync(g => g.GrupoId != id && g.Nombre.ToLower() == nombre.ToLower()))
            throw new ReglaNegocioException($"Ya existe un grupo llamado {nombre}.");

        GrupoModificador grupo;
        if (id == null)
        {
            grupo = new GrupoModificador { Orden = (await _db.GruposModificador.MaxAsync(x => (int?)x.Orden) ?? 0) + 1 };
            _db.GruposModificador.Add(grupo);
        }
        else grupo = await _db.GruposModificador.Include(g => g.Opciones).FirstOrDefaultAsync(g => g.GrupoId == id)
                     ?? throw ReglaNegocioException.NoEncontrado("Grupo");

        grupo.Nombre = nombre;
        grupo.Minimo = dto.Minimo;
        grupo.Maximo = dto.Maximo;

        // Las opciones que ya no vienen se quitan; las demás se actualizan en su lugar.
        var ids = limpias.Where(o => o.OpcionId.HasValue).Select(o => o.OpcionId!.Value).ToHashSet();
        if (ids.Any(x => grupo.Opciones.All(o => o.OpcionId != x))) throw new ReglaNegocioException("Una opción no pertenece a este grupo.");
        _db.OpcionesModificador.RemoveRange(grupo.Opciones.Where(o => !ids.Contains(o.OpcionId)));
        for (var i = 0; i < limpias.Count; i++)
        {
            var o = limpias[i];
            var entidad = o.OpcionId.HasValue ? grupo.Opciones.First(x => x.OpcionId == o.OpcionId) : new ModificadorOpcion { Nombre = o.Nombre };
            if (o.OpcionId.HasValue) Renombrar(entidad, entidad.OpcionId, entidad.Nombre, o.Nombre, (e, n) => e.Nombre = n);
            entidad.PrecioExtra = o.PrecioExtra;
            entidad.Activo = o.Activo;
            entidad.Orden = i + 1;
            if (!o.OpcionId.HasValue) grupo.Opciones.Add(entidad);
        }
        await GuardarPorPartesAsync();
        return await GruposAsync();
    }

    public async Task<List<GrupoDto>> EstadoGrupoAsync(int id, bool activo)
    {
        var g = await _db.GruposModificador.FirstOrDefaultAsync(x => x.GrupoId == id) ?? throw ReglaNegocioException.NoEncontrado("Grupo");
        g.Activo = activo;
        await _db.SaveChangesAsync();
        return await GruposAsync();
    }

    public async Task<List<GrupoDto>> EliminarGrupoAsync(int id)
    {
        var g = await _db.GruposModificador.FirstOrDefaultAsync(x => x.GrupoId == id) ?? throw ReglaNegocioException.NoEncontrado("Grupo");
        _db.GruposModificador.Remove(g); // quita también sus opciones y sus asignaciones (cascada)
        await _db.SaveChangesAsync();
        return await GruposAsync();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PRODUCTOS
    // ═════════════════════════════════════════════════════════════════════════
    public async Task<List<ProductoListaDto>> ProductosAsync(int? categoriaId, string? tipo, string? buscar, bool incluirInactivos)
    {
        var q = _db.Productos.AsNoTracking().Include(p => p.Categoria).Include(p => p.Area).AsQueryable();
        if (categoriaId.HasValue) q = q.Where(p => p.CategoriaId == categoriaId);
        if (!string.IsNullOrWhiteSpace(tipo)) q = q.Where(p => p.Tipo == tipo.ToUpper());
        if (!incluirInactivos) q = q.Where(p => p.Activo);
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var patron = $"%{buscar.Trim()}%";
            q = q.Where(p => EF.Functions.ILike(p.Nombre, patron) || (p.Codigo != null && EF.Functions.ILike(p.Codigo, patron)));
        }
        var productos = await q.OrderBy(p => p.Categoria!.Orden).ThenBy(p => p.Orden).ThenBy(p => p.Nombre).ToListAsync();
        var ids = productos.Select(p => p.ProductoId).ToList();
        var variantes = await _db.Variantes.Where(v => ids.Contains(v.ProductoId) && v.Activo)
            .GroupBy(v => v.ProductoId).Select(g => new { g.Key, N = g.Count(), Min = g.Min(v => v.Precio) }).ToListAsync();
        var grupos = await _db.ProductoGrupos.Where(x => ids.Contains(x.ProductoId)).GroupBy(x => x.ProductoId)
            .Select(g => new { g.Key, N = g.Count() }).ToListAsync();
        var sucursales = await _db.ProductoSucursales.Where(x => ids.Contains(x.ProductoId)).GroupBy(x => x.ProductoId)
            .Select(g => new { g.Key, N = g.Count() }).ToListAsync();

        return productos.Select(p =>
        {
            var v = variantes.FirstOrDefault(x => x.Key == p.ProductoId);
            return new ProductoListaDto(p.ProductoId, p.Nombre, p.Codigo, p.Tipo, p.CategoriaId, p.Categoria!.Nombre, p.Categoria.Color,
                p.AreaId, p.Area?.Nombre, p.Precio, v?.Min, p.Imagen, p.Activo, v?.N ?? 0,
                grupos.FirstOrDefault(x => x.Key == p.ProductoId)?.N ?? 0, sucursales.FirstOrDefault(x => x.Key == p.ProductoId)?.N ?? 0,
                p.Etiquetas, p.Orden);
        }).ToList();
    }

    public async Task<ProductoDetalleDto> ProductoAsync(int id)
    {
        var p = await _db.Productos.AsNoTracking()
            .Include(x => x.Variantes).Include(x => x.Grupos).Include(x => x.Sucursales)
            .Include(x => x.ComboGrupos).ThenInclude(g => g.Opciones).ThenInclude(o => o.Producto)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.ProductoId == id) ?? throw ReglaNegocioException.NoEncontrado("Plato");
        var hoy = DiaLima.Hoy();
        var sucursales = await _db.Sucursales.AsNoTracking().Where(s => s.Activo).OrderByDescending(s => s.EsPrincipal).ThenBy(s => s.Nombre).ToListAsync();
        return new ProductoDetalleDto(p.ProductoId, p.CategoriaId, p.AreaId, p.Tipo, p.Codigo, p.Nombre, p.Descripcion, p.Precio,
            p.AfectacionIgv, p.TiempoPreparacionMin, p.Etiquetas, p.Imagen, p.Activo,
            p.Variantes.OrderBy(v => v.Orden).Select(v => new VarianteDto(v.VarianteId, v.Nombre, v.Precio, v.Orden, v.Activo)).ToList(),
            p.Grupos.OrderBy(g => g.Orden).Select(g => g.GrupoId).ToList(),
            p.ComboGrupos.OrderBy(g => g.Orden).Select(g => new ComboGrupoDto(g.ComboGrupoId, g.Nombre, g.Cantidad,
                g.Opciones.OrderBy(o => o.Orden).Select(o => new ComboOpcionDto(o.ProductoId, o.Producto?.Nombre ?? "", o.PrecioExtra, o.Producto?.Activo ?? false)).ToList())).ToList(),
            sucursales.Select(s =>
            {
                var ps = p.Sucursales.FirstOrDefault(x => x.SucursalId == s.SucursalId);
                return new ProductoSucursalDto(s.SucursalId, s.Nombre, ps != null, ps?.Precio, ps?.AgotadoFecha == hoy);
            }).ToList(),
            p.UpdatedAt);
    }

    public async Task<ProductoDetalleDto> GuardarProductoAsync(int? id, ProductoGuardarDto dto)
    {
        var puedeCombos = await PuedeCombosAsync();
        var tipo = (dto.Tipo ?? TiposProducto.Plato).Trim().ToUpperInvariant();
        if (!TiposProducto.Todos.Contains(tipo)) throw new ReglaNegocioException("Tipo no válido: plato, bebida, producto o combo.");
        if (tipo == TiposProducto.Combo && !puedeCombos)
            throw new ReglaNegocioException("Tu plan no incluye combos. Sube al plan Avanzado para armarlos.", 403);
        var tocaAvanzado = (dto.Variantes?.Count > 0) || (dto.GrupoIds?.Count > 0) || (dto.ComboGrupos?.Count > 0);
        if (tocaAvanzado && !puedeCombos)
            throw new ReglaNegocioException("Tu plan no incluye variantes, modificadores ni combos.", 403);

        var nombre = Texto.Requerido(dto.Nombre, "Nombre", 120, 2);
        var codigo = Texto.Opcional(dto.Codigo, "Código", 20)?.ToUpperInvariant();
        if (codigo != null && !Regex.IsMatch(codigo, @"^[A-Z0-9\-]{1,20}$")) throw new ReglaNegocioException("El código admite letras, números y guiones.");
        if (dto.Precio is < 0 or > 99999 || Math.Round(dto.Precio, 2) != dto.Precio)
            throw new ReglaNegocioException("El precio va de 0 a 99 999, con 2 decimales como máximo.");
        var afectacion = (dto.AfectacionIgv ?? "GRAVADO").ToUpperInvariant();
        if (!Afectaciones.Contains(afectacion)) throw new ReglaNegocioException("Afectación al IGV no válida.");
        if (dto.TiempoPreparacionMin is < 0 or > 240) throw new ReglaNegocioException("El tiempo de preparación va de 0 a 240 minutos.");
        var etiquetas = (dto.Etiquetas ?? []).Select(e => Texto.Limpio(e)).Where(e => e != null).Select(e => e!.ToLowerInvariant())
            .Distinct().ToArray();
        if (etiquetas.Length > 8 || etiquetas.Any(e => e.Length > 20)) throw new ReglaNegocioException("Hasta 8 etiquetas de 20 caracteres.");

        if (!await _db.Categorias.AnyAsync(c => c.CategoriaId == dto.CategoriaId && c.Activo))
            throw new ReglaNegocioException("Elige una categoría activa.");
        int? areaId = tipo == TiposProducto.Combo ? null : dto.AreaId; // cada componente del combo va a su propia área
        if (areaId.HasValue && !await _db.Areas.AnyAsync(a => a.AreaId == areaId && a.Activo))
            throw new ReglaNegocioException("El área de preparación no existe o está desactivada.");
        if (await _db.Productos.AnyAsync(p => p.ProductoId != id && p.Nombre.ToLower() == nombre.ToLower()))
            throw new ReglaNegocioException($"Ya existe un plato llamado {nombre}.");
        if (codigo != null && await _db.Productos.AnyAsync(p => p.ProductoId != id && p.Codigo != null && p.Codigo.ToUpper() == codigo))
            throw new ReglaNegocioException($"El código {codigo} ya lo usa otro plato.");

        Producto p;
        if (id == null)
        {
            p = new Producto { CreatedAt = DateTime.UtcNow, Orden = (await _db.Productos.Where(x => x.CategoriaId == dto.CategoriaId).MaxAsync(x => (int?)x.Orden) ?? 0) + 1 };
            _db.Productos.Add(p);
        }
        else
        {
            p = await _db.Productos.Include(x => x.Variantes).Include(x => x.Grupos).Include(x => x.Sucursales)
                    .Include(x => x.ComboGrupos).ThenInclude(g => g.Opciones).AsSplitQuery()
                    .FirstOrDefaultAsync(x => x.ProductoId == id) ?? throw ReglaNegocioException.NoEncontrado("Plato");
            if (p.Tipo == TiposProducto.Combo && tipo != TiposProducto.Combo && p.ComboGrupos.Count > 0 && !puedeCombos)
                throw new ReglaNegocioException("Tu plan no incluye combos.", 403);
            if (p.Tipo != TiposProducto.Combo && tipo == TiposProducto.Combo &&
                await _db.ComboOpciones.AnyAsync(o => o.ProductoId == p.ProductoId))
                throw new ReglaNegocioException("Este plato es parte de otro combo: no puede convertirse en combo.");
        }

        var precioAntes = id == null ? (decimal?)null : p.Precio;
        p.CategoriaId = dto.CategoriaId;
        p.AreaId = areaId;
        p.Tipo = tipo;
        p.Codigo = codigo;
        p.Nombre = nombre;
        p.Descripcion = Texto.Opcional(dto.Descripcion, "Descripción", 500);
        p.Precio = dto.Precio;
        p.AfectacionIgv = afectacion;
        p.TiempoPreparacionMin = dto.TiempoPreparacionMin;
        p.Etiquetas = etiquetas;
        p.UpdatedAt = DateTime.UtcNow;
        if (id == null) p.Activo = true;

        // ─── Avanzado (solo si el plan lo incluye; si no, lo existente queda intacto) ───
        if (puedeCombos)
        {
            if (dto.Variantes != null) await AplicarVariantesAsync(p, dto.Variantes);
            if (dto.GrupoIds != null) await AplicarGruposAsync(p, dto.GrupoIds);
            if (tipo == TiposProducto.Combo) await AplicarComboAsync(p, dto.ComboGrupos ?? []);
            else if (p.ComboGrupos.Count > 0) _db.ComboGrupos.RemoveRange(p.ComboGrupos);
        }

        // ─── Sucursales ───
        var cambiosSucursal = await AplicarSucursalesAsync(p, dto.Sucursales, id == null);

        if (id == null)
            await _bitacora.AgregarAsync(Tenant, "PRODUCTO_CREADO", "Producto", null, $"{nombre} ({tipo}) a S/ {Texto.Soles(p.Precio)}.");
        else if (precioAntes != p.Precio)
            await _bitacora.AgregarAsync(Tenant, "PRODUCTO_PRECIO", "Producto", p.ProductoId,
                $"{nombre}: precio S/ {Texto.Soles(precioAntes!.Value)} → S/ {Texto.Soles(p.Precio)}.");
        if (cambiosSucursal.Count > 0)
            await _bitacora.AgregarAsync(Tenant, "PRODUCTO_PRECIO_SUCURSAL", "Producto", id, $"{nombre}: {string.Join("; ", cambiosSucursal)}.");

        await GuardarPorPartesAsync();
        return await ProductoAsync(p.ProductoId);
    }

    private async Task AplicarVariantesAsync(Producto p, List<VarianteGuardarDto> variantes)
    {
        if (variantes.Count > 12) throw new ReglaNegocioException("Hasta 12 variantes por plato.");
        var limpias = variantes.Select(v => (v.VarianteId, Nombre: Texto.Requerido(v.Nombre, "Nombre de la variante", 60), v.Precio, v.Activo)).ToList();
        if (limpias.GroupBy(v => v.Nombre.ToLower()).Any(g => g.Count() > 1)) throw new ReglaNegocioException("Hay variantes repetidas.");
        if (limpias.Any(v => v.Precio is < 0 or > 99999 || Math.Round(v.Precio, 2) != v.Precio))
            throw new ReglaNegocioException("El precio de cada variante va de 0 a 99 999, con 2 decimales.");
        var ids = limpias.Where(v => v.VarianteId.HasValue).Select(v => v.VarianteId!.Value).ToHashSet();
        if (ids.Any(x => p.Variantes.All(v => v.VarianteId != x))) throw new ReglaNegocioException("Una variante no pertenece a este plato.");
        _db.Variantes.RemoveRange(p.Variantes.Where(v => !ids.Contains(v.VarianteId)));
        for (var i = 0; i < limpias.Count; i++)
        {
            var v = limpias[i];
            var e = v.VarianteId.HasValue ? p.Variantes.First(x => x.VarianteId == v.VarianteId) : new ProductoVariante { Nombre = v.Nombre };
            if (v.VarianteId.HasValue) Renombrar(e, e.VarianteId, e.Nombre, v.Nombre, (x, n) => x.Nombre = n);
            e.Precio = v.Precio;
            e.Activo = v.Activo;
            e.Orden = i + 1;
            if (!v.VarianteId.HasValue) p.Variantes.Add(e);
        }
        await Task.CompletedTask;
    }

    private async Task AplicarGruposAsync(Producto p, List<int> grupoIds)
    {
        var ids = grupoIds.Distinct().ToList();
        if (ids.Count > 10) throw new ReglaNegocioException("Hasta 10 grupos de modificadores por plato.");
        var existentes = await _db.GruposModificador.Where(g => ids.Contains(g.GrupoId)).Select(g => g.GrupoId).ToListAsync();
        if (existentes.Count != ids.Count) throw new ReglaNegocioException("Un grupo de modificadores no existe.");
        _db.ProductoGrupos.RemoveRange(p.Grupos.Where(g => !ids.Contains(g.GrupoId)));
        for (var i = 0; i < ids.Count; i++)
        {
            var actual = p.Grupos.FirstOrDefault(g => g.GrupoId == ids[i]);
            if (actual != null) actual.Orden = i + 1;
            else p.Grupos.Add(new ProductoGrupoModificador { GrupoId = ids[i], Orden = i + 1, TenantId = Tenant });
        }
    }

    private async Task AplicarComboAsync(Producto p, List<ComboGrupoGuardarDto> grupos)
    {
        if (grupos.Count == 0) throw new ReglaNegocioException("Un combo necesita al menos un grupo (por ejemplo: \"Plato de fondo\", \"Bebida\").");
        if (grupos.Count > 8) throw new ReglaNegocioException("Un combo admite hasta 8 grupos.");
        var todos = grupos.SelectMany(g => g.Opciones ?? []).Select(o => o.ProductoId).Distinct().ToList();
        var componentes = await _db.Productos.Where(x => todos.Contains(x.ProductoId))
            .Select(x => new { x.ProductoId, x.Tipo, x.Activo, x.Nombre }).ToListAsync();

        var nuevos = new List<ComboGrupo>();
        for (var i = 0; i < grupos.Count; i++)
        {
            var g = grupos[i];
            var nombre = Texto.Requerido(g.Nombre, "Nombre del grupo del combo", 80);
            var opciones = g.Opciones ?? [];
            if (opciones.Count == 0) throw new ReglaNegocioException($"El grupo \"{nombre}\" no tiene productos.");
            if (g.Cantidad is < 1 or > 20) throw new ReglaNegocioException($"En \"{nombre}\" la cantidad va de 1 a 20.");
            if (opciones.GroupBy(o => o.ProductoId).Any(x => x.Count() > 1)) throw new ReglaNegocioException($"En \"{nombre}\" hay un producto repetido.");
            foreach (var o in opciones)
            {
                var c = componentes.FirstOrDefault(x => x.ProductoId == o.ProductoId)
                        ?? throw new ReglaNegocioException("Un producto del combo no existe.");
                if (c.ProductoId == p.ProductoId || c.Tipo == TiposProducto.Combo) throw new ReglaNegocioException("Un combo no puede incluir otro combo.");
                if (!c.Activo) throw new ReglaNegocioException($"{c.Nombre} está desactivado: no puede ir en el combo.");
                if (o.PrecioExtra is < 0 or > 9999 || Math.Round(o.PrecioExtra, 2) != o.PrecioExtra)
                    throw new ReglaNegocioException("El precio extra va de 0 a 9 999, con 2 decimales.");
            }
            nuevos.Add(new ComboGrupo
            {
                TenantId = Tenant, Nombre = nombre, Cantidad = g.Cantidad, Orden = i + 1,
                Opciones = opciones.Select((o, j) => new ComboOpcion { TenantId = Tenant, ProductoId = o.ProductoId, PrecioExtra = o.PrecioExtra, Orden = j + 1 }).ToList()
            });
        }
        // El combo se rearma entero: es una receta, no hay historial que conservar aquí.
        _db.ComboGrupos.RemoveRange(p.ComboGrupos);
        p.ComboGrupos.AddRange(nuevos);
    }

    private async Task<List<string>> AplicarSucursalesAsync(Producto p, List<SucursalPrecioDto>? lista, bool nuevo)
    {
        var cambios = new List<string>();
        var activas = await _db.Sucursales.Where(s => s.Activo).Select(s => new { s.SucursalId, s.Nombre }).ToListAsync();
        if (lista == null)
        {
            // Al crear sin indicar, se vende en todas las sucursales activas.
            if (nuevo) foreach (var s in activas) p.Sucursales.Add(new ProductoSucursal { SucursalId = s.SucursalId, TenantId = Tenant });
            return cambios;
        }
        if (lista.GroupBy(x => x.SucursalId).Any(g => g.Count() > 1)) throw new ReglaNegocioException("Una sucursal está repetida.");
        var tieneVariantes = p.Variantes.Any(v => v.Activo);
        foreach (var x in lista)
        {
            var s = activas.FirstOrDefault(a => a.SucursalId == x.SucursalId)
                    ?? throw new ReglaNegocioException("Una sucursal no existe o está desactivada.");
            if (x.Precio is < 0 or > 99999 || (x.Precio.HasValue && Math.Round(x.Precio.Value, 2) != x.Precio))
                throw new ReglaNegocioException($"Precio no válido en {s.Nombre}.");
            if (x.Precio.HasValue && tieneVariantes)
                throw new ReglaNegocioException("Este plato tiene variantes: el precio va en cada variante, no por sucursal.");
            var actual = p.Sucursales.FirstOrDefault(ps => ps.SucursalId == x.SucursalId);
            if (!x.Activa)
            {
                if (actual != null) { _db.ProductoSucursales.Remove(actual); cambios.Add($"deja de venderse en {s.Nombre}"); }
                continue;
            }
            if (actual == null)
            {
                p.Sucursales.Add(new ProductoSucursal { SucursalId = x.SucursalId, Precio = x.Precio, TenantId = Tenant });
                if (!nuevo) cambios.Add($"se vende en {s.Nombre}{(x.Precio.HasValue ? $" a S/ {Texto.Soles(x.Precio.Value)}" : "")}");
            }
            else if (actual.Precio != x.Precio)
            {
                cambios.Add($"{s.Nombre}: {(actual.Precio.HasValue ? "S/ " + Texto.Soles(actual.Precio.Value) : "precio base")} → " +
                            $"{(x.Precio.HasValue ? "S/ " + Texto.Soles(x.Precio.Value) : "precio base")}");
                actual.Precio = x.Precio;
            }
        }
        return cambios;
    }

    public async Task<ProductoDetalleDto> EstadoProductoAsync(int id, bool activo)
    {
        var p = await _db.Productos.FirstOrDefaultAsync(x => x.ProductoId == id) ?? throw ReglaNegocioException.NoEncontrado("Plato");
        if (!activo)
        {
            var combos = await _db.ComboOpciones.Where(o => o.ProductoId == id)
                .Join(_db.ComboGrupos, o => o.ComboGrupoId, g => g.ComboGrupoId, (o, g) => g.ProductoId)
                .Join(_db.Productos.Where(c => c.Activo), cid => cid, c => c.ProductoId, (cid, c) => c.Nombre).Distinct().ToListAsync();
            if (combos.Count > 0)
                throw new ReglaNegocioException($"{p.Nombre} está en combos activos ({string.Join(", ", combos)}): quítalo de ellos primero.");
        }
        else if (!await _db.Categorias.AnyAsync(c => c.CategoriaId == p.CategoriaId && c.Activo))
            throw new ReglaNegocioException("Su categoría está desactivada: actívala primero.");
        p.Activo = activo;
        p.UpdatedAt = DateTime.UtcNow;
        await _bitacora.AgregarAsync(Tenant, activo ? "PRODUCTO_ACTIVADO" : "PRODUCTO_DESACTIVADO", "Producto", id, p.Nombre);
        await _db.SaveChangesAsync();
        return await ProductoAsync(id);
    }

    public async Task EliminarProductoAsync(int id)
    {
        var p = await _db.Productos.FirstOrDefaultAsync(x => x.ProductoId == id) ?? throw ReglaNegocioException.NoEncontrado("Plato");
        if (await _db.ComboOpciones.AnyAsync(o => o.ProductoId == id))
            throw new ReglaNegocioException($"{p.Nombre} es parte de un combo: quítalo del combo o desactívalo.");
        var imagen = p.Imagen;
        _db.Productos.Remove(p);
        await _bitacora.AgregarAsync(Tenant, "PRODUCTO_ELIMINADO", "Producto", id, p.Nombre);
        await _db.SaveChangesAsync();
        BorrarArchivo(imagen);
    }

    public async Task<ProductoDetalleDto> DuplicarAsync(int id)
    {
        var o = await ProductoAsync(id);
        var nombre = o.Nombre.Length > 110 ? o.Nombre[..110] : o.Nombre;
        var n = 2;
        var candidato = $"{nombre} (copia)";
        while (await _db.Productos.AnyAsync(p => p.Nombre.ToLower() == candidato.ToLower())) candidato = $"{nombre} (copia {n++})";
        var puedeCombos = await PuedeCombosAsync();
        return await GuardarProductoAsync(null, new ProductoGuardarDto(o.CategoriaId, o.AreaId, o.Tipo, null, candidato, o.Descripcion,
            o.Precio, o.AfectacionIgv, o.TiempoPreparacionMin, o.Etiquetas,
            puedeCombos ? o.Variantes.Select(v => new VarianteGuardarDto(null, v.Nombre, v.Precio, v.Activo)).ToList() : null,
            puedeCombos ? o.GrupoIds : null,
            puedeCombos ? o.ComboGrupos.Select(g => new ComboGrupoGuardarDto(g.Nombre, g.Cantidad,
                g.Opciones.Select(x => new ComboOpcionGuardarDto(x.ProductoId, x.PrecioExtra)).ToList())).ToList() : null,
            o.Sucursales.Select(s => new SucursalPrecioDto(s.SucursalId, s.Activa, o.Variantes.Any(v => v.Activo) ? null : s.Precio)).ToList()));
    }

    public async Task<List<ProductoListaDto>> OrdenarProductosAsync(OrdenDto dto)
    {
        var ids = dto.Ids ?? [];
        var lista = await _db.Productos.Where(p => ids.Contains(p.ProductoId)).ToListAsync();
        Reordenar(lista, ids, p => p.ProductoId, (p, o) => p.Orden = o);
        await _db.SaveChangesAsync();
        return await ProductosAsync(null, null, null, true);
    }

    // ─── Foto ───
    public async Task<ProductoDetalleDto> GuardarImagenAsync(int id, IFormFile? archivo)
    {
        var p = await _db.Productos.FirstOrDefaultAsync(x => x.ProductoId == id) ?? throw ReglaNegocioException.NoEncontrado("Plato");
        if (archivo == null || archivo.Length == 0) throw new ReglaNegocioException("Elige una foto.");
        if (archivo.Length > MaxImagen) throw new ReglaNegocioException("La foto pesa más de 2 MB: redúcela e intenta de nuevo.");

        // El tipo se decide por los bytes del archivo, nunca por su nombre.
        var cabecera = new byte[12];
        await using (var s = archivo.OpenReadStream()) _ = await s.ReadAsync(cabecera);
        var ext = cabecera switch
        {
            [0xFF, 0xD8, 0xFF, ..] => "jpg",
            [0x89, 0x50, 0x4E, 0x47, ..] => "png",
            [0x52, 0x49, 0x46, 0x46, _, _, _, _, 0x57, 0x45, 0x42, 0x50] => "webp",
            _ => throw new ReglaNegocioException("La foto debe ser JPG, PNG o WEBP.")
        };

        var relativo = $"t{Tenant}/productos/{id}-{Guid.NewGuid():N}.{ext}";
        var destino = Path.Combine(_rutaArchivos, relativo.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
        await using (var f = File.Create(destino)) await archivo.CopyToAsync(f);

        var anterior = p.Imagen;
        p.Imagen = relativo;
        p.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        BorrarArchivo(anterior);
        return await ProductoAsync(id);
    }

    public async Task<ProductoDetalleDto> QuitarImagenAsync(int id)
    {
        var p = await _db.Productos.FirstOrDefaultAsync(x => x.ProductoId == id) ?? throw ReglaNegocioException.NoEncontrado("Plato");
        var anterior = p.Imagen;
        p.Imagen = null;
        await _db.SaveChangesAsync();
        BorrarArchivo(anterior);
        return await ProductoAsync(id);
    }

    private void BorrarArchivo(string? relativo)
    {
        if (string.IsNullOrEmpty(relativo) || relativo.Contains("..")) return;
        try { File.Delete(Path.Combine(_rutaArchivos, relativo.Replace('/', Path.DirectorySeparatorChar))); }
        catch (IOException) { /* si no se pudo borrar, queda huérfano: no bloquea la operación */ }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // SUCURSALES, DISPONIBILIDAD Y MENÚ
    // ═════════════════════════════════════════════════════════════════════════
    public async Task<List<SucursalSimpleDto>> SucursalesAsync()
    {
        var q = _db.Sucursales.AsNoTracking().Where(s => s.Activo);
        if (_sesion.RolId != RolIds.Administrador && _sesion.SucursalId.HasValue) q = q.Where(s => s.SucursalId == _sesion.SucursalId);
        return await q.OrderByDescending(s => s.EsPrincipal).ThenBy(s => s.Nombre)
            .Select(s => new SucursalSimpleDto(s.SucursalId, s.Nombre, s.EsPrincipal)).ToListAsync();
    }

    // El Administrador elige sucursal; los demás trabajan en la suya, sin excepción.
    private async Task<(int id, string nombre)> SucursalDeTrabajoAsync(int? pedida)
    {
        var id = _sesion.RolId == RolIds.Administrador ? pedida ?? _sesion.SucursalId : _sesion.SucursalId;
        if (id == null) throw new ReglaNegocioException("Elige una sucursal.");
        var s = await _db.Sucursales.AsNoTracking().FirstOrDefaultAsync(x => x.SucursalId == id && x.Activo)
                ?? throw new ReglaNegocioException("La sucursal no existe o está desactivada.");
        return (s.SucursalId, s.Nombre);
    }

    public async Task<DisponibilidadDto> DisponibilidadAsync(int? sucursalId)
    {
        var (sid, nombre) = await SucursalDeTrabajoAsync(sucursalId);
        var hoy = DiaLima.Hoy();
        var filas = await (from ps in _db.ProductoSucursales
                           join p in _db.Productos on ps.ProductoId equals p.ProductoId
                           join c in _db.Categorias on p.CategoriaId equals c.CategoriaId
                           where ps.SucursalId == sid && p.Activo && c.Activo
                           orderby c.Orden, p.Orden, p.Nombre
                           select new DisponibilidadItemDto(p.ProductoId, p.Nombre, p.Tipo, c.CategoriaId, c.Nombre, c.Color, p.Imagen,
                               ps.AgotadoFecha == hoy)).ToListAsync();
        return new DisponibilidadDto(sid, nombre, hoy, filas);
    }

    public async Task<DisponibilidadDto> MarcarAgotadoAsync(MarcarAgotadoDto dto)
    {
        var (sid, nombre) = await SucursalDeTrabajoAsync(dto.SucursalId);
        var ps = await _db.ProductoSucursales.FirstOrDefaultAsync(x => x.ProductoId == dto.ProductoId && x.SucursalId == sid)
                 ?? throw new ReglaNegocioException($"Ese plato no se vende en {nombre}.");
        var producto = await _db.Productos.Where(p => p.ProductoId == dto.ProductoId).Select(p => p.Nombre).FirstAsync();
        ps.AgotadoFecha = dto.Agotado ? DiaLima.Hoy() : null;
        await _bitacora.AgregarAsync(Tenant, dto.Agotado ? "PRODUCTO_AGOTADO" : "PRODUCTO_REPUESTO", "Producto", dto.ProductoId,
            $"{producto} {(dto.Agotado ? "agotado hoy" : "disponible otra vez")} en {nombre}.");
        await _db.SaveChangesAsync();
        return await DisponibilidadAsync(sid);
    }

    // La carta tal como se vende HOY en la sucursal: la usa el POS y el mesero.
    public async Task<MenuDto> MenuAsync(int? sucursalId)
    {
        var (sid, nombre) = await SucursalDeTrabajoAsync(sucursalId);
        var hoy = DiaLima.Hoy();
        var combos = (await MisPermisosAsync()).Funciones.Contains(FuncionCombos);

        var productos = await _db.Productos.AsNoTracking()
            .Include(p => p.Categoria).Include(p => p.Variantes).Include(p => p.Sucursales)
            .Include(p => p.Grupos).ThenInclude(g => g.Grupo).ThenInclude(g => g!.Opciones)
            .Include(p => p.ComboGrupos).ThenInclude(g => g.Opciones).ThenInclude(o => o.Producto)
            .AsSplitQuery()
            .Where(p => p.Activo && p.Categoria!.Activo && p.Sucursales.Any(s => s.SucursalId == sid))
            .Where(p => combos || p.Tipo != TiposProducto.Combo)
            .ToListAsync();

        // Agotado de los componentes: un combo sin ninguna opción disponible en un grupo, se agota.
        var agotadosHoy = await _db.ProductoSucursales.Where(x => x.SucursalId == sid && x.AgotadoFecha == hoy).Select(x => x.ProductoId).ToListAsync();
        var vendibles = await _db.ProductoSucursales.Where(x => x.SucursalId == sid).Select(x => x.ProductoId).ToListAsync();

        MenuProductoDto Mapear(Producto p)
        {
            var ps = p.Sucursales.First(s => s.SucursalId == sid);
            var comboGrupos = !combos ? [] : p.ComboGrupos.OrderBy(g => g.Orden).Select(g => new MenuComboGrupoDto(g.ComboGrupoId, g.Nombre, g.Cantidad,
                g.Opciones.Where(o => o.Producto!.Activo && vendibles.Contains(o.ProductoId)).OrderBy(o => o.Orden)
                    .Select(o => new MenuComboOpcionDto(o.ProductoId, o.Producto!.Nombre, o.PrecioExtra, agotadosHoy.Contains(o.ProductoId))).ToList())).ToList();
            var agotado = ps.AgotadoFecha == hoy || comboGrupos.Any(g => g.Opciones.All(o => o.Agotado));
            return new MenuProductoDto(p.ProductoId, p.Nombre, p.Codigo, p.Tipo, p.Descripcion, ps.Precio ?? p.Precio, p.Imagen, p.Etiquetas,
                p.AreaId, agotado,
                !combos ? [] : p.Variantes.Where(v => v.Activo).OrderBy(v => v.Orden).Select(v => new MenuVarianteDto(v.VarianteId, v.Nombre, v.Precio)).ToList(),
                !combos ? [] : p.Grupos.Where(g => g.Grupo!.Activo).OrderBy(g => g.Orden).Select(g => new MenuGrupoDto(g.GrupoId, g.Grupo!.Nombre,
                    g.Grupo.Minimo, g.Grupo.Maximo, g.Grupo.Opciones.Where(o => o.Activo).OrderBy(o => o.Orden)
                        .Select(o => new MenuOpcionDto(o.OpcionId, o.Nombre, o.PrecioExtra)).ToList())).Where(g => g.Opciones.Count > 0).ToList(),
                comboGrupos);
        }

        // Sin tracking cada plato trae su propia copia de la categoría: se agrupa por id.
        var categorias = productos.GroupBy(p => p.CategoriaId).Select(g => (Cat: g.First().Categoria!, Items: g))
            .OrderBy(g => g.Cat.Orden).ThenBy(g => g.Cat.Nombre)
            .Select(g => new MenuCategoriaDto(g.Cat.CategoriaId, g.Cat.Nombre, g.Cat.Color,
                g.Items.OrderBy(p => p.Orden).ThenBy(p => p.Nombre).Select(Mapear).ToList())).ToList();
        return new MenuDto(sid, nombre, combos, categorias);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // AUXILIARES
    // ═════════════════════════════════════════════════════════════════════════
    // Un índice único por nombre se valida fila por fila: intercambiar dos nombres en
    // un solo guardado chocaría. El nombre real se pone en la segunda fase.
    private void Renombrar<T>(T entidad, int id, string actual, string nuevo, Action<T, string> fijar)
    {
        if (actual == nuevo) return;
        fijar(entidad, $"~{id}~{Guid.NewGuid():N}"); // cabe en 60 caracteres
        _nombresFinales.Add(() => fijar(entidad, nuevo));
    }

    private static string ValidarColor(string? color, string defecto)
    {
        var c = Texto.Limpio(color) ?? defecto;
        if (!Color.IsMatch(c)) throw new ReglaNegocioException("El color debe tener el formato #RRGGBB.");
        return c.ToUpperInvariant();
    }

    private static void Reordenar<T>(List<T> lista, List<int>? ids, Func<T, int> id, Action<T, int> fijar)
    {
        if (ids == null || ids.Count == 0) throw new ReglaNegocioException("Indica el nuevo orden.");
        if (ids.Distinct().Count() != ids.Count || ids.Any(i => lista.All(x => id(x) != i)))
            throw new ReglaNegocioException("El orden enviado no coincide con la lista actual.");
        for (var i = 0; i < ids.Count; i++) fijar(lista.First(x => id(x) == ids[i]), i + 1);
    }

    // Borrados antes que altas: los índices únicos (nombre por grupo/plato) no chocan
    // cuando se renombra o se reemplaza una opción en el mismo guardado.
    private async Task GuardarPorPartesAsync()
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            var agregados = _db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToList();
            foreach (var e in agregados) e.State = EntityState.Detached;
            await _db.SaveChangesAsync();                 // 1.ª: borrados, cambios y nombres temporales
            foreach (var fijar in _nombresFinales) fijar();
            _nombresFinales.Clear();
            foreach (var e in agregados) e.State = EntityState.Added;
            await _db.SaveChangesAsync();                 // 2.ª: nombres finales y altas
            await tx.CommitAsync();
        });
    }
}
