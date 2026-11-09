using Microsoft.EntityFrameworkCore;
using ValexaFood.Application.Common;
using ValexaFood.Domain.Entities;
using ValexaFood.Infrastructure.Context;
using ValexaFood.Infrastructure.Sesion;

namespace ValexaFood.Application.Services;

public record ZonaRepartoDto(int ZonaId, string Nombre, decimal CostoEnvio, int MinutosEstimados, bool Activa, int Orden);
public record ZonaRepartoGuardarDto(string? Nombre, decimal CostoEnvio, int MinutosEstimados, int Orden);

public record DireccionEntregaDto(int DireccionId, int? ZonaId, string? Zona, string Direccion, string? Referencia, bool Activa);
public record DireccionGuardarDto(int? ZonaId, string? Direccion, string? Referencia, decimal? Latitud, decimal? Longitud);

public record ClienteDto(int ClienteId, string Nombre, string Telefono, string? Documento, string? Nota,
    bool Activo, List<DireccionEntregaDto> Direcciones);
public record ClienteGuardarDto(string? Nombre, string? Telefono, string? Documento, string? Nota);

public record EnvioDto(long EnvioId, long PedidoId, string Estado, string Cliente, string Telefono,
    string Direccion, string? Referencia, string? Zona, decimal CostoEnvio, string MomentoPago,
    int? RepartidorId, string? Repartidor, decimal TotalPedido, decimal SaldoPendiente,
    string? CobradoMedio, decimal? CobradoMonto, bool Rendido, string? MotivoNoEntrega,
    DateTime CreadoEn, DateTime? AsignadoEn, DateTime? SalidaEn, DateTime? EntregadoEn);

public record PedidoDeliveryNuevoDto(int? ClienteId, ClienteGuardarDto? Cliente, int? DireccionId,
    DireccionGuardarDto? Direccion, string? MomentoPago, decimal? CostoEnvio, string? Nota, string? ClaveIdempotencia);
public record AsignarRepartidorDto(int RepartidorId);
public record EntregaDto(string? Medio, decimal? Monto);
public record NoEntregaDto(string? Motivo);

/// <summary>
/// Delivery · Fase 1. Ver docs/14-FLUJO-DE-ATENCION.md y scripts/29.
///
/// Dos reglas que sostienen todo lo demás:
///
///  · EL COSTO DE ENVÍO ES UNA LÍNEA DEL PEDIDO, no un campo aparte. Si fuera
///    un campo, el total del pedido y el del comprobante dejarían de coincidir
///    y las tres auditorías de cuadre se caerían.
///
///  · LA PLATA DEL DELIVERY NO ABRE UN CAMINO NUEVO. El repartidor DECLARA lo
///    que cobró; el cobro real lo registra el cajero contra su sesión de caja
///    por el mismo endpoint de siempre. El arqueo sigue cuadrando al céntimo.
/// </summary>
public class DeliveryService
{
    private readonly ValexaFoodContext _db;
    private readonly ISesionActual _sesion;
    private readonly BitacoraService _bitacora;
    private readonly PedidoService _pedidos;

    /// <summary>Código reservado del producto interno con el que se cobra el envío.</summary>
    public const string CodigoEnvio = "__ENVIO__";

    public DeliveryService(ValexaFoodContext db, ISesionActual sesion, BitacoraService bitacora, PedidoService pedidos)
    { _db = db; _sesion = sesion; _bitacora = bitacora; _pedidos = pedidos; }

    private int Tenant => _sesion.TenantId ?? throw new ReglaNegocioException("Esta sección es de los restaurantes.", 403);
    private int Sucursal => _sesion.SucursalId ?? throw new ReglaNegocioException("Elige una sucursal para operar.", 400);
    private int Usuario => _sesion.UsuarioId ?? throw new ReglaNegocioException("Sesión sin usuario.", 403);

    // ═════════════════════════════════════════════════════════════════════════
    // ZONAS
    // ═════════════════════════════════════════════════════════════════════════
    public async Task<List<ZonaRepartoDto>> ZonasAsync(bool soloActivas = false) =>
        await _db.ZonasReparto.AsNoTracking().Where(z => z.SucursalId == Sucursal && (!soloActivas || z.Activa))
            .OrderBy(z => z.Orden).ThenBy(z => z.Nombre)
            .Select(z => new ZonaRepartoDto(z.ZonaId, z.Nombre, z.CostoEnvio, z.MinutosEstimados, z.Activa, z.Orden))
            .ToListAsync();

    public async Task<List<ZonaRepartoDto>> GuardarZonaAsync(int? zonaId, ZonaRepartoGuardarDto dto)
    {
        var nombre = Texto.Requerido(dto.Nombre, "Nombre de la zona", 60, 2);
        if (dto.CostoEnvio is < 0 or > 9999 || decimal.Round(dto.CostoEnvio, 2) != dto.CostoEnvio)
            throw new ReglaNegocioException("El costo de envío debe ser un monto válido con dos decimales.");
        if (dto.MinutosEstimados is < 1 or > 600)
            throw new ReglaNegocioException("El tiempo estimado debe estar entre 1 y 600 minutos.");
        if (await _db.ZonasReparto.AnyAsync(z => z.SucursalId == Sucursal && z.ZonaId != zonaId
                && z.Nombre.ToLower() == nombre.ToLower()))
            throw new ReglaNegocioException($"Ya existe una zona llamada {nombre} en esta sucursal.");

        var zona = zonaId == null
            ? new ZonaReparto { TenantId = Tenant, SucursalId = Sucursal, CreatedAt = DateTime.UtcNow }
            : await _db.ZonasReparto.FirstOrDefaultAsync(z => z.ZonaId == zonaId && z.SucursalId == Sucursal)
              ?? throw ReglaNegocioException.NoEncontrado("Zona de reparto");
        if (zonaId == null) _db.ZonasReparto.Add(zona);
        zona.Nombre = nombre;
        zona.CostoEnvio = dto.CostoEnvio;
        zona.MinutosEstimados = dto.MinutosEstimados;
        zona.Orden = dto.Orden;
        await _bitacora.AgregarAsync(Tenant, zonaId == null ? "ZONA_CREADA" : "ZONA_EDITADA", "ZonaReparto", null,
            $"Zona {nombre}: S/ {dto.CostoEnvio:0.00}, {dto.MinutosEstimados} min.");
        await _db.SaveChangesAsync();
        return await ZonasAsync();
    }

    public async Task<List<ZonaRepartoDto>> EstadoZonaAsync(int zonaId, bool activa)
    {
        var z = await _db.ZonasReparto.FirstOrDefaultAsync(x => x.ZonaId == zonaId && x.SucursalId == Sucursal)
            ?? throw ReglaNegocioException.NoEncontrado("Zona de reparto");
        if (z.Activa == activa) throw new ReglaNegocioException(activa ? "La zona ya está activa." : "La zona ya está desactivada.");
        z.Activa = activa;
        await _bitacora.AgregarAsync(Tenant, activa ? "ZONA_ACTIVADA" : "ZONA_DESACTIVADA", "ZonaReparto", zonaId.ToString(), z.Nombre);
        await _db.SaveChangesAsync();
        return await ZonasAsync();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // CLIENTES Y DIRECCIONES
    // ═════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Busca por teléfono o por nombre. Quien toma el pedido por WhatsApp
    /// escribe el número y le aparece el cliente con sus direcciones: no vuelve
    /// a dictar nada.
    /// </summary>
    public async Task<List<ClienteDto>> BuscarClientesAsync(string? texto, int limite = 20)
    {
        var q = _db.Clientes.AsNoTracking().Include(c => c.Direcciones).Where(c => c.Activo);
        var buscar = (texto ?? "").Trim().ToLowerInvariant();
        if (buscar.Length > 0)
            q = q.Where(c => c.Telefono.Contains(buscar) || c.Nombre.ToLower().Contains(buscar));
        var clientes = await q.OrderBy(c => c.Nombre).Take(Math.Clamp(limite, 1, 50)).ToListAsync();
        var zonas = await _db.ZonasReparto.AsNoTracking().ToDictionaryAsync(z => z.ZonaId, z => z.Nombre);
        return clientes.Select(c => Map(c, zonas)).ToList();
    }

    public async Task<ClienteDto> GuardarClienteAsync(int? clienteId, ClienteGuardarDto dto)
    {
        var nombre = Texto.Requerido(dto.Nombre, "Nombre del cliente", 120, 3);
        var telefono = Documento.NormalizarTelefono(dto.Telefono)
            ?? throw new ReglaNegocioException("El cliente necesita un teléfono: sin él, el repartidor no lo ubica.");
        if (await _db.Clientes.AnyAsync(c => c.ClienteId != clienteId && c.Telefono == telefono))
            throw new ReglaNegocioException($"Ya hay un cliente con el teléfono {telefono}.");

        var cliente = clienteId == null
            ? new Cliente { TenantId = Tenant, CreatedAt = DateTime.UtcNow }
            : await _db.Clientes.Include(c => c.Direcciones).FirstOrDefaultAsync(c => c.ClienteId == clienteId)
              ?? throw ReglaNegocioException.NoEncontrado("Cliente");
        if (clienteId == null) _db.Clientes.Add(cliente);
        cliente.Nombre = nombre;
        cliente.Telefono = telefono;
        cliente.Documento = Documento.NormalizarDni(dto.Documento);
        cliente.Nota = Texto.Opcional(dto.Nota, "Nota del cliente", 300);
        await _bitacora.AgregarAsync(Tenant, clienteId == null ? "CLIENTE_CREADO" : "CLIENTE_EDITADO", "Cliente", null,
            $"{nombre} · {telefono}");
        await _db.SaveChangesAsync();
        return await ClienteAsync(cliente.ClienteId);
    }

    public async Task<ClienteDto> GuardarDireccionAsync(int clienteId, int? direccionId, DireccionGuardarDto dto)
    {
        var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.ClienteId == clienteId)
            ?? throw ReglaNegocioException.NoEncontrado("Cliente");
        var texto = Texto.Requerido(dto.Direccion, "Dirección", 200, 5);
        if (dto.ZonaId != null && !await _db.ZonasReparto.AnyAsync(z => z.ZonaId == dto.ZonaId && z.SucursalId == Sucursal && z.Activa))
            throw new ReglaNegocioException("Esa zona de reparto no existe o está desactivada.");

        var dir = direccionId == null
            ? new DireccionEntrega { TenantId = Tenant, ClienteId = clienteId, CreatedAt = DateTime.UtcNow }
            : await _db.DireccionesEntrega.FirstOrDefaultAsync(d => d.DireccionId == direccionId && d.ClienteId == clienteId)
              ?? throw ReglaNegocioException.NoEncontrado("Dirección");
        if (direccionId == null) _db.DireccionesEntrega.Add(dir);
        dir.Direccion = texto;
        dir.Referencia = Texto.Opcional(dto.Referencia, "Referencia", 300);
        dir.ZonaId = dto.ZonaId;
        dir.Latitud = dto.Latitud;
        dir.Longitud = dto.Longitud;
        await _bitacora.AgregarAsync(Tenant, "DIRECCION_GUARDADA", "Cliente", clienteId.ToString(),
            $"{cliente.Nombre}: {texto}");
        await _db.SaveChangesAsync();
        return await ClienteAsync(clienteId);
    }

    private async Task<ClienteDto> ClienteAsync(int clienteId)
    {
        var c = await _db.Clientes.AsNoTracking().Include(x => x.Direcciones)
            .FirstOrDefaultAsync(x => x.ClienteId == clienteId) ?? throw ReglaNegocioException.NoEncontrado("Cliente");
        var zonas = await _db.ZonasReparto.AsNoTracking().ToDictionaryAsync(z => z.ZonaId, z => z.Nombre);
        return Map(c, zonas);
    }

    private static ClienteDto Map(Cliente c, IReadOnlyDictionary<int, string> zonas) => new(
        c.ClienteId, c.Nombre, c.Telefono, c.Documento, c.Nota, c.Activo,
        c.Direcciones.Where(d => d.Activa).Select(d => new DireccionEntregaDto(
            d.DireccionId, d.ZonaId, d.ZonaId == null ? null : zonas.GetValueOrDefault(d.ZonaId.Value),
            d.Direccion, d.Referencia, d.Activa)).ToList());

    // ═════════════════════════════════════════════════════════════════════════
    // EL PEDIDO A DOMICILIO
    // ═════════════════════════════════════════════════════════════════════════
    public async Task<PedidoDetalleDto> CrearPedidoAsync(PedidoDeliveryNuevoDto dto)
    {
        var momento = (dto.MomentoPago ?? MomentosPago.Tienda).Trim().ToUpperInvariant();
        if (momento is not (MomentosPago.Tienda or MomentosPago.ContraEntrega))
            throw new ReglaNegocioException("Elige si paga en tienda o contra entrega.");

        // El cliente: uno que ya existe, o uno nuevo que se crea de paso.
        var cliente = dto.ClienteId != null
            ? await _db.Clientes.FirstOrDefaultAsync(c => c.ClienteId == dto.ClienteId)
              ?? throw ReglaNegocioException.NoEncontrado("Cliente")
            : null;
        if (cliente == null)
        {
            if (dto.Cliente == null) throw new ReglaNegocioException("Elige un cliente o registra uno nuevo.");
            var creado = await GuardarClienteAsync(null, dto.Cliente);
            cliente = await _db.Clientes.FirstAsync(c => c.ClienteId == creado.ClienteId);
        }

        // La dirección: una guardada, o uno nuevo que queda para la próxima vez.
        DireccionEntrega? direccion = null;
        if (dto.DireccionId != null)
            direccion = await _db.DireccionesEntrega.FirstOrDefaultAsync(d => d.DireccionId == dto.DireccionId && d.ClienteId == cliente.ClienteId)
                ?? throw ReglaNegocioException.NoEncontrado("Dirección del cliente");
        else if (dto.Direccion != null)
        {
            await GuardarDireccionAsync(cliente.ClienteId, null, dto.Direccion);
            direccion = await _db.DireccionesEntrega.Where(d => d.ClienteId == cliente.ClienteId && d.Activa)
                .OrderByDescending(d => d.DireccionId).FirstAsync();
        }
        if (direccion == null) throw new ReglaNegocioException("El pedido a domicilio necesita una dirección de entrega.");

        var zona = direccion.ZonaId == null ? null
            : await _db.ZonasReparto.FirstOrDefaultAsync(z => z.ZonaId == direccion.ZonaId && z.SucursalId == Sucursal);
        // El costo sale de la zona, pero se puede pisar: una promoción, o una
        // dirección lejos dentro de la misma zona.
        var costo = dto.CostoEnvio ?? zona?.CostoEnvio ?? 0m;
        if (costo < 0 || decimal.Round(costo, 2) != costo)
            throw new ReglaNegocioException("El costo de envío debe ser un monto válido con dos decimales.");

        var pedido = await _pedidos.CrearPedidoAsync(new PedidoNuevoDto(null, "DELIVERY", dto.Nota, dto.ClaveIdempotencia));

        var envio = new Envio
        {
            TenantId = Tenant, SucursalId = Sucursal, PedidoId = pedido.PedidoId, ClienteId = cliente.ClienteId,
            DireccionId = direccion.DireccionId, ZonaId = zona?.ZonaId,
            DireccionTexto = direccion.Direccion, Referencia = direccion.Referencia, Telefono = cliente.Telefono,
            CostoEnvio = costo, MomentoPago = momento, Estado = EstadosEnvio.Pendiente,
            CreadoPor = Usuario, CreadoEn = DateTime.UtcNow,
        };
        _db.Envios.Add(envio);
        await _db.SaveChangesAsync();

        if (costo > 0) await AgregarLineaEnvioAsync(pedido.PedidoId, zona?.Nombre, costo);

        await _bitacora.AgregarAsync(Tenant, "DELIVERY_CREADO", "Envio", envio.EnvioId,
            $"Pedido #{pedido.PedidoId} a domicilio para {cliente.Nombre}: {direccion.Direccion}. " +
            $"Envío S/ {costo:0.00}, {(momento == MomentosPago.Tienda ? "paga en tienda" : "contra entrega")}.");
        await _db.SaveChangesAsync();
        return await _pedidos.DetalleAsync(pedido.PedidoId);
    }

    /// <summary>
    /// El envío se cobra como una línea más del pedido, con un producto interno
    /// que no está en la carta. Así el total, el IGV, el comprobante y el arqueo
    /// funcionan sin que ninguno tenga que saber qué es un delivery.
    /// </summary>
    private async Task AgregarLineaEnvioAsync(long pedidoId, string? zona, decimal costo)
    {
        var producto = await ProductoEnvioAsync();
        _db.PedidoItems.Add(new PedidoItem
        {
            TenantId = Tenant, SucursalId = Sucursal, PedidoId = pedidoId, ProductoId = producto.ProductoId,
            Nombre = zona == null ? "Delivery" : $"Delivery · {zona}",
            AreaId = null,            // sin área no genera comanda: nadie lo cocina
            Cantidad = 1, PrecioUnitario = costo, AfectacionIgv = producto.AfectacionIgv,
            SeleccionJson = "{}", ClaveIdempotencia = Guid.NewGuid().ToString("N"),
            Estado = "PENDIENTE", CreadoEn = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }

    /// <summary>El producto interno del envío, creado la primera vez que hace falta.</summary>
    private async Task<Producto> ProductoEnvioAsync()
    {
        var existente = await _db.Productos.FirstOrDefaultAsync(p => p.Codigo == CodigoEnvio);
        if (existente != null) return existente;

        var categoria = await _db.Categorias.OrderBy(c => c.CategoriaId).FirstOrDefaultAsync()
            ?? throw new ReglaNegocioException("Crea al menos una categoría en la carta antes de cobrar envíos.");
        var producto = new Producto
        {
            TenantId = Tenant, CategoriaId = categoria.CategoriaId, AreaId = null,
            Tipo = TiposProducto.Plato, Codigo = CodigoEnvio, Nombre = "Delivery",
            Descripcion = "Costo de envío. Producto interno: no se vende en la carta.",
            Precio = 0m, AfectacionIgv = "GRAVADO",
            // Inactivo a propósito: nunca aparece en el menú, y el envío se agrega
            // por este servicio, no eligiéndolo de la carta.
            Activo = false, Orden = 9999, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        _db.Productos.Add(producto);
        await _db.SaveChangesAsync();
        return producto;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // DESPACHO Y RUTA
    // ═════════════════════════════════════════════════════════════════════════
    /// <summary>La pizarra del despacho: todo lo que no está cerrado.</summary>
    public Task<List<EnvioDto>> PendientesAsync() =>
        ListarAsync(e => e.Estado != EstadosEnvio.Entregado && e.Estado != EstadosEnvio.NoEntregado);

    /// <summary>Lo del repartidor que está mirando: solo lo suyo y sin cerrar.</summary>
    public Task<List<EnvioDto>> MisEnviosAsync() =>
        ListarAsync(e => e.RepartidorId == Usuario
            && (e.Estado == EstadosEnvio.Asignado || e.Estado == EstadosEnvio.EnRuta));

    /// <summary>Lo entregado con cobro declarado que el cajero todavía no registró.</summary>
    public Task<List<EnvioDto>> PorRendirAsync() =>
        ListarAsync(e => e.Estado == EstadosEnvio.Entregado && !e.Rendido && e.CobradoMonto != null);

    public async Task<EnvioDto> AsignarAsync(long envioId, AsignarRepartidorDto dto)
    {
        var e = await CargarAsync(envioId);
        if (e.Estado is not (EstadosEnvio.Pendiente or EstadosEnvio.Asignado))
            throw new ReglaNegocioException("Ese envío ya salió o ya se cerró.");
        var repartidor = await _db.Usuarios.FirstOrDefaultAsync(u => u.UsuarioId == dto.RepartidorId && u.Activo)
            ?? throw ReglaNegocioException.NoEncontrado("Repartidor");
        if (repartidor.RolId != RolIds.Delivery)
            throw new ReglaNegocioException("Solo se le asigna un envío a alguien con rol Delivery.");

        e.RepartidorId = repartidor.UsuarioId;
        e.Estado = EstadosEnvio.Asignado;
        e.AsignadoEn = DateTime.UtcNow;
        await _bitacora.AgregarAsync(Tenant, "DELIVERY_ASIGNADO", "Envio", envioId,
            $"Envío #{envioId} asignado a {repartidor.Nombre}.");
        await _db.SaveChangesAsync();
        return await UnoAsync(envioId);
    }

    /// <summary>
    /// El repartidor sale. Puede salir con uno o con varios a la vez, del mismo
    /// cliente o de clientes distintos: por eso recibe una lista.
    /// </summary>
    public async Task<List<EnvioDto>> SalirAsync(List<long> envioIds)
    {
        if (envioIds == null || envioIds.Count == 0) throw new ReglaNegocioException("Elige al menos un envío para salir.");
        foreach (var id in envioIds.Distinct())
        {
            var e = await CargarAsync(id);
            if (e.RepartidorId != Usuario)
                throw new ReglaNegocioException($"El envío #{id} no está asignado a ti.", 403);
            if (e.Estado != EstadosEnvio.Asignado)
                throw new ReglaNegocioException($"El envío #{id} no está listo para salir.");
            e.Estado = EstadosEnvio.EnRuta;
            e.SalidaEn = DateTime.UtcNow;
        }
        await _bitacora.AgregarAsync(Tenant, "DELIVERY_EN_RUTA", "Envio", null,
            $"{envioIds.Distinct().Count()} envío(s) salieron a ruta.");
        await _db.SaveChangesAsync();
        return await MisEnviosAsync();
    }

    /// <summary>
    /// Entrega. Si el pedido era contra entrega, el repartidor DECLARA qué cobró
    /// y cuánto — eso no es el pago: queda pendiente de rendir ante el cajero,
    /// que lo registra contra su caja. La plata entra cuando llega de verdad.
    /// </summary>
    public async Task<EnvioDto> EntregarAsync(long envioId, EntregaDto dto)
    {
        var e = await CargarAsync(envioId);
        if (e.RepartidorId != Usuario && _sesion.RolId == RolIds.Delivery)
            throw new ReglaNegocioException("Ese envío no está asignado a ti.", 403);
        if (e.Estado != EstadosEnvio.EnRuta) throw new ReglaNegocioException("Marca primero la salida a ruta.");

        if (e.MomentoPago == MomentosPago.ContraEntrega)
        {
            var medio = (dto.Medio ?? "").Trim().ToUpperInvariant();
            if (medio is not ("EFECTIVO" or "YAPE" or "PLIN" or "TARJETA" or "TRANSFERENCIA"))
                throw new ReglaNegocioException("Di con qué te pagaron: efectivo, Yape, Plin, tarjeta o transferencia.");
            var monto = dto.Monto ?? 0m;
            if (monto <= 0 || decimal.Round(monto, 2) != monto)
                throw new ReglaNegocioException("Declara cuánto cobraste, con dos decimales.");
            var detalle = await _pedidos.DetalleAsync(e.PedidoId);
            if (monto > detalle.SaldoPendiente)
                throw new ReglaNegocioException($"Estás declarando más de lo que se debe (S/ {detalle.SaldoPendiente:0.00}).");
            e.CobradoMedio = medio;
            e.CobradoMonto = monto;
        }

        e.Estado = EstadosEnvio.Entregado;
        e.EntregadoEn = DateTime.UtcNow;
        await _bitacora.AgregarAsync(Tenant, "DELIVERY_ENTREGADO", "Envio", envioId,
            e.CobradoMonto == null
                ? $"Envío #{envioId} entregado."
                : $"Envío #{envioId} entregado; el repartidor declara S/ {e.CobradoMonto:0.00} en {e.CobradoMedio}.");
        await _db.SaveChangesAsync();
        return await UnoAsync(envioId);
    }

    public async Task<EnvioDto> NoEntregadoAsync(long envioId, NoEntregaDto dto)
    {
        var e = await CargarAsync(envioId);
        if (e.Estado is EstadosEnvio.Entregado or EstadosEnvio.NoEntregado)
            throw new ReglaNegocioException("Ese envío ya está cerrado.");
        e.MotivoNoEntrega = Texto.Requerido(dto.Motivo, "Motivo", 300, 5);
        e.Estado = EstadosEnvio.NoEntregado;
        await _bitacora.AgregarAsync(Tenant, "DELIVERY_NO_ENTREGADO", "Envio", envioId,
            $"Envío #{envioId} no entregado. Motivo: {e.MotivoNoEntrega}.");
        await _db.SaveChangesAsync();
        return await UnoAsync(envioId);
    }

    /// <summary>
    /// El cajero recibe la plata del repartidor y la registra contra su caja por
    /// el endpoint de cobro de siempre. Este método NO mueve dinero: solo marca
    /// que la rendición ya ocurrió, para que no se cobre dos veces.
    /// </summary>
    public async Task<EnvioDto> MarcarRendidoAsync(long envioId)
    {
        var e = await CargarAsync(envioId);
        if (e.Estado != EstadosEnvio.Entregado) throw new ReglaNegocioException("Solo se rinde un envío entregado.");
        if (e.Rendido) throw new ReglaNegocioException("Ese envío ya fue rendido.");
        if (e.CobradoMonto == null) throw new ReglaNegocioException("Ese envío no tiene cobro declarado: no hay nada que rendir.");
        var detalle = await _pedidos.DetalleAsync(e.PedidoId);
        if (detalle.SaldoPendiente > 0)
            throw new ReglaNegocioException(
                $"Registra primero el cobro del pedido #{e.PedidoId} contra tu caja: quedan S/ {detalle.SaldoPendiente:0.00} sin pagar.");

        e.Rendido = true;
        await _bitacora.AgregarAsync(Tenant, "DELIVERY_RENDIDO", "Envio", envioId,
            $"Envío #{envioId} rendido: S/ {e.CobradoMonto:0.00} en {e.CobradoMedio} entraron a caja.");
        await _db.SaveChangesAsync();
        return await UnoAsync(envioId);
    }

    // ═════════════════════════════════════════════════════════════════════════
    private async Task<Envio> CargarAsync(long envioId) =>
        await _db.Envios.FirstOrDefaultAsync(e => e.EnvioId == envioId && e.SucursalId == Sucursal)
        ?? throw ReglaNegocioException.NoEncontrado("Envío");

    private async Task<EnvioDto> UnoAsync(long envioId) =>
        (await ListarAsync(e => e.EnvioId == envioId)).FirstOrDefault()
        ?? throw ReglaNegocioException.NoEncontrado("Envío");

    private async Task<List<EnvioDto>> ListarAsync(System.Linq.Expressions.Expression<Func<Envio, bool>> filtro)
    {
        var envios = await _db.Envios.AsNoTracking().Where(e => e.SucursalId == Sucursal).Where(filtro)
            .OrderBy(e => e.Estado == EstadosEnvio.Pendiente ? 0 : 1).ThenBy(e => e.CreadoEn).ToListAsync();
        if (envios.Count == 0) return [];

        var pedidoIds = envios.Select(e => e.PedidoId).ToList();
        var clienteIds = envios.Select(e => e.ClienteId).Distinct().ToList();
        var repartidorIds = envios.Where(e => e.RepartidorId != null).Select(e => e.RepartidorId!.Value).Distinct().ToList();
        var zonaIds = envios.Where(e => e.ZonaId != null).Select(e => e.ZonaId!.Value).Distinct().ToList();

        var clientes = await _db.Clientes.AsNoTracking().Where(c => clienteIds.Contains(c.ClienteId))
            .ToDictionaryAsync(c => c.ClienteId, c => c.Nombre);
        var repartidores = await _db.Usuarios.AsNoTracking().IgnoreQueryFilters()
            .Where(u => repartidorIds.Contains(u.UsuarioId)).ToDictionaryAsync(u => u.UsuarioId, u => u.Nombre);
        var zonas = await _db.ZonasReparto.AsNoTracking().Where(z => zonaIds.Contains(z.ZonaId))
            .ToDictionaryAsync(z => z.ZonaId, z => z.Nombre);

        // El total y el saldo salen del pedido, que es la única verdad de la cuenta.
        var lineas = await _db.PedidoItems.AsNoTracking()
            .Where(i => i.SucursalId == Sucursal && pedidoIds.Contains(i.PedidoId) && i.Estado != "ANULADO")
            .GroupBy(i => i.PedidoId)
            .Select(g => new { PedidoId = g.Key, Total = g.Sum(i => i.Cantidad * i.PrecioUnitario) }).ToListAsync();
        var pagado = await _db.Pagos.AsNoTracking()
            .Where(p => p.SucursalId == Sucursal && pedidoIds.Contains(p.PedidoId))
            .GroupBy(p => p.PedidoId).Select(g => new { PedidoId = g.Key, Monto = g.Sum(p => p.Monto) }).ToListAsync();

        return envios.Select(e =>
        {
            var total = lineas.FirstOrDefault(x => x.PedidoId == e.PedidoId)?.Total ?? 0m;
            var cobrado = pagado.FirstOrDefault(x => x.PedidoId == e.PedidoId)?.Monto ?? 0m;
            return new EnvioDto(e.EnvioId, e.PedidoId, e.Estado,
                clientes.GetValueOrDefault(e.ClienteId, ""), e.Telefono, e.DireccionTexto, e.Referencia,
                e.ZonaId == null ? null : zonas.GetValueOrDefault(e.ZonaId.Value), e.CostoEnvio, e.MomentoPago,
                e.RepartidorId, e.RepartidorId == null ? null : repartidores.GetValueOrDefault(e.RepartidorId.Value),
                total, Math.Max(0m, total - cobrado),
                e.CobradoMedio, e.CobradoMonto, e.Rendido, e.MotivoNoEntrega,
                e.CreadoEn, e.AsignadoEn, e.SalidaEn, e.EntregadoEn);
        }).ToList();
    }
}
