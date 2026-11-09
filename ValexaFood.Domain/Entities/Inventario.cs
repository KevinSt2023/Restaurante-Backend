namespace ValexaFood.Domain.Entities;

public static class UnidadesInventario
{
    public const string Gramo = "G", Mililitro = "ML", Unidad = "UN";
    public static readonly string[] Todos = [Gramo, Mililitro, Unidad];
}

public class Insumo : TenantEntity
{
    public int InsumoId { get; set; }
    public string? Codigo { get; set; }
    public string Nombre { get; set; } = "";
    public string UnidadBase { get; set; } = UnidadesInventario.Unidad;
    public string UnidadCompra { get; set; } = UnidadesInventario.Unidad;
    public decimal FactorCompraABase { get; set; } = 1m;
    public bool Activo { get; set; } = true;
    public DateTime CreadoEn { get; set; }
    public DateTime ActualizadoEn { get; set; }
}

public class InsumoSucursal : TenantEntity
{
    public int SucursalId { get; set; }
    public int InsumoId { get; set; }
    public decimal StockActualBase { get; set; }
    public decimal StockMinimoBase { get; set; }
    public decimal StockObjetivoBase { get; set; }
    public decimal CostoPromedioBase { get; set; }
    public bool CostoPromedioConfigurado { get; set; }
    public DateTime ActualizadoEn { get; set; }
}

// Un renglón base se combina con los incrementos de variante o adicional elegido.
public class RecetaInsumo : TenantEntity
{
    public long RecetaInsumoId { get; set; }
    public int ProductoId { get; set; }
    public int? VarianteId { get; set; }
    public int? OpcionId { get; set; }
    public int InsumoId { get; set; }
    public decimal CantidadBase { get; set; }
    public DateTime ActualizadoEn { get; set; }
}

// Ledger append-only; CantidadDeltaBase es positiva para entradas y negativa para salidas.
public class MovimientoInsumo : TenantEntity
{
    public long MovimientoInsumoId { get; set; }
    public int SucursalId { get; set; }
    public int InsumoId { get; set; }
    public string Tipo { get; set; } = "";
    public decimal CantidadDeltaBase { get; set; }
    public decimal? CostoUnitarioBase { get; set; }
    public decimal? CostoTotal { get; set; }
    public bool CostoConfigurado { get; set; }
    public decimal CantidadRegistrada { get; set; }
    public string UnidadRegistradaSnapshot { get; set; } = "";
    public string NombreInsumoSnapshot { get; set; } = "";
    public string UnidadBaseSnapshot { get; set; } = "";
    public string? Motivo { get; set; }
    public string? Referencia { get; set; }
    public long? PedidoId { get; set; }
    public long? PedidoItemId { get; set; }
    public long? TrasladoId { get; set; }
    public long? TrasladoItemId { get; set; }
    public long? ConteoInventarioId { get; set; }
    public long? ConteoInventarioItemId { get; set; }
    public long? RecepcionCompraInsumoId { get; set; }
    public long? RecepcionCompraInsumoItemId { get; set; }
    public string ClaveIdempotencia { get; set; } = "";
    public int RegistradoPor { get; set; }
    public DateTime RegistradoEn { get; set; }
}

public class TrasladoInsumo : TenantEntity
{
    public long TrasladoId { get; set; }
    public int SucursalOrigenId { get; set; }
    public int SucursalDestinoId { get; set; }
    public string Estado { get; set; } = "EN_TRANSITO";
    public string ClaveIdempotencia { get; set; } = "";
    public string HuellaSolicitud { get; set; } = "";
    public string? Motivo { get; set; }
    public int RegistradoPor { get; set; }
    public DateTime RegistradoEn { get; set; }
    public string? ClaveRecepcion { get; set; }
    public string? HuellaRecepcion { get; set; }
    public int? RecibidoPor { get; set; }
    public DateTime? RecibidoEn { get; set; }
}

public class TrasladoInsumoItem : TenantEntity
{
    public long TrasladoItemId { get; set; }
    public long TrasladoId { get; set; }
    public int InsumoId { get; set; }
    public decimal CantidadBase { get; set; }
    public decimal CostoUnitarioBase { get; set; }
    public decimal CostoTotal { get; set; }
    public string NombreInsumoSnapshot { get; set; } = "";
    public string UnidadBaseSnapshot { get; set; } = "";
}

public class TrasladoInsumoRecepcionItem : TenantEntity
{
    public long TrasladoRecepcionItemId { get; set; }
    public long TrasladoId { get; set; }
    public long TrasladoItemId { get; set; }
    public decimal CantidadRecibidaBase { get; set; }
    public string? MotivoDiferencia { get; set; }
}

public class ConteoInventario : TenantEntity
{
    public long ConteoInventarioId { get; set; }
    public int SucursalId { get; set; }
    public string Estado { get; set; } = "ABIERTO";
    public string ClaveIdempotencia { get; set; } = "";
    public string HuellaSolicitud { get; set; } = "";
    public string Motivo { get; set; } = "";
    public int RegistradoPor { get; set; }
    public DateTime RegistradoEn { get; set; }
    public int? AplicadoPor { get; set; }
    public DateTime? AplicadoEn { get; set; }
}

public class ConteoInventarioItem : TenantEntity
{
    public long ConteoInventarioItemId { get; set; }
    public long ConteoInventarioId { get; set; }
    public int InsumoId { get; set; }
    public decimal StockSistemaBase { get; set; }
    public decimal CantidadContadaBase { get; set; }
    public decimal DiferenciaBase { get; set; }
    public decimal? CostoUnitarioBase { get; set; }
    public bool CostoConfigurado { get; set; }
    public decimal? CostoTotalDiferencia { get; set; }
    public string NombreInsumoSnapshot { get; set; } = "";
    public string UnidadBaseSnapshot { get; set; } = "";
}

public class ProveedorInsumo : TenantEntity
{
    public int ProveedorInsumoId { get; set; }
    public string? Ruc { get; set; }
    public string Nombre { get; set; } = "";
    public string? Contacto { get; set; }
    public string? Telefono { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime CreadoEn { get; set; }
    public DateTime ActualizadoEn { get; set; }
}

public class RecepcionCompraInsumo : TenantEntity
{
    public long RecepcionCompraInsumoId { get; set; }
    public int SucursalId { get; set; }
    public int ProveedorInsumoId { get; set; }
    public string ProveedorRucSnapshot { get; set; } = "";
    public string ProveedorNombreSnapshot { get; set; } = "";
    public string TipoDocumento { get; set; } = "";
    public string? SerieDocumento { get; set; }
    public string? NumeroDocumento { get; set; }
    public DateOnly FechaDocumento { get; set; }
    public decimal TotalInsumos { get; set; }
    public string Estado { get; set; } = "ABIERTO";
    public string ClaveIdempotencia { get; set; } = "";
    public string HuellaSolicitud { get; set; } = "";
    public string? Motivo { get; set; }
    public int RegistradoPor { get; set; }
    public DateTime RegistradoEn { get; set; }
    public int? AplicadoPor { get; set; }
    public DateTime? AplicadoEn { get; set; }
}

public class RecepcionCompraInsumoItem : TenantEntity
{
    public long RecepcionCompraInsumoItemId { get; set; }
    public long RecepcionCompraInsumoId { get; set; }
    public int InsumoId { get; set; }
    public decimal CantidadCompra { get; set; }
    public string UnidadCompraSnapshot { get; set; } = "";
    public decimal FactorCompraABaseSnapshot { get; set; }
    public decimal CantidadBase { get; set; }
    public decimal CostoUnitarioBase { get; set; }
    public decimal CostoTotal { get; set; }
    public string NombreInsumoSnapshot { get; set; } = "";
    public string UnidadBaseSnapshot { get; set; } = "";
}
