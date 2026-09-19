namespace ValexaFood.Domain.Entities;

// Toda tabla que pertenece a una empresa hereda de aquí: el contexto le aplica
// el filtro por empresa y estampa el tenant_id al insertar.
public abstract class TenantEntity
{
    public int TenantId { get; set; }
}

public static class RolIds
{
    public const int SuperAdmin = 0;
    public const int Administrador = 1;
    public const int Cajero = 2;
    public const int Cocinero = 3;
    public const int Mesero = 4;
    public const int Delivery = 5;

    public static readonly int[] Operativos = [Cajero, Cocinero, Mesero, Delivery];
}

public class Rol
{
    public int RolId { get; set; }
    public string Nombre { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public bool VeDinero { get; set; }
    public bool OcupaCupo { get; set; }
    public int Orden { get; set; }
}

public class Funcion
{
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Grupo { get; set; } = "";
    public int Orden { get; set; }
}

public class Permiso
{
    public int PermisoId { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Modulo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string? FuncionRequerida { get; set; }
    public int Orden { get; set; }
}

public class RolPermiso
{
    public int RolId { get; set; }
    public int PermisoId { get; set; }
}

public class Plan
{
    public int PlanId { get; set; }
    public string Nombre { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public decimal PrecioMensual { get; set; }
    public decimal? PrecioAnual { get; set; }
    public int MaxSucursales { get; set; }
    public int MaxUsuarios { get; set; }
    public int[] AdminAcumula { get; set; } = [];
    public bool Destacado { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public List<PlanRolCupo> Cupos { get; set; } = [];
    public List<PlanFuncion> Funciones { get; set; } = [];
}

public class PlanRolCupo
{
    public int PlanId { get; set; }
    public int RolId { get; set; }
    public int Maximo { get; set; }
}

public class PlanFuncion
{
    public int PlanId { get; set; }
    public string FuncionCodigo { get; set; } = "";
}

public class ConfiguracionSaas
{
    public int Id { get; set; } = 1;
    public string? WhatsappVentas { get; set; }
    public string? EmailSoporte { get; set; }
    public decimal DescuentoBienvenidaPct { get; set; }
    public int DiasPorVencer { get; set; }
    public int DiasPrueba { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class Tenant
{
    public int TenantId { get; set; }
    public string NombreComercial { get; set; } = "";
    public string? RazonSocial { get; set; }
    public string? Ruc { get; set; }
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? ContactoNombre { get; set; }
    public string TipoServicio { get; set; } = "RESTAURANTE";
    public int? PlanId { get; set; }
    public DateOnly? PlanFechaInicio { get; set; }
    public DateOnly? PlanFechaVencimiento { get; set; }
    public bool Activo { get; set; } = true;
    public string? MotivoSuspension { get; set; }
    public DateTime? FechaSuspension { get; set; }
    public int? PlanAvisoUltimoUmbral { get; set; }
    public DateOnly? PlanAvisoParaFecha { get; set; }
    public DateTime? RucVerificadoEn { get; set; }
    public string? NotasInternas { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Plan? Plan { get; set; }
}

public class Sucursal : TenantEntity
{
    public int SucursalId { get; set; }
    public string Nombre { get; set; } = "";
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public bool EsPrincipal { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

// No hereda TenantEntity: el SuperAdmin no tiene empresa (tenant_id NULL).
// El contexto le pone su propio filtro.
public class Usuario
{
    public int UsuarioId { get; set; }
    public int? TenantId { get; set; }
    public int? SucursalId { get; set; }
    public int RolId { get; set; }
    public string Nombre { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Dni { get; set; }
    public string? Telefono { get; set; }
    public string PasswordHash { get; set; } = "";
    public bool Activo { get; set; } = true;
    public bool DebeCambiarPassword { get; set; }
    public int IntentosFallidos { get; set; }
    public DateTime? BloqueadoHasta { get; set; }
    public DateTime? UltimoLogin { get; set; }
    public int SesionVersion { get; set; } = 1;
    public DateTime CreatedAt { get; set; }

    public Rol? Rol { get; set; }
    public Sucursal? Sucursal { get; set; }
    public Tenant? Tenant { get; set; }
}

public class RefreshToken
{
    public long RefreshTokenId { get; set; }
    public int UsuarioId { get; set; }
    public string Token { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public bool Revoked { get; set; }
    public string? Ip { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AccesoLog
{
    public long AccesoId { get; set; }
    public int? TenantId { get; set; }
    public int? UsuarioId { get; set; }
    public string Email { get; set; } = "";
    public bool Exito { get; set; }
    public string? Motivo { get; set; }
    public string? Ip { get; set; }
    public DateTime Fecha { get; set; }
}

public class SuscripcionMovimiento
{
    public int MovimientoId { get; set; }
    public int TenantId { get; set; }
    public string Tipo { get; set; } = "";
    public int PlanId { get; set; }
    public string PlanNombre { get; set; } = "";
    public decimal PrecioReferencial { get; set; }
    public int Meses { get; set; }
    public decimal Monto { get; set; }
    public decimal DescuentoPct { get; set; }
    public string? MetodoPago { get; set; }
    public string? NumeroOperacion { get; set; }
    public DateOnly FechaPago { get; set; }
    public string? Comprobante { get; set; }
    public string? Observacion { get; set; }
    public DateOnly? VenceAntes { get; set; }
    public DateOnly VenceDespues { get; set; }
    public int RegistradoPor { get; set; }
    public DateTime FechaRegistro { get; set; }
    public string Estado { get; set; } = "VIGENTE";
    public string? MotivoAnulacion { get; set; }
    public int? AnuladoPor { get; set; }
    public DateTime? FechaAnulacion { get; set; }

    public Tenant? Tenant { get; set; }
    public Usuario? RegistradoPorUsuario { get; set; }
    public Usuario? AnuladoPorUsuario { get; set; }
}

public class Bitacora
{
    public long BitacoraId { get; set; }
    public int? TenantId { get; set; }
    public int? UsuarioId { get; set; }
    public string UsuarioNombre { get; set; } = "";
    public int? RolId { get; set; }
    public string Accion { get; set; } = "";
    public string Entidad { get; set; } = "";
    public string? EntidadId { get; set; }
    public string Detalle { get; set; } = "";
    public string? Ip { get; set; }
    public DateTime Fecha { get; set; }
}
