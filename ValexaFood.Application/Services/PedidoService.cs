using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ValexaFood.Application.Common;
using ValexaFood.Domain.Entities;
using ValexaFood.Infrastructure.Context;
using ValexaFood.Infrastructure.Sesion;

namespace ValexaFood.Application.Services;

public record MesaGuardarDto(string? Nombre, int Capacidad);
/// <param name="Estado">
/// LIBRE · OCUPADA · EN_LIMPIEZA · INACTIVA. Solo EN_LIMPIEZA se guarda; las
/// demás se derivan del pedido abierto, que es la única verdad sobre el uso de
/// la mesa. Guardar "ocupada" sería tener dos verdades capaces de discrepar, y
/// una mesa marcada libre con pedido abierto es plata que se va.
/// </param>
/// <param name="SinEnviar">
/// Platos de esta mesa que nadie está preparando porque todavía no se mandaron.
/// Sin este dato el mapa del salón pinta de verde "lista para servir" una mesa
/// que tiene un plato esperando que alguien pulse Enviar, y el mesero se entera
/// cuando el cliente reclama.
/// </param>
public record MesaDto(int MesaId, string Nombre, int Capacidad, bool Activa, long? PedidoId, string? EstadoPedido,
    decimal Total, string Estado, DateTime? LimpiezaDesde, int SinEnviar = 0);
public record PedidoNuevoDto(int? MesaId, string? Canal, string? Nota, string? ClaveIdempotencia);
public record ModificadorPedidoSeleccionDto(int GrupoId, List<int>? OpcionIds);
public record ComboPedidoOpcionSeleccionDto(int ProductoId, int? VarianteId, List<ModificadorPedidoSeleccionDto>? Modificadores);
public record ComboPedidoGrupoSeleccionDto(int ComboGrupoId, List<ComboPedidoOpcionSeleccionDto>? Opciones);
/// <param name="ParaLlevar">La mesa se queda, pero este plato se lo llevan.</param>
/// <param name="Empaque">TAPER o BOLSA. Solo tiene sentido con ParaLlevar.</param>
public record PedidoItemNuevoDto(int ProductoId, int Cantidad, string? Nota, string? ClaveIdempotencia,
    int? VarianteId = null, List<ModificadorPedidoSeleccionDto>? Modificadores = null, List<ComboPedidoGrupoSeleccionDto>? Combo = null,
    bool ParaLlevar = false, string? Empaque = null);
public record ModificadorPedidoSnapshotDto(int GrupoId, string Grupo, List<OpcionPedidoSnapshotDto> Opciones);
public record OpcionPedidoSnapshotDto(int OpcionId, string Nombre, decimal PrecioExtra);
public record ComboPedidoSnapshotDto(int ComboGrupoId, string Grupo, List<ComboOpcionPedidoSnapshotDto> Opciones);
public record ComboOpcionPedidoSnapshotDto(int ProductoId, string Nombre, int? AreaId, int Cantidad,
    decimal PrecioExtra, int? VarianteId, string? Variante, List<ModificadorPedidoSnapshotDto> Modificadores);
public record PedidoSeleccionSnapshotDto(int? VarianteId, string? Variante,
    List<ModificadorPedidoSnapshotDto> Modificadores, List<ComboPedidoSnapshotDto> Combo);
/// <param name="Enviado">
/// true = esta línea ya no espera salir: o salió con su comanda, o no tiene
/// área que la prepare —el táper, la bolsa, el costo de envío— y el envío la
/// dejó resuelta. Lo que sigue en false es lo que viaja en el próximo envío.
///
/// La regla es la misma que filtra <see cref="EnviarACocinaAsync"/>: si un
/// cargo sin área contara como pendiente, el botón "Enviar N a cocina" mentiría
/// para siempre y el envío siguiente devolvería "no hay nada nuevo que enviar".
/// </param>
public record PedidoItemDto(long PedidoItemId, int ProductoId, string Nombre, int? AreaId, int Cantidad,
    decimal PrecioUnitario, decimal Total, string? Nota, string Estado, string? ResumenSeleccion = null, long? ComandaItemId = null,
    int CantidadPagada = 0, bool Enviado = false, bool ParaLlevar = false, string? Empaque = null);
public record EmpaqueConfigDto(decimal TaperPrecio, decimal BolsaPrecio);
public record PedidoDetalleDto(long PedidoId, int? MesaId, string Canal, string Estado, DateTime CreadoEn,
    string? Nota, decimal Total, List<PedidoItemDto> Items, decimal TotalPagado = 0m,
    decimal SaldoPendiente = 0m, List<PagoDto>? Pagos = null, bool PuedeDividirPorLineas = true,
    decimal Descuento = 0m, decimal TotalDevuelto = 0m, List<AjusteVentaDto>? Ajustes = null);
public record ComandaCocinaDto(long ComandaId, long PedidoId, int AreaId, string Area, string Estado,
    DateTime EnviadaEn, string? Mesa, string Canal, List<PedidoItemDto> Items, TrabajoImpresionDto? Impresion = null);
public record EstadoComandaDto(string Estado);
/// <summary>Sin líneas, entrega todo lo que esté listo en esa mesa.</summary>
public record EntregaMesaDto(List<long>? ItemIds = null);
public record TrabajoImpresionDto(long TrabajoImpresionId, long ComandaId, string Estado, int Intentos,
    string? UltimoError, DateTime CreadoEn, JsonElement Payload);
public record TrabajoImpresionEventoDto(string Tipo, string? Detalle, int? RegistradoPor, DateTime RegistradoEn);
public record SolicitarImpresionDto(string? ClaveIdempotencia);
public record ResultadoImpresionNuevoDto(bool Impresa, string? Detalle, string? ClaveIdempotencia);
public record PagoDto(long PagoId, long PedidoId, string Medio, decimal Monto, decimal Recibido, decimal Vuelto,
    long SesionCajaId, DateTime RegistradoEn, string? Referencia = null,
    List<PagoItemAplicacionDto>? Aplicaciones = null);
public record PagoItemAplicacionDto(long PedidoItemId, string Producto, int Cantidad, decimal Monto);
public record PagoItemAplicacionNuevoDto(long PedidoItemId, int Cantidad);
public record PagoNuevoDto(long SesionCajaId, string? Medio, decimal Recibido, string? ClaveIdempotencia,
    decimal? MontoAplicar = null, List<PagoItemAplicacionNuevoDto>? Aplicaciones = null, string? Referencia = null);
public record AjusteVentaDto(long AjusteVentaId, string Tipo, decimal Monto, string Motivo, long PedidoId,
    long? PagoId, long? SesionCajaId, int RegistradoPor, DateTime RegistradoEn, long? PedidoItemId = null);
public record AjusteVentaNuevoDto(decimal Monto, string? Motivo, string? ClaveIdempotencia, long? SesionCajaId = null);

public class PedidoService
{
    private readonly ValexaFoodContext _db;
    private readonly ISesionActual _sesion;
    private readonly BitacoraService _bitacora;
    private readonly PermisosEfectivos _permisos;
    private readonly InventarioService _inventario;
    private static readonly JsonSerializerOptions JsonWeb = new(JsonSerializerDefaults.Web);
    public PedidoService(ValexaFoodContext db, ISesionActual sesion, BitacoraService bitacora, PermisosEfectivos permisos, InventarioService inventario)
    { _db = db; _sesion = sesion; _bitacora = bitacora; _permisos = permisos; _inventario = inventario; }

    private sealed record ProductoVenta(Producto Producto, ProductoSucursal Sucursal);
    private sealed record SeleccionResuelta(Producto Producto, decimal Precio, PedidoSeleccionSnapshotDto Seleccion);

    private async Task<SeleccionResuelta> ResolverSeleccionAsync(PedidoItemNuevoDto dto)
    {
        var producto = await CargarProductoVentaAsync(dto.ProductoId);
        var permisoPlan = await _permisos.DeSesionAsync(_sesion.RolId, Tenant);
        var puedeConfigurar = permisoPlan.Funciones.Contains(CartaService.FuncionCombos);
        if (!puedeConfigurar && (producto.Producto.Tipo == TiposProducto.Combo || dto.VarianteId != null
            || dto.Modificadores is { Count: > 0 } || dto.Combo is { Count: > 0 }))
            throw new ReglaNegocioException("El plan no permite vender combos, variantes ni modificadores.", 403);

        var precio = PrecioBase(producto, dto.VarianteId, puedeConfigurar, out var varianteNombre);
        var modificadores = puedeConfigurar
            ? ResolverModificadores(producto.Producto, dto.Modificadores)
            : [];
        precio += modificadores.SelectMany(g => g.Opciones).Sum(o => o.PrecioExtra);

        var combos = new List<ComboPedidoSnapshotDto>();
        if (producto.Producto.Tipo == TiposProducto.Combo)
        {
            if (!puedeConfigurar) throw new ReglaNegocioException("El plan no permite vender combos.", 403);
            var grupos = producto.Producto.ComboGrupos.OrderBy(g => g.Orden).ToList();
            var entradas = dto.Combo ?? [];
            if (grupos.Count == 0 || entradas.Count != grupos.Count || entradas.Select(g => g.ComboGrupoId).Distinct().Count() != entradas.Count)
                throw new ReglaNegocioException("Completa todos los grupos de este combo.");
            var componentes = new Dictionary<int, ProductoVenta>();
            foreach (var grupo in grupos)
            {
                var entrada = entradas.SingleOrDefault(g => g.ComboGrupoId == grupo.ComboGrupoId)
                    ?? throw new ReglaNegocioException($"Selecciona las {grupo.Cantidad} opciones de {grupo.Nombre}.");
                var elegidas = entrada.Opciones ?? [];
                if (elegidas.Select(o => o.ProductoId).Distinct().Count() != elegidas.Count)
                    throw new ReglaNegocioException($"No repitas el mismo producto en el grupo {grupo.Nombre}.");
                if (elegidas.Count != grupo.Cantidad)
                    throw new ReglaNegocioException($"El grupo {grupo.Nombre} requiere {grupo.Cantidad} selección(es).");

                var snapshots = new List<ComboOpcionPedidoSnapshotDto>();
                foreach (var elegida in elegidas)
                {
                    var opcion = grupo.Opciones.FirstOrDefault(o => o.ProductoId == elegida.ProductoId)
                        ?? throw new ReglaNegocioException($"La opción seleccionada no pertenece al grupo {grupo.Nombre}.");
                    if (!componentes.TryGetValue(elegida.ProductoId, out var componente))
                    {
                        componente = await CargarProductoVentaAsync(elegida.ProductoId);
                        componentes[elegida.ProductoId] = componente;
                    }
                    if (componente.Producto.Tipo == TiposProducto.Combo)
                        throw new ReglaNegocioException("Un combo no puede incluir otro combo.");

                    var variante = componente.Producto.Variantes.Where(v => v.Activo).OrderBy(v => v.Precio).ThenBy(v => v.VarianteId).ToList();
                    var precioVariante = PrecioBase(componente, elegida.VarianteId, true, out var nombreVariante);
                    var ajusteVariante = variante.Count == 0 ? 0m : precioVariante - variante[0].Precio;
                    var mods = ResolverModificadores(componente.Producto, elegida.Modificadores);
                    var extras = mods.SelectMany(g => g.Opciones).Sum(o => o.PrecioExtra);
                    precio += opcion.PrecioExtra + ajusteVariante + extras;
                    snapshots.Add(new ComboOpcionPedidoSnapshotDto(componente.Producto.ProductoId, componente.Producto.Nombre,
                        componente.Producto.AreaId, 1, opcion.PrecioExtra, elegida.VarianteId, nombreVariante, mods));
                }
                combos.Add(new ComboPedidoSnapshotDto(grupo.ComboGrupoId, grupo.Nombre, snapshots));
            }
        }
        else if (dto.Combo is { Count: > 0 })
        {
            throw new ReglaNegocioException("Este producto no es un combo y no admite opciones de combo.");
        }

        precio = decimal.Round(precio, 2, MidpointRounding.AwayFromZero);
        if (precio < 0 || precio > 99999) throw new ReglaNegocioException("El precio final del producto está fuera del rango permitido.");
        return new SeleccionResuelta(producto.Producto, precio,
            new PedidoSeleccionSnapshotDto(dto.VarianteId, varianteNombre, modificadores, combos));
    }

    private async Task<ProductoVenta> CargarProductoVentaAsync(int productoId)
    {
        var hoy = DiaLima.Hoy();
        var producto = await _db.Productos.AsNoTracking()
            .Include(p => p.Categoria)
            .Include(p => p.Variantes)
            .Include(p => p.Grupos).ThenInclude(g => g.Grupo).ThenInclude(g => g!.Opciones)
            .Include(p => p.ComboGrupos).ThenInclude(g => g.Opciones)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.ProductoId == productoId && p.Activo && p.Categoria!.Activo)
            ?? throw new ReglaNegocioException("El producto no está disponible en la carta de esta sucursal.");
        var sucursal = await _db.ProductoSucursales.AsNoTracking()
            .FirstOrDefaultAsync(s => s.ProductoId == productoId && s.SucursalId == Sucursal)
            ?? throw new ReglaNegocioException("El producto no se vende en esta sucursal.");
        if (sucursal.AgotadoFecha == hoy) throw new ReglaNegocioException($"{producto.Nombre} está agotado hoy en esta sucursal.");
        return new ProductoVenta(producto, sucursal);
    }

    private static decimal PrecioBase(ProductoVenta venta, int? varianteId, bool puedeConfigurar, out string? varianteNombre)
    {
        var variantes = puedeConfigurar ? venta.Producto.Variantes.Where(v => v.Activo).ToList() : [];
        if (variantes.Count == 0)
        {
            if (varianteId != null) throw new ReglaNegocioException("La variante elegida ya no está disponible.");
            varianteNombre = null;
            return venta.Sucursal.Precio ?? venta.Producto.Precio;
        }
        var variante = variantes.FirstOrDefault(v => v.VarianteId == varianteId)
            ?? throw new ReglaNegocioException($"Elige una variante para {venta.Producto.Nombre}.");
        varianteNombre = variante.Nombre;
        return variante.Precio;
    }

    private static List<ModificadorPedidoSnapshotDto> ResolverModificadores(Producto producto, List<ModificadorPedidoSeleccionDto>? entradas)
    {
        var entrada = entradas ?? [];
        if (entrada.Select(g => g.GrupoId).Distinct().Count() != entrada.Count)
            throw new ReglaNegocioException("Un grupo de modificadores está repetido.");
        var grupos = producto.Grupos.Where(g => g.Grupo?.Activo == true).OrderBy(g => g.Orden).ToList();
        if (entrada.Any(e => grupos.All(g => g.GrupoId != e.GrupoId)))
            throw new ReglaNegocioException("Un modificador seleccionado no pertenece a este producto.");
        var resultado = new List<ModificadorPedidoSnapshotDto>();
        foreach (var relacion in grupos)
        {
            var grupo = relacion.Grupo!;
            var ids = entrada.FirstOrDefault(e => e.GrupoId == grupo.GrupoId)?.OpcionIds ?? [];
            if (ids.Distinct().Count() != ids.Count) throw new ReglaNegocioException($"Hay opciones repetidas en {grupo.Nombre}.");
            if (ids.Count < grupo.Minimo || ids.Count > grupo.Maximo)
                throw new ReglaNegocioException(grupo.Minimo == grupo.Maximo
                    ? $"Elige {grupo.Minimo} opción(es) en {grupo.Nombre}."
                    : $"Elige entre {grupo.Minimo} y {grupo.Maximo} opciones en {grupo.Nombre}.");
            var opciones = grupo.Opciones.Where(o => o.Activo && ids.Contains(o.OpcionId)).OrderBy(o => o.Orden).ToList();
            if (opciones.Count != ids.Count) throw new ReglaNegocioException($"Una opción de {grupo.Nombre} ya no está disponible.");
            resultado.Add(new ModificadorPedidoSnapshotDto(grupo.GrupoId, grupo.Nombre,
                opciones.Select(o => new OpcionPedidoSnapshotDto(o.OpcionId, o.Nombre, o.PrecioExtra)).ToList()));
        }
        return resultado;
    }

    public static PedidoSeleccionSnapshotDto LeerSeleccion(string? json)
    {
        try
        {
            var seleccion = JsonSerializer.Deserialize<PedidoSeleccionSnapshotDto>(json ?? "{}", JsonWeb)
                ?? new PedidoSeleccionSnapshotDto(null, null, [], []);
            return seleccion with { Modificadores = seleccion.Modificadores ?? [], Combo = seleccion.Combo ?? [] };
        }
        catch (JsonException) { return new PedidoSeleccionSnapshotDto(null, null, [], []); }
    }

    private static string Canonizar(PedidoItemNuevoDto dto) => JsonSerializer.Serialize(new
    {
        varianteId = dto.VarianteId,
        modificadores = (dto.Modificadores ?? []).OrderBy(g => g.GrupoId)
            .Select(g => new { g.GrupoId, opciones = (g.OpcionIds ?? []).Order().ToArray() }),
        combo = (dto.Combo ?? []).OrderBy(g => g.ComboGrupoId).Select(g => new
        {
            g.ComboGrupoId,
            opciones = (g.Opciones ?? []).OrderBy(o => o.ProductoId).ThenBy(o => o.VarianteId).Select(o => new
            {
                o.ProductoId, o.VarianteId,
                modificadores = (o.Modificadores ?? []).OrderBy(m => m.GrupoId)
                    .Select(m => new { m.GrupoId, opciones = (m.OpcionIds ?? []).Order().ToArray() })
            })
        })
    }, JsonWeb);

    private static string Canonizar(PedidoSeleccionSnapshotDto snapshot) => JsonSerializer.Serialize(new
    {
        varianteId = snapshot.VarianteId,
        modificadores = snapshot.Modificadores.OrderBy(g => g.GrupoId)
            .Select(g => new { g.GrupoId, opciones = g.Opciones.Select(o => o.OpcionId).Order().ToArray() }),
        combo = snapshot.Combo.OrderBy(g => g.ComboGrupoId).Select(g => new
        {
            g.ComboGrupoId,
            opciones = g.Opciones.OrderBy(o => o.ProductoId).ThenBy(o => o.VarianteId).Select(o => new
            {
                o.ProductoId, o.VarianteId,
                modificadores = o.Modificadores.OrderBy(m => m.GrupoId)
                    .Select(m => new { m.GrupoId, opciones = m.Opciones.Select(x => x.OpcionId).Order().ToArray() })
            })
        })
    }, JsonWeb);

    public static string? ResumenSeleccion(PedidoSeleccionSnapshotDto seleccion)
    {
        var partes = new List<string>();
        if (!string.IsNullOrWhiteSpace(seleccion.Variante)) partes.Add(seleccion.Variante);
        partes.AddRange(seleccion.Modificadores.Where(g => g.Opciones.Count > 0)
            .Select(g => $"{g.Grupo}: {string.Join(", ", g.Opciones.Select(o => o.Nombre))}"));
        partes.AddRange(seleccion.Combo.SelectMany(g => g.Opciones.Select(o =>
            $"{g.Grupo}: {o.Nombre}{(o.Variante == null ? "" : $" ({o.Variante})")}" +
            (o.Modificadores.Count == 0 ? "" : $" · {string.Join(", ", o.Modificadores.SelectMany(m => m.Opciones).Select(m => m.Nombre))}"))));
        return partes.Count == 0 ? null : string.Join(" · ", partes);
    }

    public static string DescripcionItem(string nombre, string? seleccionJson)
    {
        var resumen = ResumenSeleccion(LeerSeleccion(seleccionJson));
        var descripcion = string.IsNullOrWhiteSpace(resumen) ? nombre : $"{nombre} ({resumen})";
        return descripcion.Length <= 500 ? descripcion : descripcion[..500];
    }

    private int Tenant => _sesion.TenantId ?? throw new ReglaNegocioException("La sesión no pertenece a una empresa.", 403);
    private int Sucursal => _sesion.SucursalId ?? throw new ReglaNegocioException("Selecciona una sucursal antes de operar.", 428);
    private int Usuario => _sesion.UsuarioId ?? throw new ReglaNegocioException("La sesión no identifica al usuario.", 401);

    public async Task<List<MesaDto>> MesasAsync()
    {
        var mesas = await _db.Mesas.AsNoTracking().Where(m => m.SucursalId == Sucursal)
            .OrderBy(m => m.Orden).ThenBy(m => m.Nombre).ToListAsync();
        var pedidos = await _db.Pedidos.AsNoTracking().Where(p => p.SucursalId == Sucursal && p.MesaId != null
            && (p.Estado == "ABIERTO" || p.Estado == "EN_PREPARACION" || p.Estado == "LISTO")).ToListAsync();
        var ids = pedidos.Select(p => p.PedidoId).ToList();
        var totales = await _db.PedidoItems.AsNoTracking().Where(i => i.SucursalId == Sucursal && ids.Contains(i.PedidoId)
            && i.Estado != "ANULADO").GroupBy(i => i.PedidoId).Select(g => new { Id = g.Key, Total = g.Sum(i => i.Cantidad * i.PrecioUnitario) }).ToListAsync();
        // Lo que todavía no salió a preparación, contando solo lo que alguien
        // cocina: el táper viaja en el mismo envío pero no es un plato.
        var porMandar = await _db.PedidoItems.AsNoTracking().Where(i => i.SucursalId == Sucursal && ids.Contains(i.PedidoId)
            && i.Estado == "PENDIENTE" && i.ComandaId == null && i.AreaId != null)
            .GroupBy(i => i.PedidoId).Select(g => new { Id = g.Key, Cuantos = g.Count() }).ToListAsync();
        return mesas.Select(m =>
        {
            var p = pedidos.FirstOrDefault(x => x.MesaId == m.MesaId);
            var estado = !m.Activa ? "INACTIVA" : p != null ? "OCUPADA" : m.EnLimpieza ? "EN_LIMPIEZA" : "LIBRE";
            return new MesaDto(m.MesaId, m.Nombre, m.Capacidad, m.Activa, p?.PedidoId, p?.Estado,
                p == null ? 0 : totales.FirstOrDefault(x => x.Id == p.PedidoId)?.Total ?? 0,
                estado, m.EnLimpieza ? m.LimpiezaDesde : null,
                p == null ? 0 : porMandar.FirstOrDefault(x => x.Id == p.PedidoId)?.Cuantos ?? 0);
        }).ToList();
    }

    /// <summary>
    /// Deja la mesa en limpieza, o la libera. Es lo único del estado de la mesa
    /// que se guarda: libre y ocupada salen del pedido. La base rechaza mandar a
    /// limpieza una mesa con pedido abierto, y al sentarse alguien la limpieza
    /// se da por terminada sola.
    /// </summary>
    public async Task<List<MesaDto>> MarcarLimpiezaAsync(int mesaId, bool enLimpieza)
    {
        var m = await _db.Mesas.FirstOrDefaultAsync(x => x.MesaId == mesaId && x.SucursalId == Sucursal)
            ?? throw ReglaNegocioException.NoEncontrado("Mesa");
        if (m.EnLimpieza == enLimpieza)
            throw new ReglaNegocioException(enLimpieza ? "La mesa ya está en limpieza." : "La mesa ya está libre.");
        m.EnLimpieza = enLimpieza;
        await _db.SaveChangesAsync();
        await _bitacora.AgregarAsync(Tenant, enLimpieza ? "MESA_EN_LIMPIEZA" : "MESA_LIBERADA", "Mesa", mesaId.ToString(),
            enLimpieza ? $"{m.Nombre} quedó en limpieza." : $"{m.Nombre} volvió a estar libre.");
        await _db.SaveChangesAsync();
        return await MesasAsync();
    }

    public async Task<MesaDto> CrearMesaAsync(MesaGuardarDto dto)
    {
        var nombre = Texto.Requerido(dto.Nombre, "Nombre de mesa", 40, 1);
        if (dto.Capacidad is < 1 or > 100) throw new ReglaNegocioException("La capacidad debe estar entre 1 y 100.");
        if (await _db.Mesas.AnyAsync(m => m.SucursalId == Sucursal && m.Nombre.ToLower() == nombre.ToLower()))
            throw new ReglaNegocioException("Ya existe una mesa con ese nombre en esta sucursal.");
        var mesa = new Mesa { TenantId = Tenant, SucursalId = Sucursal, Nombre = nombre, Capacidad = dto.Capacidad,
            Orden = (await _db.Mesas.Where(m => m.SucursalId == Sucursal).MaxAsync(m => (int?)m.Orden) ?? 0) + 1 };
        _db.Mesas.Add(mesa);
        await _db.SaveChangesAsync();
        return new MesaDto(mesa.MesaId, mesa.Nombre, mesa.Capacidad, mesa.Activa, null, null, 0, "LIBRE", null);
    }

    public async Task<List<MesaDto>> EstadoMesaAsync(int mesaId, bool activa)
    {
        var mesa = await _db.Mesas.FirstOrDefaultAsync(m => m.MesaId == mesaId && m.SucursalId == Sucursal)
            ?? throw ReglaNegocioException.NoEncontrado("Mesa");
        if (!activa && await _db.Pedidos.AnyAsync(p => p.MesaId == mesaId && p.SucursalId == Sucursal
                && (p.Estado == "ABIERTO" || p.Estado == "EN_PREPARACION" || p.Estado == "LISTO")))
            throw new ReglaNegocioException("Cobra o anula el pedido antes de desactivar la mesa.");
        mesa.Activa = activa;
        await _db.SaveChangesAsync();
        return await MesasAsync();
    }

    public async Task<PedidoDetalleDto> CrearPedidoAsync(PedidoNuevoDto dto)
    {
        var canal = (dto.Canal ?? "SALON").Trim().ToUpperInvariant();
        if (canal is not ("SALON" or "MOSTRADOR" or "PARA_LLEVAR" or "DELIVERY"))
            throw new ReglaNegocioException("Canal de pedido no válido.");
        // Un pedido a domicilio no se sienta en una mesa, y además necesita su
        // envío: se toma por /delivery/pedidos, que lo crea completo.
        if (canal == "DELIVERY" && dto.MesaId != null)
            throw new ReglaNegocioException("Un pedido a domicilio no ocupa mesa.");
        if (!Guid.TryParse(dto.ClaveIdempotencia, out var guid)) throw new ReglaNegocioException("Envía una clave de idempotencia válida.");
        var key = guid.ToString("N");
        var nota = Texto.Opcional(dto.Nota, "Nota", 300);
        if (canal == "SALON" && dto.MesaId == null) throw new ReglaNegocioException("Selecciona una mesa para un pedido de salón.");
        if (canal != "SALON" && dto.MesaId != null) throw new ReglaNegocioException("Solo los pedidos de salón se asignan a una mesa.");
        var anterior = await _db.Pedidos.FirstOrDefaultAsync(p => p.SucursalId == Sucursal && p.ClaveIdempotencia == key);
        if (anterior != null) return await RepetirPedidoAsync(anterior, dto.MesaId, canal, nota);
        if (dto.MesaId != null && !await _db.Mesas.AnyAsync(m => m.MesaId == dto.MesaId && m.SucursalId == Sucursal && m.Activa))
            throw ReglaNegocioException.NoEncontrado("Mesa activa");
        var pedido = new Pedido { TenantId = Tenant, SucursalId = Sucursal, MesaId = dto.MesaId, Canal = canal,
            Estado = "ABIERTO", ClaveIdempotencia = key, Nota = nota, CreadoPor = Usuario, CreadoEn = DateTime.UtcNow };
        _db.Pedidos.Add(pedido);
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            var repetido = await _db.Pedidos.FirstOrDefaultAsync(p => p.SucursalId == Sucursal && p.ClaveIdempotencia == key);
            if (repetido != null) return await RepetirPedidoAsync(repetido, dto.MesaId, canal, nota);
            if (dto.MesaId != null)
            {
                var activo = await _db.Pedidos.FirstOrDefaultAsync(p => p.SucursalId == Sucursal && p.MesaId == dto.MesaId
                    && (p.Estado == "ABIERTO" || p.Estado == "EN_PREPARACION" || p.Estado == "LISTO"));
                if (activo != null) return await DetalleAsync(activo.PedidoId);
            }
            throw;
        }
        await _bitacora.AgregarAsync(Tenant, "PEDIDO_CREADO", "Pedido", pedido.PedidoId, $"Pedido #{pedido.PedidoId} creado en sucursal {Sucursal}.");
        await _db.SaveChangesAsync();
        return await DetalleAsync(pedido.PedidoId);
    }

    private Task<PedidoDetalleDto> RepetirPedidoAsync(Pedido previo, int? mesaId, string canal, string? nota)
    {
        if (previo.MesaId != mesaId || previo.Canal != canal || previo.Nota != nota)
            throw new ReglaNegocioException("La clave de idempotencia del pedido ya se usó con datos distintos.");
        return DetalleAsync(previo.PedidoId);
    }

    public async Task<PedidoDetalleDto> AgregarItemAsync(long pedidoId, PedidoItemNuevoDto dto)
    {
        return await EnTransaccionAsync(async () =>
        {
            if (dto.Cantidad is < 1 or > 999) throw new ReglaNegocioException("La cantidad debe estar entre 1 y 999.");
            if (!Guid.TryParse(dto.ClaveIdempotencia, out var itemGuid)) throw new ReglaNegocioException("Envía una clave de idempotencia válida para el ítem.");
            var itemKey = itemGuid.ToString("N");
            var pedido = await BloquearPedidoAsync(pedidoId) ?? throw ReglaNegocioException.NoEncontrado("Pedido");
            var itemPrevio = await _db.PedidoItems.AsNoTracking().FirstOrDefaultAsync(i => i.SucursalId == Sucursal && i.ClaveIdempotencia == itemKey);
            if (itemPrevio != null)
            {
                var anteriorSeleccion = LeerSeleccion(itemPrevio.SeleccionJson);
                if (itemPrevio.PedidoId != pedidoId || itemPrevio.ProductoId != dto.ProductoId || itemPrevio.Cantidad != dto.Cantidad
                    || Texto.Opcional(itemPrevio.Nota, "Nota del plato", 200) != Texto.Opcional(dto.Nota, "Nota del plato", 200)
                    || Canonizar(anteriorSeleccion) != Canonizar(dto))
                    throw new ReglaNegocioException("La clave del ítem ya se usó con datos distintos.");
                return await DetalleAsync(pedidoId);
            }
            // La mesa sigue pidiendo mientras no se cobre: la segunda ronda de
            // tragos, el postre, el plato que faltó. Lo que cierra la cuenta es el
            // cobro, no el primer envío a cocina.
            if (pedido.Estado is "PAGADO" or "ANULADO")
                throw new ReglaNegocioException("La cuenta ya está cerrada: abre un pedido nuevo para esta mesa.");
            var empaque = (dto.Empaque ?? "").Trim().ToUpperInvariant();
            if (empaque.Length > 0 && empaque is not (Empaques.Taper or Empaques.Bolsa))
                throw new ReglaNegocioException("El empaque es táper o bolsa.");
            if (empaque.Length > 0 && !dto.ParaLlevar)
                throw new ReglaNegocioException("El empaque solo aplica a lo que se llevan.");

            var resuelta = await ResolverSeleccionAsync(dto);
            var item = new PedidoItem { TenantId = Tenant, SucursalId = Sucursal, PedidoId = pedidoId,
                ProductoId = resuelta.Producto.ProductoId, Nombre = resuelta.Producto.Nombre, AreaId = resuelta.Producto.AreaId, Cantidad = dto.Cantidad,
                PrecioUnitario = resuelta.Precio, AfectacionIgv = resuelta.Producto.AfectacionIgv,
                SeleccionJson = JsonSerializer.Serialize(resuelta.Seleccion, JsonWeb), Nota = Texto.Opcional(dto.Nota, "Nota del plato", 200),
                ClaveIdempotencia = itemKey, Estado = "PENDIENTE", CreadoEn = DateTime.UtcNow,
                ParaLlevar = dto.ParaLlevar, Empaque = empaque.Length > 0 ? empaque : null };
            _db.PedidoItems.Add(item);
            await _db.SaveChangesAsync();
            if (empaque.Length > 0) await CobrarEmpaqueAsync(pedidoId, empaque, dto.Cantidad);
            return await DetalleAsync(pedidoId);
        });
    }

    public async Task<EmpaqueConfigDto> EmpaqueAsync()
    {
        var c = await _db.ConfiguracionEmpaque.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == Tenant);
        return new(c?.TaperPrecio ?? 1.00m, c?.BolsaPrecio ?? 0m);
    }

    public async Task<EmpaqueConfigDto> GuardarEmpaqueAsync(EmpaqueConfigDto dto)
    {
        if (dto.TaperPrecio is < 0 or > 999 || decimal.Round(dto.TaperPrecio, 2) != dto.TaperPrecio
            || dto.BolsaPrecio is < 0 or > 999 || decimal.Round(dto.BolsaPrecio, 2) != dto.BolsaPrecio)
            throw new ReglaNegocioException("Los precios de empaque deben ser montos válidos con dos decimales.");

        var c = await _db.ConfiguracionEmpaque.FirstOrDefaultAsync(x => x.TenantId == Tenant);
        if (c == null) { c = new ConfiguracionEmpaque { TenantId = Tenant }; _db.ConfiguracionEmpaque.Add(c); }
        c.TaperPrecio = dto.TaperPrecio;
        c.BolsaPrecio = dto.BolsaPrecio;
        c.ActualizadoPor = Usuario;
        c.ActualizadoEn = DateTime.UtcNow;
        await _bitacora.AgregarAsync(Tenant, "EMPAQUE_CONFIGURADO", "Empaque", null,
            $"Táper S/ {dto.TaperPrecio:0.00}, bolsa S/ {dto.BolsaPrecio:0.00}.");
        await _db.SaveChangesAsync();
        return await EmpaqueAsync();
    }

    /// <summary>
    /// El empaque se cobra como una línea más, con un producto interno que no
    /// está en la carta y sin área, así que nadie lo cocina. Igual que el costo
    /// de envío: un cargo que fuera un campo aparte rompería el cuadre entre el
    /// total del pedido y el del comprobante.
    ///
    /// Si el negocio lo tiene en cero —la bolsa, normalmente— no se agrega nada.
    /// </summary>
    private async Task CobrarEmpaqueAsync(long pedidoId, string empaque, int cantidad)
    {
        var cfg = await _db.ConfiguracionEmpaque.AsNoTracking().FirstOrDefaultAsync(c => c.TenantId == Tenant);
        var precio = empaque == Empaques.Taper ? cfg?.TaperPrecio ?? 1.00m : cfg?.BolsaPrecio ?? 0m;
        if (precio <= 0) return;

        var producto = await ProductoEmpaqueAsync();
        _db.PedidoItems.Add(new PedidoItem
        {
            TenantId = Tenant, SucursalId = Sucursal, PedidoId = pedidoId, ProductoId = producto.ProductoId,
            Nombre = empaque == Empaques.Taper ? "Táper" : "Bolsa",
            AreaId = null,            // sin área no genera comanda
            Cantidad = cantidad, PrecioUnitario = precio, AfectacionIgv = producto.AfectacionIgv,
            SeleccionJson = "{}", ClaveIdempotencia = Guid.NewGuid().ToString("N"),
            Estado = "PENDIENTE", CreadoEn = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }

    /// <summary>Producto interno del empaque, creado la primera vez que hace falta.</summary>
    private async Task<Producto> ProductoEmpaqueAsync()
    {
        const string codigo = "__EMPAQUE__";
        var existente = await _db.Productos.FirstOrDefaultAsync(p => p.Codigo == codigo);
        if (existente != null) return existente;

        var categoria = await _db.Categorias.OrderBy(c => c.CategoriaId).FirstOrDefaultAsync()
            ?? throw new ReglaNegocioException("Crea al menos una categoría en la carta antes de cobrar el empaque.");
        var producto = new Producto
        {
            TenantId = Tenant, CategoriaId = categoria.CategoriaId, AreaId = null,
            Tipo = TiposProducto.Plato, Codigo = codigo, Nombre = "Empaque",
            Descripcion = "Táper o bolsa. Producto interno: no se vende en la carta.",
            Precio = 0m, AfectacionIgv = "GRAVADO",
            Activo = false, Orden = 9998, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        _db.Productos.Add(producto);
        await _db.SaveChangesAsync();
        return producto;
    }

    public async Task<PedidoDetalleDto> DetalleAsync(long pedidoId)
    {
        var pedido = await _db.Pedidos.AsNoTracking().FirstOrDefaultAsync(p => p.PedidoId == pedidoId && p.SucursalId == Sucursal)
            ?? throw ReglaNegocioException.NoEncontrado("Pedido");
        var filas = await _db.PedidoItems.AsNoTracking().Where(i => i.PedidoId == pedidoId && i.SucursalId == Sucursal)
            .OrderBy(i => i.PedidoItemId).ToListAsync();
        var pagos = await _db.Pagos.AsNoTracking().Where(p => p.PedidoId == pedidoId && p.SucursalId == Sucursal)
            .OrderBy(p => p.RegistradoEn).ToListAsync();
        var ajustes = await _db.AjustesVenta.AsNoTracking().Where(a => a.PedidoId == pedidoId && a.SucursalId == Sucursal)
            .OrderBy(a => a.RegistradoEn).ToListAsync();
        var aplicaciones = await (from a in _db.PagoItemAplicaciones.AsNoTracking()
                                  join i in _db.PedidoItems.AsNoTracking() on a.PedidoItemId equals i.PedidoItemId
                                  where a.SucursalId == Sucursal && pagos.Select(p => p.PagoId).Contains(a.PagoId)
                                  select new { a.PagoId, a.PedidoItemId, i.Nombre, a.Cantidad, a.Monto }).ToListAsync();
        var pagadoPorItem = aplicaciones.GroupBy(a => a.PedidoItemId).ToDictionary(g => g.Key, g => g.Sum(a => a.Cantidad));
        var items = filas.Select(i => MapearItem(i) with { CantidadPagada = pagadoPorItem.GetValueOrDefault(i.PedidoItemId) }).ToList();
        var bruto = decimal.Round(items.Where(i => i.Estado != "ANULADO").Sum(i => i.Total), 2, MidpointRounding.AwayFromZero);
        var descuento = ajustes.Where(a => a.Tipo == "DESCUENTO").Sum(a => a.Monto);
        var devuelto = ajustes.Where(a => a.Tipo == "DEVOLUCION").Sum(a => a.Monto);
        var total = Math.Max(0m, bruto - descuento);
        var pagado = decimal.Round(pagos.Sum(p => p.Monto) - devuelto, 2, MidpointRounding.AwayFromZero);
        var pagosDto = pagos.Select(p => PagoMap(p, aplicaciones.Where(a => a.PagoId == p.PagoId)
            .Select(a => new PagoItemAplicacionDto(a.PedidoItemId, a.Nombre, a.Cantidad, a.Monto)).ToList())).ToList();
        var puedeDividir = pagos.All(p => aplicaciones.Any(a => a.PagoId == p.PagoId));
        return new PedidoDetalleDto(pedido.PedidoId, pedido.MesaId, pedido.Canal, pedido.Estado, pedido.CreadoEn,
            pedido.Nota, total, items, pagado, pedido.Estado == "PAGADO" ? 0m : Math.Max(0m, total - pagado), pagosDto, puedeDividir,
            descuento, devuelto, ajustes.Select(AjusteMap).ToList());
    }

    public async Task<AjusteVentaDto> AplicarDescuentoAsync(long pedidoId, AjusteVentaNuevoDto dto)
    {
        var motivo = Texto.Requerido(dto.Motivo, "Motivo del descuento", 300, 5);
        var key = ClaveAjuste(dto.ClaveIdempotencia);
        ValidarMontoAjuste(dto.Monto);
        return await EnTransaccionAsync(async () =>
        {
            var previo = await _db.AjustesVenta.AsNoTracking().FirstOrDefaultAsync(a => a.SucursalId == Sucursal && a.ClaveIdempotencia == key);
            if (previo != null)
            {
                if (previo.PedidoId != pedidoId || previo.Tipo != "DESCUENTO" || previo.Monto != dto.Monto || previo.Motivo != motivo)
                    throw new ReglaNegocioException("La clave de idempotencia ya se usó con datos distintos.");
                return AjusteMap(previo);
            }
            var pedido = await BloquearPedidoAsync(pedidoId) ?? throw ReglaNegocioException.NoEncontrado("Pedido");
            previo = await _db.AjustesVenta.AsNoTracking().FirstOrDefaultAsync(a => a.SucursalId == Sucursal && a.ClaveIdempotencia == key);
            if (previo != null)
            {
                if (previo.PedidoId != pedidoId || previo.Tipo != "DESCUENTO" || previo.Monto != dto.Monto || previo.Motivo != motivo)
                    throw new ReglaNegocioException("La clave de idempotencia ya se usó con datos distintos.");
                return AjusteMap(previo);
            }
            if (pedido.Estado is "PAGADO" or "ANULADO") throw new ReglaNegocioException("Solo se descuenta una cuenta activa antes del primer pago.");
            if (await _db.Pagos.AnyAsync(p => p.PedidoId == pedidoId && p.SucursalId == Sucursal))
                throw new ReglaNegocioException("No se aplican descuentos después de iniciar el cobro.");
            var cfg = await _db.AjustesVentaConfiguracion.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == Tenant);
            var bruto = await _db.PedidoItems.Where(i => i.PedidoId == pedidoId && i.SucursalId == Sucursal && i.Estado != "ANULADO")
                .SumAsync(i => (decimal?)(i.Cantidad * i.PrecioUnitario)) ?? 0m;
            var acumulado = await _db.AjustesVenta.Where(a => a.PedidoId == pedidoId && a.SucursalId == Sucursal && a.Tipo == "DESCUENTO")
                .SumAsync(a => (decimal?)a.Monto) ?? 0m;
            var max = Math.Min(bruto * (cfg?.DescuentoMaximoPct ?? 20m) / 100m, cfg?.DescuentoMaximoMonto ?? 200m);
            if (dto.Monto + acumulado > max || dto.Monto + acumulado >= bruto)
                throw new ReglaNegocioException($"El descuento excede el límite configurado o supera S/ {Math.Max(0m, max - acumulado):0.00} disponibles.");
            var ajuste = CrearAjuste(pedidoId, null, null, "DESCUENTO", dto.Monto, motivo, key);
            _db.AjustesVenta.Add(ajuste);
            await _bitacora.AgregarAsync(Tenant, "PEDIDO_DESCUENTO", "Pedido", pedidoId,
                $"Descuento S/ {dto.Monto:0.00} aplicado antes del pago. Motivo: {motivo}.");
            await _db.SaveChangesAsync();
            return AjusteMap(ajuste);
        });
    }

    public async Task<AjusteVentaDto> DevolverPagoAsync(long pagoId, AjusteVentaNuevoDto dto)
    {
        var motivo = Texto.Requerido(dto.Motivo, "Motivo de la devolución", 300, 5);
        var key = ClaveAjuste(dto.ClaveIdempotencia);
        ValidarMontoAjuste(dto.Monto);
        if (!dto.SesionCajaId.HasValue || dto.SesionCajaId.Value <= 0)
            throw new ReglaNegocioException("Selecciona una caja abierta para registrar la devolución.");
        return await EnTransaccionAsync(async () =>
        {
            var previo = await _db.AjustesVenta.AsNoTracking().FirstOrDefaultAsync(a => a.SucursalId == Sucursal && a.ClaveIdempotencia == key);
            if (previo != null)
            {
                if (previo.PagoId != pagoId || previo.Tipo != "DEVOLUCION" || previo.Monto != dto.Monto || previo.Motivo != motivo || previo.SesionCajaId != dto.SesionCajaId)
                    throw new ReglaNegocioException("La clave de idempotencia ya se usó con datos distintos.");
                return AjusteMap(previo);
            }
            var pagoRows = await _db.Pagos.FromSqlInterpolated($"SELECT * FROM pago WHERE tenant_id={Tenant} AND sucursal_id={Sucursal} AND pago_id={pagoId} FOR UPDATE")
                .IgnoreQueryFilters().ToListAsync();
            var pago = pagoRows.FirstOrDefault() ?? throw ReglaNegocioException.NoEncontrado("Pago original");
            // La emisión fiscal bloquea la misma fila de pedido. Esto evita que
            // una devolución y la creación del CPE validen estados incompatibles a la vez.
            var pedido = await BloquearPedidoAsync(pago.PedidoId) ?? throw ReglaNegocioException.NoEncontrado("Pedido original");
            if (pedido.Estado != "PAGADO") throw new ReglaNegocioException("Solo se puede devolver un pago de una venta pagada.");
            previo = await _db.AjustesVenta.AsNoTracking().FirstOrDefaultAsync(a => a.SucursalId == Sucursal && a.ClaveIdempotencia == key);
            if (previo != null)
            {
                if (previo.PagoId != pagoId || previo.Tipo != "DEVOLUCION" || previo.Monto != dto.Monto || previo.Motivo != motivo || previo.SesionCajaId != dto.SesionCajaId)
                    throw new ReglaNegocioException("La clave de idempotencia ya se usó con datos distintos.");
                return AjusteMap(previo);
            }
            var sesiones = await _db.SesionesCaja.FromSqlInterpolated($"SELECT * FROM sesion_caja WHERE tenant_id={Tenant} AND sucursal_id={Sucursal} AND sesion_caja_id={dto.SesionCajaId} AND estado='ABIERTA' FOR UPDATE")
                .IgnoreQueryFilters().ToListAsync();
            if (sesiones.Count == 0) throw new ReglaNegocioException("La devolución requiere una caja abierta de esta sucursal.");
            var comprobantesFiscales = await _db.ComprobantesVenta.AsNoTracking()
                .Where(c => c.PedidoId == pago.PedidoId && c.SucursalId == Sucursal && c.TenantId == Tenant && c.Tipo != "NOTA_VENTA")
                .Select(c => new { c.Estado, c.Serie, c.Numero })
                .ToListAsync();
            if (comprobantesFiscales.Any(c => c.Estado is "PENDIENTE" or "INCIERTO"))
                throw new ReglaNegocioException("El comprobante electrónico todavía no tiene un resultado confirmado. Concilia su estado con el proveedor antes de devolver el pago.");
            if (comprobantesFiscales.Any(c => c.Estado == "ACEPTADO"))
            {
                var cpe = comprobantesFiscales.First(c => c.Estado == "ACEPTADO");
                throw new ReglaNegocioException($"El comprobante {cpe.Serie}-{cpe.Numero:00000000} fue aceptado. Registra primero la nota de crédito fiscal antes de devolver el pago.");
            }
            if (comprobantesFiscales.Any(c => c.Estado is not ("RECHAZADO" or "ERROR")))
                throw new ReglaNegocioException("El estado fiscal de la venta requiere revisión antes de devolver el pago.");
            var devuelto = await _db.AjustesVenta.Where(a => a.PagoId == pagoId && a.SucursalId == Sucursal && a.Tipo == "DEVOLUCION")
                .SumAsync(a => (decimal?)a.Monto) ?? 0m;
            if (dto.Monto + devuelto > pago.Monto) throw new ReglaNegocioException($"El pago admite como máximo S/ {pago.Monto - devuelto:0.00} de devolución.");
            var ajuste = CrearAjuste(pago.PedidoId, pagoId, dto.SesionCajaId, "DEVOLUCION", dto.Monto, motivo, key);
            _db.AjustesVenta.Add(ajuste);
            await _bitacora.AgregarAsync(Tenant, "PAGO_DEVOLUCION", "Pago", pagoId,
                $"Devolución compensatoria S/ {dto.Monto:0.00} en caja {dto.SesionCajaId}. Motivo: {motivo}.");
            await _db.SaveChangesAsync();
            return AjusteMap(ajuste);
        });
    }

    public async Task<AjusteVentaDto> AnularItemAsync(long pedidoId, long pedidoItemId, AjusteVentaNuevoDto dto)
    {
        // Quitar un plato que TODAVÍA NO SE MANDÓ a cocina es corregir el pedido,
        // y eso lo hace el mesero: nadie cocinó nada y no hay plata de por medio.
        // Quitar uno que ya salió a cocina sí necesita supervisión: ahí hay comida
        // hecha y es por donde se escapa la merma. Por eso el permiso se mira
        // contra el estado de la línea, no contra el rol a secas.
        var efectivos = await _permisos.DeSesionAsync(_sesion.RolId, Tenant);
        var puedeAnular = efectivos.Permisos.Contains("PEDIDO_ANULAR_ITEM");
        var puedeTomar = efectivos.Permisos.Contains("PEDIDO_TOMAR");
        if (!puedeAnular && !puedeTomar)
            throw new ReglaNegocioException("Tu rol no puede anular líneas de pedido.", 403);
        var motivo = Texto.Requerido(dto.Motivo, "Motivo de anulación", 300, 5);
        var key = ClaveAjuste(dto.ClaveIdempotencia);
        return await EnTransaccionAsync(async () =>
        {
            var pedido = await BloquearPedidoAsync(pedidoId) ?? throw ReglaNegocioException.NoEncontrado("Pedido");
            var previo = await _db.AjustesVenta.AsNoTracking().FirstOrDefaultAsync(a => a.SucursalId == Sucursal && a.ClaveIdempotencia == key);
            if (previo != null)
            {
                if (previo.PedidoId != pedidoId || previo.PedidoItemId != pedidoItemId || previo.Tipo != "ANULACION_ITEM" || previo.Motivo != motivo)
                    throw new ReglaNegocioException("La clave de idempotencia ya se usó con datos distintos.");
                return AjusteMap(previo);
            }
            if (pedido.Estado is "PAGADO" or "ANULADO") throw new ReglaNegocioException("No se anula una línea de una cuenta pagada o cerrada; registra la devolución del pago.");
            if (await _db.Pagos.AnyAsync(p => p.PedidoId == pedidoId && p.SucursalId == Sucursal))
                throw new ReglaNegocioException("Después del primer pago usa el flujo de devolución compensatoria.");
            if (await _db.AjustesVenta.AnyAsync(a => a.PedidoId == pedidoId && a.SucursalId == Sucursal && a.Tipo == "DESCUENTO"))
                throw new ReglaNegocioException("No se anula una línea con descuento aplicado. Resuelve primero el descuento.");
            var item = await _db.PedidoItems.FirstOrDefaultAsync(i => i.PedidoItemId == pedidoItemId && i.PedidoId == pedidoId && i.SucursalId == Sucursal);
            if (item == null) throw ReglaNegocioException.NoEncontrado("Línea del pedido");
            if (item.Estado == "ANULADO") throw new ReglaNegocioException("La línea ya está anulada.");
            var seMandoACocina = YaSalio(item);
            if (seMandoACocina && !puedeAnular)
                throw new ReglaNegocioException(
                    "Esa línea ya salió a preparación: pídele al cajero o al gerente que la anule.", 403);
            var monto = decimal.Round(item.Cantidad * item.PrecioUnitario, 2, MidpointRounding.AwayFromZero);
            var ajuste = CrearAjuste(pedidoId, null, null, "ANULACION_ITEM", monto, motivo, key, item.PedidoItemId);
            _db.AjustesVenta.Add(ajuste);
            await _bitacora.AgregarAsync(Tenant, "PEDIDO_ITEM_ANULADO", "PedidoItem", item.PedidoItemId,
                $"Línea {item.Nombre} × {item.Cantidad} anulada por S/ {monto:0.00}. Motivo: {motivo}.");
            await _db.SaveChangesAsync();
            item.Estado = "ANULADO";
            var componentes = await _db.ComandaItems.Where(i => i.PedidoItemId == item.PedidoItemId && i.SucursalId == Sucursal).ToListAsync();
            foreach (var componente in componentes) componente.Estado = "ANULADO";
            await _db.SaveChangesAsync();
            if (!await _db.PedidoItems.AnyAsync(i => i.PedidoId == pedidoId && i.SucursalId == Sucursal && i.Estado != "ANULADO"))
                pedido.Estado = "ANULADO";
            await _db.SaveChangesAsync();
            return AjusteMap(ajuste);
        });
    }

    private AjusteVenta CrearAjuste(long pedidoId, long? pagoId, long? sesion, string tipo, decimal monto, string motivo, string key, long? itemId = null) => new()
    {
        TenantId = Tenant, SucursalId = Sucursal, PedidoId = pedidoId, PagoId = pagoId, SesionCajaId = sesion,
        PedidoItemId = itemId, Tipo = tipo, Monto = decimal.Round(monto, 2, MidpointRounding.AwayFromZero), Motivo = motivo,
        ClaveIdempotencia = key, RegistradoPor = Usuario, RegistradoEn = DateTime.UtcNow
    };
    private static AjusteVentaDto AjusteMap(AjusteVenta a) => new(a.AjusteVentaId, a.Tipo, a.Monto, a.Motivo, a.PedidoId, a.PagoId, a.SesionCajaId, a.RegistradoPor, a.RegistradoEn, a.PedidoItemId);
    private static string ClaveAjuste(string? value) => Guid.TryParse(value, out var guid) ? guid.ToString("N") : throw new ReglaNegocioException("Envía una clave de idempotencia válida.");
    private static void ValidarMontoAjuste(decimal monto)
    {
        if (monto <= 0 || monto > 9_999_999_999.99m || decimal.Round(monto, 2) != monto)
            throw new ReglaNegocioException("El importe debe ser positivo, con máximo dos decimales.");
    }

    /// <summary>
    /// La línea ya no espera salir a preparación. Sin comanda nunca salió del
    /// borrador; pero un cargo sin área —táper, bolsa, envío— tampoco recibe
    /// comanda nunca y el envío lo deja en LISTO, así que también está resuelto.
    /// </summary>
    private static bool YaSalio(PedidoItem item) => item.ComandaId != null || item.Estado != "PENDIENTE";

    private static PedidoItemDto MapearItem(PedidoItem item)
    {
        var seleccion = LeerSeleccion(item.SeleccionJson);
        return new PedidoItemDto(item.PedidoItemId, item.ProductoId, item.Nombre, item.AreaId,
            item.Cantidad, item.PrecioUnitario, item.Cantidad * item.PrecioUnitario, item.Nota, item.Estado,
            ResumenSeleccion(seleccion), Enviado: YaSalio(item),
            ParaLlevar: item.ParaLlevar, Empaque: item.Empaque);
    }

    public async Task<PedidoDetalleDto> AnularBorradorAsync(long pedidoId)
    {
        return await EnTransaccionAsync(async () =>
        {
            var pedido = await BloquearPedidoAsync(pedidoId) ?? throw ReglaNegocioException.NoEncontrado("Pedido abierto");
            if (pedido.Estado != "ABIERTO" || await _db.PedidoItems.AnyAsync(i => i.PedidoId == pedidoId && i.SucursalId == Sucursal))
                throw new ReglaNegocioException("Solo se puede liberar un pedido borrador sin ítems.");
            pedido.Estado = "ANULADO";
            await _db.SaveChangesAsync();
            await _bitacora.AgregarAsync(Tenant, "PEDIDO_BORRADOR_ANULADO", "Pedido", pedidoId, $"Borrador vacío #{pedidoId} liberado en sucursal {Sucursal}.");
            await _db.SaveChangesAsync();
            return await DetalleAsync(pedidoId);
        });
    }

    public async Task<PedidoDetalleDto> EnviarACocinaAsync(long pedidoId)
    {
        return await EnTransaccionAsync(async () =>
        {
            var pedido = await BloquearPedidoAsync(pedidoId) ?? throw ReglaNegocioException.NoEncontrado("Pedido abierto");
            if (pedido.Estado is "PAGADO" or "ANULADO")
                throw new ReglaNegocioException("La cuenta ya está cerrada: no se manda nada más a preparación.");
            // Solo lo que todavía no salió. Lo ya mandado tiene su comanda y no se
            // vuelve a cocinar: así la segunda ronda genera comandas nuevas sin
            // duplicar la primera.
            var items = await _db.PedidoItems.Where(i => i.PedidoId == pedidoId && i.SucursalId == Sucursal
                && i.Estado == "PENDIENTE" && i.ComandaId == null).ToListAsync();
            if (items.Count == 0) throw new ReglaNegocioException("No hay nada nuevo que enviar a preparación.");
            var directosPorArea = new Dictionary<int, List<PedidoItem>>();
            var componentesPorArea = new Dictionary<int, List<(PedidoItem Padre, ComboOpcionPedidoSnapshotDto Componente)>>();
            foreach (var item in items)
            {
                var seleccion = LeerSeleccion(item.SeleccionJson);
                if (seleccion.Combo.Count > 0)
                {
                    foreach (var componente in seleccion.Combo.SelectMany(g => g.Opciones).Where(o => o.AreaId.HasValue))
                    {
                        var areaId = componente.AreaId!.Value;
                        if (!componentesPorArea.TryGetValue(areaId, out var lista)) componentesPorArea[areaId] = lista = [];
                        lista.Add((item, componente));
                    }
                }
                else if (item.AreaId.HasValue)
                {
                    var areaId = item.AreaId.Value;
                    if (!directosPorArea.TryGetValue(areaId, out var lista)) directosPorArea[areaId] = lista = [];
                    lista.Add(item);
                }
            }

            var areaIds = directosPorArea.Keys.Union(componentesPorArea.Keys).Distinct().ToList();
            var trabajosImpresion = new List<TrabajoImpresion>();
            foreach (var areaId in areaIds)
                if (!await _db.Areas.AnyAsync(a => a.AreaId == areaId && a.Activo))
                    throw new ReglaNegocioException("Un área de preparación está desactivada; actualiza la carta antes de enviar.");

            foreach (var areaId in areaIds)
            {
                var area = await _db.Areas.FirstAsync(a => a.AreaId == areaId && a.Activo);
                var comanda = new Comanda { TenantId = Tenant, SucursalId = Sucursal, PedidoId = pedidoId, AreaId = area.AreaId,
                    Estado = "PENDIENTE", EnviadaPor = Usuario, EnviadaEn = DateTime.UtcNow };
                _db.Comandas.Add(comanda);
                await _db.SaveChangesAsync();
                if (directosPorArea.TryGetValue(areaId, out var directos))
                    foreach (var item in directos) { item.ComandaId = comanda.ComandaId; item.Estado = "PENDIENTE"; }
                if (componentesPorArea.TryGetValue(areaId, out var componentes))
                    foreach (var (padre, componente) in componentes)
                    {
                        var notas = new List<string> { $"Parte de {padre.Nombre}" };
                        if (!string.IsNullOrWhiteSpace(componente.Variante)) notas.Add(componente.Variante);
                        notas.AddRange(componente.Modificadores.SelectMany(g => g.Opciones.Select(o => $"{g.Grupo}: {o.Nombre}")));
                        if (!string.IsNullOrWhiteSpace(padre.Nota)) notas.Add(padre.Nota);
                        _db.ComandaItems.Add(new ComandaItem { TenantId = Tenant, SucursalId = Sucursal,
                            ComandaId = comanda.ComandaId, PedidoItemId = padre.PedidoItemId,
                            Nombre = componente.Nombre, Cantidad = padre.Cantidad * componente.Cantidad,
                            Nota = string.Join(" · ", notas), Estado = "PENDIENTE" });
                    }
                if (area.ImprimeComanda)
                {
                    var lineasDirectas = directosPorArea.TryGetValue(areaId, out var directosPrint)
                        ? directosPrint.Select(i => new { nombre = i.Nombre, cantidad = i.Cantidad, nota = i.Nota, seleccion = i.SeleccionJson,
                            paraLlevar = i.ParaLlevar, empaque = i.Empaque }).ToList()
                        : [];
                    var lineasCombo = componentesPorArea.TryGetValue(areaId, out var componentesPrint)
                        ? componentesPrint.Select(x => new { nombre = x.Componente.Nombre, cantidad = x.Padre.Cantidad * x.Componente.Cantidad,
                            nota = string.Join(" · ", new[] { x.Componente.Variante }.Concat(x.Componente.Modificadores.SelectMany(g => g.Opciones.Select(o => $"{g.Grupo}: {o.Nombre}"))).Where(t => !string.IsNullOrWhiteSpace(t))),
                            paraLlevar = x.Padre.ParaLlevar, empaque = x.Padre.Empaque }).ToList()
                        : [];
                    trabajosImpresion.Add(new TrabajoImpresion { TenantId = Tenant, SucursalId = Sucursal,
                        ComandaId = comanda.ComandaId, ClaveIdempotencia = $"comanda:{comanda.ComandaId}",
                        PayloadJson = JsonSerializer.Serialize(new { pedidoId, comandaId = comanda.ComandaId, area = area.Nombre,
                            enviadaEn = comanda.EnviadaEn, mesaId = pedido.MesaId, canal = pedido.Canal, lineas = lineasDirectas, componentes = lineasCombo }, JsonWeb),
                        Estado = "PENDIENTE", CreadoEn = DateTime.UtcNow, ActualizadoEn = DateTime.UtcNow });
                }
            }
            var conTrabajo = areaIds.Count > 0;
            foreach (var item in items.Where(i => !i.AreaId.HasValue && LeerSeleccion(i.SeleccionJson).Combo.SelectMany(g => g.Opciones).All(o => !o.AreaId.HasValue)))
                item.Estado = "LISTO";
            // Con rondas, el estado del pedido es el de lo que falta: si quedó algo
            // en cocina vuelve a EN_PREPARACION aunque la ronda anterior ya saliera.
            pedido.Estado = conTrabajo ? "EN_PREPARACION" : "LISTO";
            _db.TrabajosImpresion.AddRange(trabajosImpresion);
            await _db.SaveChangesAsync();
            await _bitacora.AgregarAsync(Tenant, "PEDIDO_ENVIADO_COCINA", "Pedido", pedidoId, $"Pedido #{pedidoId} enviado a preparación.");
            await _db.SaveChangesAsync();
            return await DetalleAsync(pedidoId);
        });
    }

    /// <summary>
    /// Las áreas que le tocan a quien está mirando. Sin asignaciones ve todas:
    /// el restaurante de una sola cocina no tiene nada que configurar, y el que
    /// tiene barra le asigna "Barra" al bartender y deja de ver los ceviches.
    /// </summary>
    private async Task<List<int>?> AreasDelUsuarioAsync()
    {
        var usuarioId = _sesion.UsuarioId;
        if (usuarioId == null) return null;
        var suyas = await _db.UsuarioAreas.AsNoTracking()
            .Where(x => x.UsuarioId == usuarioId.Value).Select(x => x.AreaId).ToListAsync();
        return suyas.Count == 0 ? null : suyas;
    }

    public async Task<List<ComandaCocinaDto>> CocinaAsync()
    {
        var areas = await AreasDelUsuarioAsync();
        var comandas = await _db.Comandas.AsNoTracking().Where(c => c.SucursalId == Sucursal && (c.Estado == "PENDIENTE" || c.Estado == "EN_PREPARACION" || c.Estado == "LISTA")
                && (areas == null || areas.Contains(c.AreaId)))
            .OrderBy(c => c.EnviadaEn).ToListAsync();
        var result = new List<ComandaCocinaDto>();
        foreach (var c in comandas)
        {
            var area = await _db.Areas.AsNoTracking().Where(a => a.AreaId == c.AreaId).Select(a => a.Nombre).FirstOrDefaultAsync() ?? "Área";
            var pedido = await _db.Pedidos.AsNoTracking().FirstAsync(p => p.PedidoId == c.PedidoId && p.SucursalId == Sucursal);
            var mesa = pedido.MesaId == null ? null : await _db.Mesas.AsNoTracking().Where(m => m.MesaId == pedido.MesaId).Select(m => m.Nombre).FirstOrDefaultAsync();
            var directos = await _db.PedidoItems.AsNoTracking().Where(i => i.ComandaId == c.ComandaId && i.SucursalId == Sucursal && i.Estado != "ANULADO")
                .OrderBy(i => i.PedidoItemId).ToListAsync();
            var items = directos.Select(MapearItem).ToList();
            var componentes = await _db.ComandaItems.AsNoTracking().Where(i => i.ComandaId == c.ComandaId && i.SucursalId == Sucursal)
                .OrderBy(i => i.ComandaItemId).ToListAsync();
            foreach (var componente in componentes)
            {
                var padre = await _db.PedidoItems.AsNoTracking().FirstAsync(i => i.PedidoItemId == componente.PedidoItemId && i.SucursalId == Sucursal);
                items.Add(new PedidoItemDto(padre.PedidoItemId, padre.ProductoId, componente.Nombre, c.AreaId,
                    componente.Cantidad, 0m, 0m, componente.Nota, componente.Estado, null, componente.ComandaItemId,
                    ParaLlevar: padre.ParaLlevar, Empaque: padre.Empaque));
            }
            var trabajo = await _db.TrabajosImpresion.AsNoTracking()
                .FirstOrDefaultAsync(t => t.ComandaId == c.ComandaId && t.SucursalId == Sucursal);
            result.Add(new ComandaCocinaDto(c.ComandaId, c.PedidoId, c.AreaId, area, c.Estado, c.EnviadaEn, mesa, pedido.Canal, items,
                trabajo == null ? null : MapearTrabajoImpresion(trabajo)));
        }
        return result;
    }

    public async Task<List<TrabajoImpresionDto>> ColaImpresionAsync()
    {
        var trabajos = await _db.TrabajosImpresion.AsNoTracking()
            .Where(t => t.SucursalId == Sucursal)
            .OrderByDescending(t => t.CreadoEn).Take(100).ToListAsync();
        return trabajos.Select(MapearTrabajoImpresion).ToList();
    }

    public async Task<TrabajoImpresionDto> SolicitarImpresionAsync(long trabajoId, string? claveIdempotencia)
    {
        var key = ClaveAjuste(claveIdempotencia);
        return await EnTransaccionAsync(async () =>
        {
            var existentes = await _db.TrabajosImpresion.FromSqlInterpolated($"""
                SELECT * FROM trabajo_impresion WHERE tenant_id={Tenant} AND sucursal_id={Sucursal}
                  AND trabajo_impresion_id={trabajoId} FOR UPDATE
                """).IgnoreQueryFilters().ToListAsync();
            var trabajo = existentes.FirstOrDefault() ?? throw ReglaNegocioException.NoEncontrado("Trabajo de impresión");
            var evento = await _db.TrabajosImpresionEventos.AsNoTracking()
                .FirstOrDefaultAsync(e => e.TrabajoImpresionId == trabajoId && e.SucursalId == Sucursal
                    && e.ClaveIdempotencia == key);
            if (evento != null) return MapearTrabajoImpresion(trabajo);
            if (trabajo.Estado == "IMPRESA") throw new ReglaNegocioException("La comanda ya fue confirmada como impresa.");
            if (trabajo.Estado == "SOLICITADA") throw new ReglaNegocioException("La solicitud sigue pendiente de resultado; registra el resultado antes de reintentar.");
            trabajo.Estado = "SOLICITADA";
            trabajo.Intentos++;
            trabajo.UltimoError = null;
            _db.TrabajosImpresionEventos.Add(new TrabajoImpresionEvento
            {
                TenantId = Tenant, SucursalId = Sucursal, TrabajoImpresionId = trabajoId,
                ClaveIdempotencia = key, Tipo = "SOLICITUD", Detalle = "Enviada a impresión desde cocina.",
                RegistradoPor = Usuario, RegistradoEn = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
            return MapearTrabajoImpresion(trabajo);
        });
    }

    public async Task<TrabajoImpresionDto> RegistrarResultadoImpresionAsync(long trabajoId, ResultadoImpresionNuevoDto dto)
    {
        var key = ClaveAjuste(dto.ClaveIdempotencia);
        var detalle = string.IsNullOrWhiteSpace(dto.Detalle) ? null : dto.Detalle.Trim();
        if (detalle?.Length > 500) throw new ReglaNegocioException("El detalle admite hasta 500 caracteres.");
        if (!dto.Impresa && string.IsNullOrWhiteSpace(detalle))
            throw new ReglaNegocioException("Indica el motivo del fallo de impresión.");
        return await EnTransaccionAsync(async () =>
        {
            var existentes = await _db.TrabajosImpresion.FromSqlInterpolated($"""
                SELECT * FROM trabajo_impresion WHERE tenant_id={Tenant} AND sucursal_id={Sucursal}
                  AND trabajo_impresion_id={trabajoId} FOR UPDATE
                """).IgnoreQueryFilters().ToListAsync();
            var trabajo = existentes.FirstOrDefault() ?? throw ReglaNegocioException.NoEncontrado("Trabajo de impresión");
            var eventoKey = $"{key}:{(dto.Impresa ? "ok" : "error")}";
            var eventoPrevio = await _db.TrabajosImpresionEventos.AsNoTracking()
                .FirstOrDefaultAsync(e => e.TrabajoImpresionId == trabajoId && e.SucursalId == Sucursal
                    && e.ClaveIdempotencia == eventoKey);
            if (eventoPrevio != null)
            {
                var tipoEsperado = dto.Impresa ? "RESULTADO_OK" : "RESULTADO_ERROR";
                if (eventoPrevio.Tipo != tipoEsperado || eventoPrevio.Detalle != detalle)
                    throw new ReglaNegocioException("La clave del resultado de impresión ya se usó con datos distintos.");
                return MapearTrabajoImpresion(trabajo);
            }
            if (trabajo.Estado != "SOLICITADA")
                throw new ReglaNegocioException("El trabajo no tiene una solicitud de impresión pendiente.");
            trabajo.Estado = dto.Impresa ? "IMPRESA" : "ERROR";
            trabajo.UltimoError = dto.Impresa ? null : detalle;
            _db.TrabajosImpresionEventos.Add(new TrabajoImpresionEvento
            {
                TenantId = Tenant, SucursalId = Sucursal, TrabajoImpresionId = trabajoId,
                ClaveIdempotencia = eventoKey, Tipo = dto.Impresa ? "RESULTADO_OK" : "RESULTADO_ERROR",
                Detalle = detalle, RegistradoPor = Usuario, RegistradoEn = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
            return MapearTrabajoImpresion(trabajo);
        });
    }

    public async Task<List<TrabajoImpresionEventoDto>> HistorialImpresionAsync(long trabajoId)
    {
        if (!await _db.TrabajosImpresion.AnyAsync(t => t.TrabajoImpresionId == trabajoId && t.SucursalId == Sucursal))
            throw ReglaNegocioException.NoEncontrado("Trabajo de impresión");
        return await _db.TrabajosImpresionEventos.AsNoTracking()
            .Where(e => e.TrabajoImpresionId == trabajoId && e.SucursalId == Sucursal)
            .OrderBy(e => e.RegistradoEn)
            .Select(e => new TrabajoImpresionEventoDto(e.Tipo, e.Detalle, e.RegistradoPor, e.RegistradoEn))
            .ToListAsync();
    }

    private static TrabajoImpresionDto MapearTrabajoImpresion(TrabajoImpresion trabajo) => new(
        trabajo.TrabajoImpresionId, trabajo.ComandaId, trabajo.Estado, trabajo.Intentos,
        trabajo.UltimoError, trabajo.CreadoEn, JsonSerializer.Deserialize<JsonElement>(trabajo.PayloadJson, JsonWeb));

    public async Task<List<ComandaCocinaDto>> CambiarEstadoComandaAsync(long comandaId, EstadoComandaDto dto)
    {
        var estado = (dto.Estado ?? "").Trim().ToUpperInvariant();
        if (estado is not ("EN_PREPARACION" or "LISTA" or "ENTREGADA")) throw new ReglaNegocioException("Estado de comanda no válido.");
        var c = await _db.Comandas.FirstOrDefaultAsync(x => x.ComandaId == comandaId && x.SucursalId == Sucursal)
            ?? throw ReglaNegocioException.NoEncontrado("Comanda");
        // Si el usuario tiene áreas asignadas, solo avanza las suyas. No basta con
        // esconderlas de la pantalla: el bartender no marca listo un ceviche.
        var mias = await AreasDelUsuarioAsync();
        if (mias != null && !mias.Contains(c.AreaId))
            throw new ReglaNegocioException("Esa comanda es de otra área de preparación.", 403);
        if (!await _db.PedidoItems.AnyAsync(i => i.ComandaId == comandaId && i.SucursalId == Sucursal && i.Estado != "ANULADO")
            && !await _db.ComandaItems.AnyAsync(i => i.ComandaId == comandaId && i.SucursalId == Sucursal && i.Estado != "ANULADO"))
            throw new ReglaNegocioException("La comanda no tiene líneas activas; conserva el aviso de anulación y no la avances.");
        var permitido = (c.Estado, estado) is ("PENDIENTE", "EN_PREPARACION") or ("PENDIENTE", "LISTA")
            or ("EN_PREPARACION", "LISTA") or ("LISTA", "ENTREGADA");
        if (!permitido) throw new ReglaNegocioException("La comanda no puede avanzar desde su estado actual.");
        c.Estado = estado;
        var items = await _db.PedidoItems.Where(i => i.ComandaId == comandaId && i.SucursalId == Sucursal && i.Estado != "ANULADO").ToListAsync();
        foreach (var item in items) item.Estado = estado switch { "EN_PREPARACION" => "EN_PREPARACION", "LISTA" => "LISTO", _ => "ENTREGADO" };
        var componentes = await _db.ComandaItems.Where(i => i.ComandaId == comandaId && i.SucursalId == Sucursal && i.Estado != "ANULADO").ToListAsync();
        foreach (var item in componentes) item.Estado = estado switch { "EN_PREPARACION" => "EN_PREPARACION", "LISTA" => "LISTO", _ => "ENTREGADO" };
        await _db.SaveChangesAsync();
        foreach (var pedidoItemId in componentes.Select(i => i.PedidoItemId).Distinct())
        {
            var padre = await _db.PedidoItems.FirstAsync(i => i.PedidoItemId == pedidoItemId && i.SucursalId == Sucursal);
            var estados = await _db.ComandaItems.Where(i => i.PedidoItemId == pedidoItemId && i.SucursalId == Sucursal).Select(i => i.Estado).ToListAsync();
            if (estados.Any(e => e == "EN_PREPARACION")) padre.Estado = "EN_PREPARACION";
            else if (estados.Count > 0 && estados.All(e => e is "LISTO" or "ENTREGADO") && padre.Estado is "PENDIENTE" or "EN_PREPARACION") padre.Estado = "LISTO";
        }
        await _db.SaveChangesAsync();
        var otras = await _db.Comandas.Where(x => x.PedidoId == c.PedidoId && x.SucursalId == Sucursal && x.Estado != "LISTA" && x.Estado != "ENTREGADA").AnyAsync();
        var pedido = await _db.Pedidos.FirstAsync(p => p.PedidoId == c.PedidoId && p.SucursalId == Sucursal);
        pedido.Estado = otras ? "EN_PREPARACION" : "LISTO";
        await _db.SaveChangesAsync();
        return await CocinaAsync();
    }

    /// <summary>
    /// El mesero cierra el circuito desde la mesa: lo que cocina dejó LISTO, él
    /// lo lleva. Plato por plato —de cuatro platos casi nunca salen los cuatro
    /// juntos— o todo lo listo de una. La comanda se cierra sola cuando ya no le
    /// queda nada por entregar, así que la pantalla de cocina se limpia sin que
    /// el cocinero vuelva a tocarla.
    /// </summary>
    public async Task<PedidoDetalleDto> EntregarAsync(long pedidoId, EntregaMesaDto dto)
    {
        return await EnTransaccionAsync(async () =>
        {
            var pedido = await BloquearPedidoAsync(pedidoId) ?? throw ReglaNegocioException.NoEncontrado("Pedido");
            if (pedido.Estado == "ANULADO") throw new ReglaNegocioException("El pedido está anulado.");
            var filtro = dto.ItemIds?.Where(x => x > 0).Distinct().ToList();
            var items = await _db.PedidoItems
                .Where(i => i.PedidoId == pedidoId && i.SucursalId == Sucursal && i.Estado == "LISTO").ToListAsync();
            if (filtro is { Count: > 0 }) items = items.Where(i => filtro.Contains(i.PedidoItemId)).ToList();
            if (items.Count == 0) throw new ReglaNegocioException("No hay nada listo para entregar en esta mesa.");

            var ids = items.Select(i => i.PedidoItemId).ToList();
            var componentes = await _db.ComandaItems
                .Where(c => c.SucursalId == Sucursal && ids.Contains(c.PedidoItemId) && c.Estado != "ANULADO").ToListAsync();
            foreach (var i in items) i.Estado = "ENTREGADO";
            foreach (var c in componentes) c.Estado = "ENTREGADO";
            await _db.SaveChangesAsync();

            // Una comanda está entregada cuando no le queda ninguna línea viva sin
            // entregar: ni plato directo ni componente de combo.
            var comandas = items.Where(i => i.ComandaId != null).Select(i => i.ComandaId!.Value)
                .Concat(componentes.Select(c => c.ComandaId)).Distinct().ToList();
            foreach (var comandaId in comandas)
            {
                if (await _db.PedidoItems.AnyAsync(i => i.ComandaId == comandaId && i.SucursalId == Sucursal
                        && i.Estado != "ANULADO" && i.Estado != "ENTREGADO")) continue;
                if (await _db.ComandaItems.AnyAsync(c => c.ComandaId == comandaId && c.SucursalId == Sucursal
                        && c.Estado != "ANULADO" && c.Estado != "ENTREGADO")) continue;
                var comanda = await _db.Comandas.FirstAsync(x => x.ComandaId == comandaId && x.SucursalId == Sucursal);
                comanda.Estado = "ENTREGADA";
            }
            // Entregar no cobra: la cuenta sigue abierta y la mesa puede pedir más.
            if (pedido.Estado is "EN_PREPARACION" or "LISTO")
            {
                var otras = await _db.Comandas.AnyAsync(x => x.PedidoId == pedidoId && x.SucursalId == Sucursal
                    && x.Estado != "LISTA" && x.Estado != "ENTREGADA");
                pedido.Estado = otras ? "EN_PREPARACION" : "LISTO";
            }
            await _db.SaveChangesAsync();
            await _bitacora.AgregarAsync(Tenant, "PEDIDO_ENTREGADO", "Pedido", pedidoId,
                $"{items.Count} línea(s) entregadas a la mesa en el pedido #{pedidoId}.");
            await _db.SaveChangesAsync();
            return await DetalleAsync(pedidoId);
        });
    }

    public async Task<PagoDto> CobrarAsync(long pedidoId, PagoNuevoDto dto)
    {
        var medio = (dto.Medio ?? "").Trim().ToUpperInvariant();
        if (medio is not ("EFECTIVO" or "YAPE" or "PLIN" or "TARJETA" or "TRANSFERENCIA"))
            throw new ReglaNegocioException("Medio de pago no válido.");
        if (!Guid.TryParse(dto.ClaveIdempotencia, out var guid)) throw new ReglaNegocioException("Envía una clave de idempotencia válida.");
        var key = guid.ToString("N");
        if (dto.Recibido < 0 || dto.Recibido > 9_999_999_999.99m || decimal.Round(dto.Recibido, 2) != dto.Recibido)
            throw new ReglaNegocioException("El monto recibido debe ser válido, positivo o cero y tener máximo dos decimales.");
        if (dto.MontoAplicar.HasValue && (dto.MontoAplicar.Value < 0 || dto.MontoAplicar.Value > 9_999_999_999.99m
            || decimal.Round(dto.MontoAplicar.Value, 2) != dto.MontoAplicar.Value))
            throw new ReglaNegocioException("El monto a aplicar debe ser válido y tener máximo dos decimales.");
        var referencia = string.IsNullOrWhiteSpace(dto.Referencia) ? null : dto.Referencia.Trim();
        if (referencia?.Length > 80) throw new ReglaNegocioException("La referencia del pago admite hasta 80 caracteres.");
        var lineasSolicitadas = dto.Aplicaciones ?? [];
        if (lineasSolicitadas.Any(a => a.PedidoItemId <= 0 || a.Cantidad <= 0)
            || lineasSolicitadas.Select(a => a.PedidoItemId).Distinct().Count() != lineasSolicitadas.Count)
            throw new ReglaNegocioException("Selecciona líneas válidas sin repetir y con cantidades mayores a cero.");
        if (lineasSolicitadas.Count > 0 && string.IsNullOrWhiteSpace(referencia))
            throw new ReglaNegocioException("Indica una referencia para identificar a quién corresponde esta cuenta.");
        return await EnTransaccionAsync(async () =>
        {
            var previo = await _db.Pagos.AsNoTracking().FirstOrDefaultAsync(p => p.SucursalId == Sucursal && p.ClaveIdempotencia == key);
            if (previo != null)
            {
                if (previo.PedidoId != pedidoId || previo.SesionCajaId != dto.SesionCajaId || previo.Medio != medio
                    || previo.Recibido != dto.Recibido || (dto.MontoAplicar.HasValue && previo.Monto != dto.MontoAplicar.Value)
                    || previo.Referencia != referencia)
                    throw new ReglaNegocioException("La clave de pago ya se usó con datos distintos.");
                return PagoMap(previo);
            }
            var pedido = await BloquearPedidoAsync(pedidoId) ?? throw ReglaNegocioException.NoEncontrado("Pedido");
            // Repetición concurrente: la primera solicitud pudo confirmar mientras
            // esta esperaba el candado del pedido; vuelve a leer antes del estado PAGADO.
            previo = await _db.Pagos.AsNoTracking().FirstOrDefaultAsync(p => p.SucursalId == Sucursal && p.ClaveIdempotencia == key);
            if (previo != null)
            {
                if (previo.PedidoId != pedidoId || previo.SesionCajaId != dto.SesionCajaId || previo.Medio != medio
                    || previo.Recibido != dto.Recibido || (dto.MontoAplicar.HasValue && previo.Monto != dto.MontoAplicar.Value)
                    || previo.Referencia != referencia)
                    throw new ReglaNegocioException("La clave de pago ya se usó con datos distintos.");
                var aplicacionesPrevias = await _db.PagoItemAplicaciones.AsNoTracking()
                    .Where(a => a.PagoId == previo.PagoId && a.SucursalId == Sucursal)
                    .Select(a => new { a.PedidoItemId, a.Cantidad }).ToListAsync();
                if (aplicacionesPrevias.Count != lineasSolicitadas.Count
                    || lineasSolicitadas.Any(a => !aplicacionesPrevias.Any(x => x.PedidoItemId == a.PedidoItemId && x.Cantidad == a.Cantidad)))
                    throw new ReglaNegocioException("La clave de pago ya se usó con una selección de líneas distinta.");
                return PagoMap(previo);
            }
            if (pedido.Estado is "PAGADO" or "ANULADO") throw new ReglaNegocioException("El pedido ya está cerrado.");
            var turno = await _db.SesionesCaja.FromSqlInterpolated($"""
                SELECT * FROM sesion_caja WHERE tenant_id={Tenant} AND sucursal_id={Sucursal}
                  AND sesion_caja_id={dto.SesionCajaId} AND estado='ABIERTA' FOR UPDATE
                """).IgnoreQueryFilters().FirstOrDefaultAsync() ?? throw new ReglaNegocioException("Abre una caja de esta sucursal antes de cobrar.");
            var total = await _db.PedidoItems.Where(i => i.PedidoId == pedidoId && i.SucursalId == Sucursal && i.Estado != "ANULADO")
                .SumAsync(i => (decimal?)(i.Cantidad * i.PrecioUnitario)) ?? 0m;
            var descuento = await _db.AjustesVenta.Where(a => a.PedidoId == pedidoId && a.SucursalId == Sucursal && a.Tipo == "DESCUENTO")
                .SumAsync(a => (decimal?)a.Monto) ?? 0m;
            total = decimal.Round(total - descuento, 2, MidpointRounding.AwayFromZero);
            total = decimal.Round(total, 2, MidpointRounding.AwayFromZero);
            if (total <= 0) throw new ReglaNegocioException("El pedido no tiene ítems cobrables.");
            var yaPagado = await _db.Pagos.Where(p => p.PedidoId == pedidoId && p.SucursalId == Sucursal)
                .SumAsync(p => (decimal?)p.Monto) ?? 0m;
            var saldo = decimal.Round(total - yaPagado, 2, MidpointRounding.AwayFromZero);
            if (saldo <= 0m) throw new ReglaNegocioException("El pedido ya está pagado.");
            decimal? montoLineas = null;
            if (lineasSolicitadas.Count > 0)
            {
                if (descuento > 0m) throw new ReglaNegocioException("Una cuenta con descuento se cobra como total general; no se divide por líneas.");
                var pagosPrevios = await _db.Pagos.Where(p => p.PedidoId == pedidoId && p.SucursalId == Sucursal).Select(p => p.PagoId).ToListAsync();
                var pagosNoDistribuidos = await _db.Pagos.Where(p => pagosPrevios.Contains(p.PagoId)
                    && !_db.PagoItemAplicaciones.Any(a => a.PagoId == p.PagoId && a.SucursalId == Sucursal)).AnyAsync();
                if (pagosNoDistribuidos) throw new ReglaNegocioException("Este pedido ya tiene abonos sin detalle por línea; continúa con el saldo general.");
                var itemIds = lineasSolicitadas.Select(a => a.PedidoItemId).ToList();
                var seleccion = await _db.PedidoItems.Where(i => itemIds.Contains(i.PedidoItemId) && i.PedidoId == pedidoId
                    && i.SucursalId == Sucursal && i.Estado != "ANULADO").ToListAsync();
                if (seleccion.Count != itemIds.Count) throw new ReglaNegocioException("Una línea seleccionada no pertenece al pedido o está anulada.");
                var cantidadPagada = await _db.PagoItemAplicaciones.Where(a => a.SucursalId == Sucursal && itemIds.Contains(a.PedidoItemId))
                    .GroupBy(a => a.PedidoItemId).Select(g => new { PedidoItemId = g.Key, Cantidad = g.Sum(a => a.Cantidad) })
                    .ToDictionaryAsync(x => x.PedidoItemId, x => x.Cantidad);
                foreach (var linea in lineasSolicitadas)
                {
                    var item = seleccion.Single(i => i.PedidoItemId == linea.PedidoItemId);
                    if (linea.Cantidad > item.Cantidad - cantidadPagada.GetValueOrDefault(item.PedidoItemId))
                        throw new ReglaNegocioException($"La cantidad seleccionada de '{item.Nombre}' supera las unidades pendientes.");
                }
                montoLineas = decimal.Round(lineasSolicitadas.Sum(a => seleccion.Single(i => i.PedidoItemId == a.PedidoItemId).PrecioUnitario * a.Cantidad), 2, MidpointRounding.AwayFromZero);
                if (dto.MontoAplicar.HasValue && dto.MontoAplicar.Value != montoLineas.Value)
                    throw new ReglaNegocioException("El monto enviado no coincide con el total de las líneas seleccionadas.");
            }
            var aplicar = decimal.Round(montoLineas ?? dto.MontoAplicar ?? saldo, 2, MidpointRounding.AwayFromZero);
            if (aplicar <= 0m || aplicar > saldo)
                throw new ReglaNegocioException($"El monto a aplicar debe ser mayor a cero y no superar el saldo de S/ {saldo:0.00}.");
            if (medio == "EFECTIVO")
            {
                if (dto.Recibido < aplicar) throw new ReglaNegocioException("El efectivo recibido no puede ser menor al monto aplicado.");
            }
            else if (dto.Recibido != aplicar)
                throw new ReglaNegocioException("En pagos digitales, el monto recibido debe coincidir con el monto aplicado.");
            var pago = new Pago { TenantId = Tenant, SucursalId = Sucursal, PedidoId = pedidoId, SesionCajaId = turno.SesionCajaId,
                Medio = medio, Monto = aplicar, Recibido = dto.Recibido, Vuelto = medio == "EFECTIVO" ? dto.Recibido - aplicar : 0m, ClaveIdempotencia = key,
                Referencia = referencia, RegistradoPor = Usuario, RegistradoEn = DateTime.UtcNow };
            _db.Pagos.Add(pago);
            var cobroFinal = aplicar == saldo;
            if (cobroFinal) { pedido.Estado = "PAGADO"; pedido.PagadoEn = DateTime.UtcNow; }
            if (cobroFinal)
            {
                var itemsInventario = await _db.PedidoItems.Where(i => i.PedidoId == pedidoId && i.SucursalId == Sucursal).ToListAsync();
                await _inventario.ConsumirPedidoAsync(pedido, itemsInventario);
            }
            await _bitacora.AgregarAsync(Tenant, "PEDIDO_PAGO_REGISTRADO", "Pedido", pedidoId,
                $"Pago de S/ {aplicar:0.00} vía {medio} aplicado al pedido #{pedidoId}; saldo previo S/ {saldo:0.00}.");
            await _db.SaveChangesAsync();
            if (lineasSolicitadas.Count > 0)
            {
                var itemsPago = await _db.PedidoItems.Where(i => lineasSolicitadas.Select(a => a.PedidoItemId).Contains(i.PedidoItemId)
                    && i.PedidoId == pedidoId && i.SucursalId == Sucursal).ToDictionaryAsync(i => i.PedidoItemId);
                _db.PagoItemAplicaciones.AddRange(lineasSolicitadas.Select(a => new PagoItemAplicacion
                {
                    TenantId = Tenant, SucursalId = Sucursal, PagoId = pago.PagoId, PedidoItemId = a.PedidoItemId,
                    Cantidad = a.Cantidad, Monto = decimal.Round(itemsPago[a.PedidoItemId].PrecioUnitario * a.Cantidad, 2, MidpointRounding.AwayFromZero)
                }));
                await _db.SaveChangesAsync();
            }
            return PagoMap(pago);
        });
    }

    private async Task<Pedido?> BloquearPedidoAsync(long id)
    {
        var filas = await _db.Pedidos.FromSqlInterpolated($"SELECT * FROM pedido WHERE tenant_id={Tenant} AND sucursal_id={Sucursal} AND pedido_id={id} FOR UPDATE")
            .IgnoreQueryFilters().ToListAsync();
        return filas.FirstOrDefault();
    }
    private async Task<T> EnTransaccionAsync<T>(Func<Task<T>> accion)
    {
        T? resultado = default;
        var estrategia = _db.Database.CreateExecutionStrategy();
        await estrategia.ExecuteAsync(async () => { _db.ChangeTracker.Clear(); await using var tx = await _db.Database.BeginTransactionAsync(); resultado = await accion(); await tx.CommitAsync(); });
        return resultado!;
    }
    private static PagoDto PagoMap(Pago p, List<PagoItemAplicacionDto>? aplicaciones = null) =>
        new(p.PagoId, p.PedidoId, p.Medio, p.Monto, p.Recibido, p.Vuelto, p.SesionCajaId, p.RegistradoEn, p.Referencia, aplicaciones);
}
