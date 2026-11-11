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
    public DbSet<UsuarioSucursal> UsuarioSucursales => Set<UsuarioSucursal>();
    public DbSet<UsuarioArea> UsuarioAreas => Set<UsuarioArea>();
    public DbSet<ConfiguracionEmpaque> ConfiguracionEmpaque => Set<ConfiguracionEmpaque>();
    public DbSet<ZonaReparto> ZonasReparto => Set<ZonaReparto>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<DireccionEntrega> DireccionesEntrega => Set<DireccionEntrega>();
    public DbSet<Envio> Envios => Set<Envio>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Caja> Cajas => Set<Caja>();
    public DbSet<SesionCaja> SesionesCaja => Set<SesionCaja>();
    public DbSet<CajaMovimiento> CajaMovimientos => Set<CajaMovimiento>();
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
    public DbSet<Mesa> Mesas => Set<Mesa>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<PedidoItem> PedidoItems => Set<PedidoItem>();
    public DbSet<Comanda> Comandas => Set<Comanda>();
    public DbSet<ComandaItem> ComandaItems => Set<ComandaItem>();
    public DbSet<TrabajoImpresion> TrabajosImpresion => Set<TrabajoImpresion>();
    public DbSet<TrabajoImpresionEvento> TrabajosImpresionEventos => Set<TrabajoImpresionEvento>();
    public DbSet<Pago> Pagos => Set<Pago>();
    public DbSet<PagoItemAplicacion> PagoItemAplicaciones => Set<PagoItemAplicacion>();
    public DbSet<AjusteVenta> AjustesVenta => Set<AjusteVenta>();
    public DbSet<AjusteVentaConfiguracion> AjustesVentaConfiguracion => Set<AjusteVentaConfiguracion>();
    public DbSet<ConfiguracionFacturacion> ConfiguracionesFacturacion => Set<ConfiguracionFacturacion>();
    public DbSet<ComprobanteSerie> ComprobantesSerie => Set<ComprobanteSerie>();
    public DbSet<ComprobanteVenta> ComprobantesVenta => Set<ComprobanteVenta>();
    public DbSet<ComprobanteVentaItem> ComprobantesVentaItems => Set<ComprobanteVentaItem>();
    public DbSet<ComprobanteVentaPago> ComprobantesVentaPagos => Set<ComprobanteVentaPago>();
    public DbSet<UnidadMedida> UnidadesMedida => Set<UnidadMedida>();
    public DbSet<Almacen> Almacenes => Set<Almacen>();
    public DbSet<InsumoAlmacen> InsumosAlmacen => Set<InsumoAlmacen>();
    public DbSet<Insumo> Insumos => Set<Insumo>();
    public DbSet<InsumoSucursal> InsumosSucursal => Set<InsumoSucursal>();
    public DbSet<RecetaInsumo> RecetasInsumo => Set<RecetaInsumo>();
    public DbSet<MovimientoInsumo> MovimientosInsumo => Set<MovimientoInsumo>();
    public DbSet<TrasladoInsumo> TrasladosInsumo => Set<TrasladoInsumo>();
    public DbSet<TrasladoInsumoItem> TrasladosInsumoItems => Set<TrasladoInsumoItem>();
    public DbSet<TrasladoInsumoRecepcionItem> TrasladosInsumoRecepcionItems => Set<TrasladoInsumoRecepcionItem>();
    public DbSet<ConteoInventario> ConteosInventario => Set<ConteoInventario>();
    public DbSet<ConteoInventarioItem> ConteosInventarioItems => Set<ConteoInventarioItem>();
    public DbSet<ProveedorInsumo> ProveedoresInsumo => Set<ProveedorInsumo>();
    public DbSet<RecepcionCompraInsumo> RecepcionesCompraInsumo => Set<RecepcionCompraInsumo>();
    public DbSet<RecepcionCompraInsumoItem> RecepcionesCompraInsumoItems => Set<RecepcionCompraInsumoItem>();

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

        mb.Entity<Sucursal>(e =>
        {
            e.ToTable("sucursales");
            e.HasKey(x => x.SucursalId);
            e.HasAlternateKey(x => new { x.TenantId, x.SucursalId });
        });

        mb.Entity<Usuario>(e =>
        {
            e.ToTable("usuarios");
            e.HasKey(x => x.UsuarioId);
            e.HasIndex(x => new { x.TenantId, x.UsuarioId }).IsUnique();
            e.HasOne(x => x.Rol).WithMany().HasForeignKey(x => x.RolId);
            e.HasOne(x => x.Sucursal).WithMany().HasForeignKey(x => x.SucursalId);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
            e.HasQueryFilter(u => u.TenantId == FiltroTenantId);
        });

        mb.Entity<UsuarioSucursal>(e =>
        {
            e.ToTable("usuario_sucursal");
            e.HasKey(x => new { x.TenantId, x.UsuarioId, x.SucursalId });
        });

        mb.Entity<UsuarioArea>(e =>
        {
            e.ToTable("usuario_area");
            e.HasKey(x => new { x.UsuarioId, x.AreaId });
        });

        mb.Entity<ConfiguracionEmpaque>(e => { e.ToTable("configuracion_empaque"); e.HasKey(x => x.TenantId); });
        mb.Entity<ZonaReparto>(e => { e.ToTable("zona_reparto"); e.HasKey(x => x.ZonaId); });
        mb.Entity<Cliente>(e =>
        {
            e.ToTable("cliente");
            e.HasKey(x => x.ClienteId);
            e.HasMany(x => x.Direcciones).WithOne().HasForeignKey(x => x.ClienteId);
        });
        mb.Entity<DireccionEntrega>(e => { e.ToTable("direccion_entrega"); e.HasKey(x => x.DireccionId); });
        mb.Entity<Envio>(e => { e.ToTable("envio"); e.HasKey(x => x.EnvioId); });

        mb.Entity<Caja>(e =>
        {
            e.ToTable("caja");
            e.HasKey(x => x.CajaId);
            e.HasAlternateKey(x => new { x.TenantId, x.SucursalId, x.CajaId });
        });

        mb.Entity<SesionCaja>(e =>
        {
            e.ToTable("sesion_caja");
            e.HasKey(x => x.SesionCajaId);
            e.HasAlternateKey(x => new { x.TenantId, x.SucursalId, x.SesionCajaId });
        });

        mb.Entity<CajaMovimiento>(e =>
        {
            e.ToTable("caja_movimiento");
            e.HasKey(x => x.CajaMovimientoId);
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

        mb.Entity<Bitacora>(e =>
        {
            e.ToTable("bitacora");
            e.HasKey(x => x.BitacoraId);
            e.HasOne<Sucursal>().WithMany().HasForeignKey(x => new { x.TenantId, x.SucursalId })
                .HasPrincipalKey(x => new { x.TenantId, x.SucursalId });
        });

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

        mb.Entity<Mesa>(e => { e.ToTable("mesa"); e.HasKey(x => x.MesaId); e.HasAlternateKey(x => new { x.TenantId, x.SucursalId, x.MesaId }); });
        mb.Entity<Pedido>(e => { e.ToTable("pedido"); e.HasKey(x => x.PedidoId); e.HasAlternateKey(x => new { x.TenantId, x.SucursalId, x.PedidoId }); });
        mb.Entity<PedidoItem>(e => { e.ToTable("pedido_item"); e.HasKey(x => x.PedidoItemId); e.Property(x => x.SeleccionJson).HasColumnType("jsonb"); });
        mb.Entity<Comanda>(e => { e.ToTable("comanda"); e.HasKey(x => x.ComandaId); e.HasAlternateKey(x => new { x.TenantId, x.SucursalId, x.ComandaId }); });
        mb.Entity<ComandaItem>(e => { e.ToTable("comanda_item"); e.HasKey(x => x.ComandaItemId); });
        mb.Entity<TrabajoImpresion>(e => { e.ToTable("trabajo_impresion"); e.HasKey(x => x.TrabajoImpresionId); e.Property(x => x.PayloadJson).HasColumnType("jsonb"); });
        mb.Entity<TrabajoImpresionEvento>(e => { e.ToTable("trabajo_impresion_evento"); e.HasKey(x => x.TrabajoImpresionEventoId); });
        mb.Entity<Pago>(e => { e.ToTable("pago"); e.HasKey(x => x.PagoId); e.Property(x => x.Referencia).HasMaxLength(80); });
        mb.Entity<PagoItemAplicacion>(e => { e.ToTable("pago_item_aplicacion"); e.HasKey(x => new { x.TenantId, x.SucursalId, x.PagoId, x.PedidoItemId }); });
        mb.Entity<AjusteVenta>(e => { e.ToTable("ajuste_venta"); e.HasKey(x => x.AjusteVentaId); e.Property(x => x.Motivo).HasMaxLength(300); });
        mb.Entity<AjusteVentaConfiguracion>(e => { e.ToTable("ajuste_venta_configuracion"); e.HasKey(x => x.TenantId); e.Property(x => x.TenantId).ValueGeneratedNever(); });
        mb.Entity<ConfiguracionFacturacion>(e => { e.ToTable("configuracion_facturacion"); e.HasKey(x => x.TenantId); });
        mb.Entity<ComprobanteSerie>(e => { e.ToTable("comprobante_serie"); e.HasKey(x => x.ComprobanteSerieId); });
        mb.Entity<ComprobanteVenta>(e =>
        {
            e.ToTable("comprobante_venta");
            e.HasKey(x => x.ComprobanteId);
            e.Property(x => x.NubefactRespuesta).HasColumnType("jsonb");
        });
        mb.Entity<ComprobanteVentaItem>(e => { e.ToTable("comprobante_venta_item"); e.HasKey(x => x.ComprobanteItemId); e.Property(x => x.Descripcion).HasMaxLength(500); });
        mb.Entity<ComprobanteVentaPago>(e => { e.ToTable("comprobante_venta_pago"); e.HasKey(x => new { x.TenantId, x.SucursalId, x.ComprobanteId, x.PagoId }); });
        mb.Entity<UnidadMedida>(e => { e.ToTable("unidad_medida"); e.HasKey(x => x.Codigo); e.Property(x => x.Codigo).HasMaxLength(8); e.Property(x => x.Nombre).HasMaxLength(40); e.Property(x => x.Plural).HasMaxLength(40); e.Property(x => x.Abreviatura).HasMaxLength(8); e.Property(x => x.Dimension).HasMaxLength(8); e.Property(x => x.UnidadBase).HasMaxLength(2); });
        mb.Entity<Insumo>(e => { e.ToTable("insumo"); e.HasKey(x => x.InsumoId); e.Property(x => x.Codigo).HasMaxLength(40); e.Property(x => x.Nombre).HasMaxLength(120); e.Property(x => x.UnidadBase).HasMaxLength(2); e.Property(x => x.UnidadCompra).HasMaxLength(24); e.Property(x => x.UnidadCompraCodigo).HasMaxLength(8); e.Property(x => x.EmpaqueNombre).HasMaxLength(24); });
        mb.Entity<Almacen>(e => { e.ToTable("almacen"); e.HasKey(x => x.AlmacenId); e.HasAlternateKey(x => new { x.TenantId, x.AlmacenId }); e.Property(x => x.Nombre).HasMaxLength(60); e.Property(x => x.Tipo).HasMaxLength(12); });
        mb.Entity<InsumoAlmacen>(e => { e.ToTable("insumo_almacen"); e.HasKey(x => new { x.TenantId, x.AlmacenId, x.InsumoId }); });
        mb.Entity<InsumoSucursal>(e => { e.ToTable("insumo_sucursal"); e.HasKey(x => new { x.TenantId, x.SucursalId, x.InsumoId }); });
        mb.Entity<RecetaInsumo>(e => { e.ToTable("receta_insumo"); e.HasKey(x => x.RecetaInsumoId); });
        mb.Entity<MovimientoInsumo>(e => { e.ToTable("movimiento_insumo"); e.HasKey(x => x.MovimientoInsumoId); e.Property(x => x.NombreInsumoSnapshot).HasMaxLength(120); e.Property(x => x.UnidadRegistradaSnapshot).HasMaxLength(24); e.Property(x => x.UnidadBaseSnapshot).HasMaxLength(2); e.Property(x => x.Motivo).HasMaxLength(300); e.Property(x => x.Referencia).HasMaxLength(100); e.Property(x => x.ClaveIdempotencia).HasMaxLength(80); });
        mb.Entity<TrasladoInsumo>(e => { e.ToTable("traslado_insumo"); e.HasKey(x => x.TrasladoId); e.HasAlternateKey(x => new { x.TenantId, x.TrasladoId }); e.Property(x => x.Estado).HasMaxLength(20); e.Property(x => x.ClaveIdempotencia).HasMaxLength(32); e.Property(x => x.HuellaSolicitud).HasMaxLength(64); e.Property(x => x.ClaveRecepcion).HasMaxLength(32); e.Property(x => x.HuellaRecepcion).HasMaxLength(64); e.Property(x => x.Motivo).HasMaxLength(300); });
        mb.Entity<TrasladoInsumoItem>(e => { e.ToTable("traslado_insumo_item"); e.HasKey(x => x.TrasladoItemId); e.HasAlternateKey(x => new { x.TenantId, x.TrasladoId, x.TrasladoItemId }); e.Property(x => x.NombreInsumoSnapshot).HasMaxLength(120); e.Property(x => x.UnidadBaseSnapshot).HasMaxLength(2); });
        mb.Entity<TrasladoInsumoRecepcionItem>(e => { e.ToTable("traslado_insumo_recepcion_item"); e.HasKey(x => x.TrasladoRecepcionItemId); e.HasAlternateKey(x => new { x.TenantId, x.TrasladoId, x.TrasladoItemId }); e.Property(x => x.MotivoDiferencia).HasMaxLength(300); });
        mb.Entity<ConteoInventario>(e => { e.ToTable("conteo_inventario"); e.HasKey(x => x.ConteoInventarioId); e.HasAlternateKey(x => new { x.TenantId, x.ConteoInventarioId }); e.Property(x => x.Estado).HasMaxLength(20); e.Property(x => x.ClaveIdempotencia).HasMaxLength(32); e.Property(x => x.HuellaSolicitud).HasMaxLength(64); e.Property(x => x.Motivo).HasMaxLength(300); });
        mb.Entity<ConteoInventarioItem>(e => { e.ToTable("conteo_inventario_item"); e.HasKey(x => x.ConteoInventarioItemId); e.HasAlternateKey(x => new { x.TenantId, x.ConteoInventarioId, x.ConteoInventarioItemId }); e.Property(x => x.NombreInsumoSnapshot).HasMaxLength(120); e.Property(x => x.UnidadBaseSnapshot).HasMaxLength(2); });
        mb.Entity<ProveedorInsumo>(e => { e.ToTable("proveedor_insumo"); e.HasKey(x => x.ProveedorInsumoId); e.HasAlternateKey(x => new { x.TenantId, x.ProveedorInsumoId }); e.Property(x => x.Ruc).HasMaxLength(11); e.Property(x => x.Nombre).HasMaxLength(120); e.Property(x => x.Contacto).HasMaxLength(120); e.Property(x => x.Telefono).HasMaxLength(20); });
        mb.Entity<RecepcionCompraInsumo>(e => { e.ToTable("recepcion_compra_insumo"); e.HasKey(x => x.RecepcionCompraInsumoId); e.HasAlternateKey(x => new { x.TenantId, x.RecepcionCompraInsumoId }); e.Property(x => x.ProveedorRucSnapshot).HasMaxLength(11); e.Property(x => x.ProveedorNombreSnapshot).HasMaxLength(120); e.Property(x => x.TipoDocumento).HasMaxLength(20); e.Property(x => x.SerieDocumento).HasMaxLength(20); e.Property(x => x.NumeroDocumento).HasMaxLength(20); e.Property(x => x.Estado).HasMaxLength(20); e.Property(x => x.ClaveIdempotencia).HasMaxLength(32); e.Property(x => x.HuellaSolicitud).HasMaxLength(64); e.Property(x => x.Motivo).HasMaxLength(300); });
        mb.Entity<RecepcionCompraInsumoItem>(e => { e.ToTable("recepcion_compra_insumo_item"); e.HasKey(x => x.RecepcionCompraInsumoItemId); e.HasAlternateKey(x => new { x.TenantId, x.RecepcionCompraInsumoId, x.RecepcionCompraInsumoItemId }); e.Property(x => x.UnidadCompraSnapshot).HasMaxLength(24); e.Property(x => x.NombreInsumoSnapshot).HasMaxLength(120); e.Property(x => x.UnidadBaseSnapshot).HasMaxLength(2); });

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
