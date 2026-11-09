namespace ValexaFood.Domain.Entities;

/// <summary>
/// Una zona de reparto es un nombre y una tarifa que el dueño escribe: "Centro
/// S/5", "La Molina S/9". Sin mapas a propósito — geocodificar direcciones
/// peruanas cuesta por consulta, falla seguido y "portón verde frente a la
/// bodega" no se geocodifica. Ver scripts/29.
/// </summary>
public class ZonaReparto : TenantEntity
{
    public int ZonaId { get; set; }
    public int SucursalId { get; set; }
    public string Nombre { get; set; } = "";
    public decimal CostoEnvio { get; set; }
    public int MinutosEstimados { get; set; } = 30;
    public bool Activa { get; set; } = true;
    public int Orden { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Se reusa entre pedidos: quien pide seguido no vuelve a dictar su dirección.</summary>
public class Cliente : TenantEntity
{
    public int ClienteId { get; set; }
    public string Nombre { get; set; } = "";
    public string Telefono { get; set; } = "";
    public string? Documento { get; set; }
    public string? Nota { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public List<DireccionEntrega> Direcciones { get; set; } = [];
}

public class DireccionEntrega : TenantEntity
{
    public int DireccionId { get; set; }
    public int ClienteId { get; set; }
    public int? ZonaId { get; set; }
    public string Direccion { get; set; } = "";
    /// <summary>Lo que de verdad usa el repartidor para encontrar la puerta.</summary>
    public string? Referencia { get; set; }
    /// <summary>Opcionales: para el día que se sume mapa, sin rediseñar nada.</summary>
    public decimal? Latitud { get; set; }
    public decimal? Longitud { get; set; }
    public bool Activa { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public static class EstadosEnvio
{
    public const string Pendiente = "PENDIENTE";
    public const string Asignado = "ASIGNADO";
    public const string EnRuta = "EN_RUTA";
    public const string Entregado = "ENTREGADO";
    public const string NoEntregado = "NO_ENTREGADO";
}

public static class MomentosPago
{
    /// <summary>Paga en el local, antes de que el pedido salga.</summary>
    public const string Tienda = "TIENDA";
    /// <summary>Paga al recibir. El repartidor declara, el cajero registra.</summary>
    public const string ContraEntrega = "CONTRA_ENTREGA";
}

public class Envio : TenantEntity
{
    public long EnvioId { get; set; }
    public int SucursalId { get; set; }
    public long PedidoId { get; set; }
    public int ClienteId { get; set; }
    public int? DireccionId { get; set; }
    public int? ZonaId { get; set; }
    /// <summary>
    /// Foto de la dirección al momento del pedido: si el cliente la edita
    /// mañana, el envío de ayer conserva a dónde se fue de verdad.
    /// </summary>
    public string DireccionTexto { get; set; } = "";
    public string? Referencia { get; set; }
    public string Telefono { get; set; } = "";
    public decimal CostoEnvio { get; set; }
    public string MomentoPago { get; set; } = MomentosPago.Tienda;
    public int? RepartidorId { get; set; }
    public string Estado { get; set; } = EstadosEnvio.Pendiente;
    /// <summary>
    /// Lo que el repartidor DECLARA haber cobrado. No es el pago: el pago lo
    /// registra el cajero contra su sesión de caja, por el camino de siempre.
    /// </summary>
    public string? CobradoMedio { get; set; }
    public decimal? CobradoMonto { get; set; }
    public bool Rendido { get; set; }
    public string? MotivoNoEntrega { get; set; }
    public DateTime? AsignadoEn { get; set; }
    public DateTime? SalidaEn { get; set; }
    public DateTime? EntregadoEn { get; set; }
    public int CreadoPor { get; set; }
    public DateTime CreadoEn { get; set; }
}
