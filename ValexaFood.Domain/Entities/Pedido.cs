namespace ValexaFood.Domain.Entities;

public class Mesa : TenantEntity
{
    public int MesaId { get; set; }
    public int SucursalId { get; set; }
    public string Nombre { get; set; } = "";
    public int Capacidad { get; set; } = 4;
    public bool Activa { get; set; } = true;
    public int Orden { get; set; }
    /// <summary>
    /// Marca manual del mesero. LIBRE y OCUPADA no se guardan: se derivan del
    /// pedido abierto, que es la única verdad sobre si la mesa está en uso.
    /// </summary>
    public bool EnLimpieza { get; set; }
    public DateTime? LimpiezaDesde { get; set; }
}

public class Pedido : TenantEntity
{
    public long PedidoId { get; set; }
    public int SucursalId { get; set; }
    public int? MesaId { get; set; }
    public string Canal { get; set; } = "SALON";
    public string Estado { get; set; } = "ABIERTO";
    public string ClaveIdempotencia { get; set; } = "";
    public string? Nota { get; set; }
    public int CreadoPor { get; set; }
    public DateTime CreadoEn { get; set; }
    public DateTime? PagadoEn { get; set; }
}

public class PedidoItem : TenantEntity
{
    public long PedidoItemId { get; set; }
    public long? ComandaId { get; set; }
    public int SucursalId { get; set; }
    public long PedidoId { get; set; }
    public int ProductoId { get; set; }
    public string Nombre { get; set; } = "";
    public int? AreaId { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public string AfectacionIgv { get; set; } = "GRAVADO";
    public string SeleccionJson { get; set; } = "{}";
    public string? Nota { get; set; }
    public string ClaveIdempotencia { get; set; } = "";
    public string Estado { get; set; } = "PENDIENTE";
    /// <summary>
    /// La mesa se queda pero este plato se lo llevan. Cocina lo ve en la comanda
    /// para empacarlo en vez de emplatarlo.
    /// </summary>
    public bool ParaLlevar { get; set; }
    /// <summary>TAPER o BOLSA. Lo que cuesta el táper va como su propia línea.</summary>
    public string? Empaque { get; set; }
    public DateTime CreadoEn { get; set; }
}

/// <summary>Lo que el negocio cobra por empacar.</summary>
public class ConfiguracionEmpaque : TenantEntity
{
    public decimal TaperPrecio { get; set; } = 1.00m;
    public decimal BolsaPrecio { get; set; }
    public int? ActualizadoPor { get; set; }
    public DateTime ActualizadoEn { get; set; }
}

public static class Empaques
{
    public const string Taper = "TAPER";
    public const string Bolsa = "BOLSA";
}

public class Comanda : TenantEntity
{
    public long ComandaId { get; set; }
    public int SucursalId { get; set; }
    public long PedidoId { get; set; }
    public int AreaId { get; set; }
    public string Estado { get; set; } = "PENDIENTE";
    public int EnviadaPor { get; set; }
    public DateTime EnviadaEn { get; set; }
}

// Una línea de combo puede enviar sus componentes a áreas distintas, aunque
// el combo se cobre como un único ítem del pedido.
public class ComandaItem : TenantEntity
{
    public long ComandaItemId { get; set; }
    public int SucursalId { get; set; }
    public long ComandaId { get; set; }
    public long PedidoItemId { get; set; }
    public string Nombre { get; set; } = "";
    public int Cantidad { get; set; }
    public string? Nota { get; set; }
    public string Estado { get; set; } = "PENDIENTE";
}

// Durable print outbox. Payload is a point-in-time snapshot; retries reuse the same job.
public class TrabajoImpresion : TenantEntity
{
    public long TrabajoImpresionId { get; set; }
    public int SucursalId { get; set; }
    public long ComandaId { get; set; }
    public string ClaveIdempotencia { get; set; } = "";
    public string PayloadJson { get; set; } = "{}";
    public string Estado { get; set; } = "PENDIENTE";
    public int Intentos { get; set; }
    public string? UltimoError { get; set; }
    public DateTime CreadoEn { get; set; }
    public DateTime ActualizadoEn { get; set; }
}

public class TrabajoImpresionEvento : TenantEntity
{
    public long TrabajoImpresionEventoId { get; set; }
    public int SucursalId { get; set; }
    public long TrabajoImpresionId { get; set; }
    public string ClaveIdempotencia { get; set; } = "";
    public string Tipo { get; set; } = "";
    public string? Detalle { get; set; }
    public int? RegistradoPor { get; set; }
    public DateTime RegistradoEn { get; set; }
}

public class Pago : TenantEntity
{
    public long PagoId { get; set; }
    public int SucursalId { get; set; }
    public long PedidoId { get; set; }
    public long SesionCajaId { get; set; }
    public string Medio { get; set; } = "EFECTIVO";
    public decimal Monto { get; set; }
    public decimal Recibido { get; set; }
    public decimal Vuelto { get; set; }
    public string? Referencia { get; set; }
    public string ClaveIdempotencia { get; set; } = "";
    public int RegistradoPor { get; set; }
    public DateTime RegistradoEn { get; set; }
}

public class PagoItemAplicacion : TenantEntity
{
    public int SucursalId { get; set; }
    public long PagoId { get; set; }
    public long PedidoItemId { get; set; }
    public int Cantidad { get; set; }
    public decimal Monto { get; set; }
}

// Ledger-only correction. Rows are append-only; never rewrite the source payment/order.
public class AjusteVenta : TenantEntity
{
    public long AjusteVentaId { get; set; }
    public int SucursalId { get; set; }
    public long PedidoId { get; set; }
    public long? PedidoItemId { get; set; }
    public long? PagoId { get; set; }
    public long? SesionCajaId { get; set; }
    public string Tipo { get; set; } = "";
    public decimal Monto { get; set; }
    public string Motivo { get; set; } = "";
    public string ClaveIdempotencia { get; set; } = "";
    public int RegistradoPor { get; set; }
    public DateTime RegistradoEn { get; set; }
}

public class AjusteVentaConfiguracion : TenantEntity
{
    public decimal DescuentoMaximoPct { get; set; } = 20;
    public decimal DescuentoMaximoMonto { get; set; } = 200;
    public int? ActualizadoPor { get; set; }
    public DateTime ActualizadoEn { get; set; }
}
