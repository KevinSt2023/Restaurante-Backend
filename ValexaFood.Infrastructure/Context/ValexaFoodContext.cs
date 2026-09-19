using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ValexaFood.Domain.Entities;
using ValexaFood.Infrastructure.Sesion;

namespace ValexaFood.Infrastructure.Context;

public class ValexaFoodContext : DbContext
{
    private readonly ISesionActual _sesion;

    public ValexaFoodContext(DbContextOptions<ValexaFoodContext> options, ISesionActual sesion) : base(options)
    {
        _sesion = sesion;
    }

    // Lo lee el filtro global: EF lo evalúa en cada consulta, no al construir el modelo.
    private int FiltroTenantId => _sesion.FiltroTenantId;

    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Funcion> Funciones => Set<Funcion>();
    public DbSet<Permiso> Permisos => Set<Permiso>();
    public DbSet<RolPermiso> RolPermisos => Set<RolPermiso>();
    public DbSet<Plan> Planes => Set<Plan>();
    public DbSet<PlanRolCupo> PlanRolCupos => Set<PlanRolCupo>();
    public DbSet<PlanFuncion> PlanFunciones => Set<PlanFuncion>();
    public DbSet<ConfiguracionSaas> ConfiguracionSaas => Set<ConfiguracionSaas>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Sucursal> Sucursales => Set<Sucursal>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AccesoLog> AccesosLog => Set<AccesoLog>();
    public DbSet<SuscripcionMovimiento> SuscripcionMovimientos => Set<SuscripcionMovimiento>();
    public DbSet<Bitacora> Bitacora => Set<Bitacora>();
    public DbSet<AreaPreparacion> Areas => Set<AreaPreparacion>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<ProductoVariante> Variantes => Set<ProductoVariante>();
    public DbSet<GrupoModificador> GruposModificador => Set<GrupoModificador>();
    public DbSet<ModificadorOpcion> OpcionesModificador => Set<ModificadorOpcion>();
    public DbSet<ProductoGrupoModificador> ProductoGrupos => Set<ProductoGrupoModificador>();
    public DbSet<ComboGrupo> ComboGrupos => Set<ComboGrupo>();
    public DbSet<ComboOpcion> ComboOpciones => Set<ComboOpcion>();
    public DbSet<ProductoSucursal> ProductoSucursales => Set<ProductoSucursal>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Rol>(e => { e.ToTable("roles"); e.HasKey(x => x.RolId); e.Property(x => x.RolId).ValueGeneratedNever(); });
        mb.Entity<Funcion>(e => { e.ToTable("funcion"); e.HasKey(x => x.Codigo); });
        mb.Entity<Permiso>(e => { e.ToTable("permiso"); e.HasKey(x => x.PermisoId); });
        mb.Entity<RolPermiso>(e => { e.ToTable("rol_permiso"); e.HasKey(x => new { x.RolId, x.PermisoId }); });

        mb.Entity<Plan>(e =>
        {
            e.ToTable("planes");
            e.HasKey(x => x.PlanId);
            e.HasMany(x => x.Cupos).WithOne().HasForeignKey(x => x.PlanId);
            e.HasMany(x => x.Funciones).WithOne().HasForeignKey(x => x.PlanId);
        });
        mb.Entity<PlanRolCupo>(e => { e.ToTable("plan_rol_cupo"); e.HasKey(x => new { x.PlanId, x.RolId }); });
        mb.Entity<PlanFuncion>(e => { e.ToTable("plan_funcion"); e.HasKey(x => new { x.PlanId, x.FuncionCodigo }); });
        mb.Entity<ConfiguracionSaas>(e => { e.ToTable("configuracion_saas"); e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedNever(); });

        mb.Entity<Tenant>(e =>
        {
            e.ToTable("tenants");
            e.HasKey(x => x.TenantId);
            e.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId);
        });

        mb.Entity<Sucursal>(e => { e.ToTable("sucursales"); e.HasKey(x => x.SucursalId); });

        mb.Entity<Usuario>(e =>
        {
            e.ToTable("usuarios");
            e.HasKey(x => x.UsuarioId);
            e.HasOne(x => x.Rol).WithMany().HasForeignKey(x => x.RolId);
            e.HasOne(x => x.Sucursal).WithMany().HasForeignKey(x => x.SucursalId);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
            e.HasQueryFilter(u => u.TenantId == FiltroTenantId);
        });

        mb.Entity<RefreshToken>(e => { e.ToTable("refresh_token"); e.HasKey(x => x.RefreshTokenId); });
        mb.Entity<AccesoLog>(e => { e.ToTable("acceso_log"); e.HasKey(x => x.AccesoId); });

        mb.Entity<SuscripcionMovimiento>(e =>
        {
            e.ToTable("suscripcion_movimiento");
            e.HasKey(x => x.MovimientoId);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
            e.HasOne(x => x.RegistradoPorUsuario).WithMany().HasForeignKey(x => x.RegistradoPor);
            e.HasOne(x => x.AnuladoPorUsuario).WithMany().HasForeignKey(x => x.AnuladoPor);
        });

        mb.Entity<Bitacora>(e => { e.ToTable("bitacora"); e.HasKey(x => x.BitacoraId); });

        // ─── Carta ───
        mb.Entity<AreaPreparacion>(e => { e.ToTable("area_preparacion"); e.HasKey(x => x.AreaId); });
        mb.Entity<Categoria>(e => { e.ToTable("categoria"); e.HasKey(x => x.CategoriaId); });
        mb.Entity<Producto>(e =>
        {
            e.ToTable("producto");
            e.HasKey(x => x.ProductoId);
            e.HasOne(x => x.Categoria).WithMany().HasForeignKey(x => x.CategoriaId);
            e.HasOne(x => x.Area).WithMany().HasForeignKey(x => x.AreaId);
            e.HasMany(x => x.Variantes).WithOne().HasForeignKey(x => x.ProductoId);
            e.HasMany(x => x.Grupos).WithOne().HasForeignKey(x => x.ProductoId);
            e.HasMany(x => x.ComboGrupos).WithOne().HasForeignKey(x => x.ProductoId);
            e.HasMany(x => x.Sucursales).WithOne().HasForeignKey(x => x.ProductoId);
        });
        mb.Entity<ProductoVariante>(e => { e.ToTable("producto_variante"); e.HasKey(x => x.VarianteId); });
        mb.Entity<GrupoModificador>(e =>
        {
            e.ToTable("grupo_modificador");
            e.HasKey(x => x.GrupoId);
            e.HasMany(x => x.Opciones).WithOne().HasForeignKey(x => x.GrupoId);
        });
        mb.Entity<ModificadorOpcion>(e => { e.ToTable("modificador_opcion"); e.HasKey(x => x.OpcionId); });
        mb.Entity<ProductoGrupoModificador>(e =>
        {
            e.ToTable("producto_grupo_modificador");
            e.HasKey(x => new { x.ProductoId, x.GrupoId });
            e.HasOne(x => x.Grupo).WithMany().HasForeignKey(x => x.GrupoId);
        });
        mb.Entity<ComboGrupo>(e =>
        {
            e.ToTable("combo_grupo");
            e.HasKey(x => x.ComboGrupoId);
            e.HasMany(x => x.Opciones).WithOne().HasForeignKey(x => x.ComboGrupoId);
        });
        mb.Entity<ComboOpcion>(e =>
        {
            e.ToTable("combo_opcion");
            e.HasKey(x => x.ComboOpcionId);
            e.HasOne(x => x.Producto).WithMany().HasForeignKey(x => x.ProductoId);
        });
        mb.Entity<ProductoSucursal>(e => { e.ToTable("producto_sucursal"); e.HasKey(x => new { x.ProductoId, x.SucursalId }); });

        // Filtro global por empresa para todo lo que hereda TenantEntity.
        foreach (var tipo in mb.Model.GetEntityTypes().Select(t => t.ClrType).Where(t => typeof(TenantEntity).IsAssignableFrom(t)).ToList())
        {
            typeof(ValexaFoodContext)
                .GetMethod(nameof(AplicarFiltroTenant), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(tipo)
                .Invoke(this, [mb]);
        }

        // Npgsql exige Kind=Utc en timestamptz.
        var utc = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        foreach (var prop in mb.Model.GetEntityTypes().SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?)))
            prop.SetValueConverter(utc);
    }

    private void AplicarFiltroTenant<T>(ModelBuilder mb) where T : TenantEntity
        => mb.Entity<T>().HasQueryFilter(e => e.TenantId == FiltroTenantId);

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        EstamparTenant();
        return base.SaveChangesAsync(ct);
    }

    public override int SaveChanges()
    {
        EstamparTenant();
        return base.SaveChanges();
    }

    // Toda fila nueva de empresa sale con su tenant_id. Si quien escribe es el
    // SuperAdmin, el servicio lo pone a mano; si nadie lo pone, se corta aquí
    // con un error claro en vez de una FK rota.
    private void EstamparTenant()
    {
        var tenantSesion = _sesion.TenantId;
        foreach (var entry in ChangeTracker.Entries<TenantEntity>().Where(e => e.State == EntityState.Added))
        {
            if (entry.Entity.TenantId > 0) continue;
            if (tenantSesion is > 0) entry.Entity.TenantId = tenantSesion.Value;
            else throw new InvalidOperationException($"Registro de {entry.Entity.GetType().Name} sin empresa asignada.");
        }
    }
}
