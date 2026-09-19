namespace ValexaFood.Domain.Entities;

public static class TiposProducto
{
    public const string Plato = "PLATO", Bebida = "BEBIDA", Producto = "PRODUCTO", Combo = "COMBO";
    public static readonly string[] Todos = [Plato, Bebida, Producto, Combo];
}

public class AreaPreparacion : TenantEntity
{
    public int AreaId { get; set; }
    public string Nombre { get; set; } = "";
    public string Color { get; set; } = "#801D31";
    public bool ImprimeComanda { get; set; } = true;
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public class Categoria : TenantEntity
{
    public int CategoriaId { get; set; }
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    public string Color { get; set; } = "#B08834";
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public class Producto : TenantEntity
{
    public int ProductoId { get; set; }
    public int CategoriaId { get; set; }
    public int? AreaId { get; set; }
    public string Tipo { get; set; } = TiposProducto.Plato;
    public string? Codigo { get; set; }
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
    public string AfectacionIgv { get; set; } = "GRAVADO";
    public int? TiempoPreparacionMin { get; set; }
    public string[] Etiquetas { get; set; } = [];
    public string? Imagen { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Categoria? Categoria { get; set; }
    public AreaPreparacion? Area { get; set; }
    public List<ProductoVariante> Variantes { get; set; } = [];
    public List<ProductoGrupoModificador> Grupos { get; set; } = [];
    public List<ComboGrupo> ComboGrupos { get; set; } = [];
    public List<ProductoSucursal> Sucursales { get; set; } = [];
}

public class ProductoVariante : TenantEntity
{
    public int VarianteId { get; set; }
    public int ProductoId { get; set; }
    public string Nombre { get; set; } = "";
    public decimal Precio { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
}

public class GrupoModificador : TenantEntity
{
    public int GrupoId { get; set; }
    public string Nombre { get; set; } = "";
    public int Minimo { get; set; }
    public int Maximo { get; set; } = 1;
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
    public List<ModificadorOpcion> Opciones { get; set; } = [];
}

public class ModificadorOpcion : TenantEntity
{
    public int OpcionId { get; set; }
    public int GrupoId { get; set; }
    public string Nombre { get; set; } = "";
    public decimal PrecioExtra { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
}

public class ProductoGrupoModificador : TenantEntity
{
    public int ProductoId { get; set; }
    public int GrupoId { get; set; }
    public int Orden { get; set; }
    public GrupoModificador? Grupo { get; set; }
}

public class ComboGrupo : TenantEntity
{
    public int ComboGrupoId { get; set; }
    public int ProductoId { get; set; }
    public string Nombre { get; set; } = "";
    public int Cantidad { get; set; } = 1;
    public int Orden { get; set; }
    public List<ComboOpcion> Opciones { get; set; } = [];
}

public class ComboOpcion : TenantEntity
{
    public int ComboOpcionId { get; set; }
    public int ComboGrupoId { get; set; }
    public int ProductoId { get; set; }
    public decimal PrecioExtra { get; set; }
    public int Orden { get; set; }
    public Producto? Producto { get; set; }
}

public class ProductoSucursal : TenantEntity
{
    public int ProductoId { get; set; }
    public int SucursalId { get; set; }
    public decimal? Precio { get; set; }
    public DateOnly? AgotadoFecha { get; set; }
}
