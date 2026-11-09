namespace ValexaFood.Domain.Entities;

public class ConfiguracionFacturacion : TenantEntity
{
    public string Proveedor { get; set; } = "NUBEFACT_API";
    public string? RutaApi { get; set; }
    public byte[]? TokenCifrado { get; set; }
    public string Modo { get; set; } = "DEMO";
    public decimal IgvTasa { get; set; } = 0.18m;
    public int? ActualizadoPor { get; set; }
    public DateTime ActualizadoEn { get; set; }
}

public class ComprobanteSerie : TenantEntity
{
    public long ComprobanteSerieId { get; set; }
    public int SucursalId { get; set; }
    public string Tipo { get; set; } = "BOLETA";
    public string Serie { get; set; } = "B001";
    public long SiguienteNumero { get; set; } = 1;
    public bool Activa { get; set; } = true;
}

public class ComprobanteVenta : TenantEntity
{
    public long ComprobanteId { get; set; }
    public int SucursalId { get; set; }
    public long PedidoId { get; set; }
    public long PagoId { get; set; }
    public string Tipo { get; set; } = "BOLETA";
    public string Serie { get; set; } = "B001";
    public long Numero { get; set; }
    public string Estado { get; set; } = "PENDIENTE";
    public string Moneda { get; set; } = "PEN";
    public string? ClienteTipoDocumento { get; set; }
    public string? ClienteNumeroDocumento { get; set; }
    public string ClienteNombre { get; set; } = "";
    public string? ClienteDireccion { get; set; }
    public string? ClienteEmail { get; set; }
    public decimal SubtotalGravado { get; set; }
    public decimal SubtotalExonerado { get; set; }
    public decimal SubtotalInafecto { get; set; }
    public decimal Igv { get; set; }
    public decimal Total { get; set; }
    public decimal TasaIgv { get; set; } = 0.18m;
    public string? NubefactRespuesta { get; set; }
    public string? MensajeRespuesta { get; set; }
    public string? XmlUrl { get; set; }
    public string? CdrUrl { get; set; }
    public string? PdfUrl { get; set; }
    public string? HashCpe { get; set; }
    public string? QrContenido { get; set; }
    public string ClaveIdempotencia { get; set; } = "";
    public int EmitidoPor { get; set; }
    public DateTime EmitidoEn { get; set; }
    public DateTime? EnviadoEn { get; set; }
}

public class ComprobanteVentaItem : TenantEntity
{
    public long ComprobanteItemId { get; set; }
    public long ComprobanteId { get; set; }
    public string Descripcion { get; set; } = "";
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public string AfectacionIgv { get; set; } = "GRAVADO";
    public decimal BaseImponible { get; set; }
    public decimal Igv { get; set; }
    public decimal Total { get; set; }
}

public class ComprobanteVentaPago : TenantEntity
{
    public int SucursalId { get; set; }
    public long ComprobanteId { get; set; }
    public long PagoId { get; set; }
    public decimal Monto { get; set; }
}
