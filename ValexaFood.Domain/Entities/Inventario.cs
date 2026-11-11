namespace ValexaFood.Domain.Entities;

public static class UnidadesInventario
{
    public const string Gramo = "G", Mililitro = "ML", Unidad = "UN";
    public static readonly string[] Todos = [Gramo, Mililitro, Unidad];
}

/// <summary>
/// Catálogo global de unidades. No lleva empresa a propósito: la equivalencia
/// kilo→gramo es física, no comercial, y si cada empresa pudiera editarla
/// volveríamos al texto libre con más pasos.
/// </summary>
public class UnidadMedida
{
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Plural { get; set; } = "";
    public string Abreviatura { get; set; } = "";
    /// <summary>MASA, VOLUMEN o CONTEO.</summary>
    public string Dimension { get; set; } = "";
    /// <summary>A qué unidad base reduce: G, ML o UN.</summary>
    public string UnidadBase { get; set; } = UnidadesInventario.Unidad;
    public decimal FactorABase { get; set; } = 1m;
    public short Orden { get; set; }
    public bool Activo { get; set; } = true;
}

public static class TiposAlmacen
{
    public const string Principal = "PRINCIPAL", Cocina = "COCINA", Barra = "BARRA",
        Frio = "FRIO", Deposito = "DEPOSITO", Otro = "OTRO";
    public static readonly string[] Todos = [Principal, Cocina, Barra, Frio, Deposito, Otro];
}

/// <summary>
/// Un sitio donde hay insumos: la cocina, la barra, la camara, el deposito.
/// `SucursalId` es nullable a proposito (sin sucursal seria un almacen central
/// que abastece varias sedes), pero hoy la base lo exige con un CHECK: un
/// almacen central sin pantalla de abastecimiento es configuracion muerta.
/// </summary>
public class Almacen : TenantEntity
{
    public int AlmacenId { get; set; }
    public int? SucursalId { get; set; }
    public string Nombre { get; set; } = "";
    public string Tipo { get; set; } = TiposAlmacen.Otro;
    /// <summary>El almacen al que va lo que no especifica destino. Uno por sucursal.</summary>
    public bool EsPrincipal { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime CreadoEn { get; set; }
    public DateTime ActualizadoEn { get; set; }
}

/// <summary>
/// El unico sitio donde se escribe el stock. `InsumoSucursal.StockActualBase`
/// es la suma de estas filas, mantenida por un trigger de la base.
/// </summary>
public class InsumoAlmacen : TenantEntity
{
    public int AlmacenId { get; set; }
    public int InsumoId { get; set; }
    public decimal StockActualBase { get; set; }
    public DateTime ActualizadoEn { get; set; }
}

public class Insumo : TenantEntity
{
    public int InsumoId { get; set; }
    public string? Codigo { get; set; }
    public string Nombre { get; set; } = "";
    public string UnidadBase { get; set; } = UnidadesInventario.Unidad;
    /// <summary>La unidad elegida del catálogo. Es lo único que se guarda de la conversión.</summary>
    public string UnidadCompraCodigo { get; set; } = UnidadesInventario.Unidad;
    /// <summary>Empaque del proveedor: "SACO", "CAJA", "BALDE". Opcional.</summary>
    public string? EmpaqueNombre { get; set; }
    /// <summary>Cuánto trae ese empaque, en la unidad elegida: 50 (kilos).</summary>
    public decimal? EmpaqueContenido { get; set; }
    /// <summary>Etiqueta derivada ("KG", "SACO 50 KG"). La escribe el trigger de la base.</summary>
    public string UnidadCompra { get; set; } = UnidadesInventario.Unidad;
    /// <summary>Derivado: contenido del empaque × factor de la unidad. Lo calcula la base.</summary>
    public decimal FactorCompraABase { get; set; } = 1m;
    public bool Activo { get; set; } = true;
    public DateTime CreadoEn { get; set; }
    public DateTime ActualizadoEn { get; set; }
}

public class InsumoSucursal : TenantEntity
{
    public int SucursalId { get; set; }
    public int InsumoId { get; set; }
    /// <summary>DERIVADO: la suma de los almacenes de la sucursal. Lo escribe un
    /// trigger sobre `insumo_almacen`; la aplicacion no lo toca.</summary>
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
    /// <summary>De que almacen salio o a cual entro. Obligatorio desde scripts/36.</summary>
    public int AlmacenId { get; set; }
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
    public int AlmacenOrigenId { get; set; }
    public int AlmacenDestinoId { get; set; }
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
    /// <summary>Se cuenta un estante, no un local: el conteo compara contra ESTE almacen.</summary>
    public int AlmacenId { get; set; }
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
    /// <summary>El almacén donde entró la mercadería. Por omisión, el Principal.</summary>
    public int AlmacenId { get; set; }
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
