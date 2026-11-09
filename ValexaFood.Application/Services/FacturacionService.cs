using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using ValexaFood.Application.Common;
using ValexaFood.Domain.Entities;
using ValexaFood.Infrastructure.Context;
using ValexaFood.Infrastructure.Sesion;

namespace ValexaFood.Application.Services;

public record ConfigFacturacionDto(string? RutaApi, string? Token, string Modo, decimal IgvTasa, bool Configurada,
    string? ConfirmacionProduccion = null);
public record EmitirComprobanteDto(string Tipo, string? ClienteTipoDocumento, string? ClienteNumeroDocumento,
    string? ClienteNombre, string? ClienteDireccion, string? ClienteEmail, string? ClaveIdempotencia);
public record ReenviarCpeDto(bool ConfirmoAusenciaEnProveedor, string? TicketSoporte, string? Motivo);
public record ComprobanteItemDto(string Descripcion, int Cantidad, decimal PrecioUnitario, string AfectacionIgv,
    decimal BaseImponible, decimal Igv, decimal Total);
public record ComprobanteDto(long ComprobanteId, long PedidoId, string Tipo, string Serie, long Numero, string Estado,
    string ClienteNombre, string? ClienteTipoDocumento, string? ClienteNumeroDocumento, decimal SubtotalGravado,
    decimal SubtotalExonerado, decimal SubtotalInafecto, decimal Igv, decimal Total, string Moneda,
    DateTime EmitidoEn, DateTime? EnviadoEn, string? MensajeRespuesta, string? PdfUrl, string? XmlUrl, string? HashCpe,
    string? QrContenido, string? CdrUrl, List<ComprobanteItemDto> Items);
public record VentaPendienteDto(long PedidoId, DateTime PagadoEn, decimal Total, string Canal, List<ComprobanteItemDto> Items);
/// <summary>Un comprobante que no calza con su venta, con el motivo escrito en claro.</summary>
public record CuadreFiscalObservadoDto(long ComprobanteId, long PedidoId, string Tipo, string Numero,
    string Estado, decimal TotalComprobante, decimal SumaLineas, decimal CobradoDelPedido, List<string> Motivos);
public record CuadreFiscalDto(int Comprobantes, decimal TotalEmitido, int Observados,
    List<CuadreFiscalObservadoDto> Detalle);

public class FacturacionService
{
    private static readonly TimeZoneInfo ZonaPeru=ResolverZonaPeru();
    private readonly ValexaFoodContext _db;
    private readonly ISesionActual _sesion;
    private readonly BitacoraService _bitacora;
    private readonly IDataProtector _protector;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<FacturacionService> _logger;
    public FacturacionService(ValexaFoodContext db, ISesionActual sesion, BitacoraService bitacora,
        IDataProtectionProvider protection, IHttpClientFactory httpFactory, ILogger<FacturacionService> logger)
    { _db=db; _sesion=sesion; _bitacora=bitacora; _protector=protection.CreateProtector("ValexaFood.NubeFact.Token.v1"); _httpFactory=httpFactory; _logger=logger; }

    private int Tenant => _sesion.TenantId ?? throw new ReglaNegocioException("La sesión no pertenece a una empresa.",403);
    private int Sucursal => _sesion.SucursalId ?? throw new ReglaNegocioException("Selecciona una sucursal antes de operar.",428);
    private int Usuario => _sesion.UsuarioId ?? throw new ReglaNegocioException("La sesión no identifica al usuario.",401);

    public async Task<ConfigFacturacionDto> ConfiguracionAsync()
    {
        var c=await _db.ConfiguracionesFacturacion.AsNoTracking().FirstOrDefaultAsync(x=>x.TenantId==Tenant);
        return new ConfigFacturacionDto(c?.RutaApi, null, c?.Modo ?? "DEMO", c?.IgvTasa ?? .18m,
            !string.IsNullOrWhiteSpace(c?.RutaApi) && c?.TokenCifrado is { Length: > 0 });
    }

    public async Task<ConfigFacturacionDto> GuardarConfiguracionAsync(ConfigFacturacionDto dto)
    {
        if (_sesion.RolId != RolIds.Administrador) throw new ReglaNegocioException("Solo el administrador puede configurar el emisor electrónico.",403);
        var modo=(dto.Modo??"").Trim().ToUpperInvariant();
        if (modo is not ("DEMO" or "PRODUCCION")) throw new ReglaNegocioException("Modo debe ser DEMO o PRODUCCION.");
        if (modo == "PRODUCCION" && !string.Equals(dto.ConfirmacionProduccion?.Trim(), "CONFIRMO PRODUCCION", StringComparison.OrdinalIgnoreCase))
            throw new ReglaNegocioException("Para guardar modo PRODUCCION escribe exactamente CONFIRMO PRODUCCION y verifica la ruta y el token asignados por Nubefact.");
        if (dto.IgvTasa is < 0 or > .25m || decimal.Round(dto.IgvTasa,4)!=dto.IgvTasa) throw new ReglaNegocioException("La tasa IGV debe estar entre 0 y 25%.");
        var ruta=string.IsNullOrWhiteSpace(dto.RutaApi)?null:dto.RutaApi.Trim();
        if (ruta!=null && (!Uri.TryCreate(ruta,UriKind.Absolute,out var uri) || uri.Scheme!="https" || uri.Host is not ("api.nubefact.com" or "api.pse.pe")))
            throw new ReglaNegocioException("La ruta debe ser HTTPS y pertenecer a api.nubefact.com o api.pse.pe.");
        var c=await _db.ConfiguracionesFacturacion.FirstOrDefaultAsync(x=>x.TenantId==Tenant);
        if (c==null) { c=new ConfiguracionFacturacion{TenantId=Tenant}; _db.ConfiguracionesFacturacion.Add(c); }
        c.Proveedor="NUBEFACT_API"; c.RutaApi=ruta; c.Modo=modo; c.IgvTasa=dto.IgvTasa; c.ActualizadoPor=Usuario; c.ActualizadoEn=DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(dto.Token)) c.TokenCifrado=Encoding.UTF8.GetBytes(_protector.Protect(dto.Token.Trim()));
        await _db.SaveChangesAsync();
        return await ConfiguracionAsync();
    }

    public async Task<List<ComprobanteDto>> ListarAsync(DateOnly? desde=null, DateOnly? hasta=null)
    {
        var q=_db.ComprobantesVenta.AsNoTracking().Where(x=>x.SucursalId==Sucursal);
        if(desde.HasValue) q=q.Where(x=>x.EmitidoEn>=desde.Value.ToDateTime(TimeOnly.MinValue,DateTimeKind.Utc));
        if(hasta.HasValue) q=q.Where(x=>x.EmitidoEn<hasta.Value.AddDays(1).ToDateTime(TimeOnly.MinValue,DateTimeKind.Utc));
        var docs=await q.OrderByDescending(x=>x.EmitidoEn).Take(100).ToListAsync();
        return await MapAsync(docs);
    }

    public async Task<List<VentaPendienteDto>> PendientesAsync()
    {
        var pedidos=await _db.Pedidos.AsNoTracking().Where(p=>p.SucursalId==Sucursal&&p.Estado=="PAGADO"&&
            !_db.ComprobantesVenta.Any(c=>c.PedidoId==p.PedidoId&&c.SucursalId==Sucursal)).OrderByDescending(p=>p.PagadoEn).Take(100).ToListAsync();
        var ids=pedidos.Select(p=>p.PedidoId).ToList();
        var items=await _db.PedidoItems.AsNoTracking().Where(i=>ids.Contains(i.PedidoId)&&i.SucursalId==Sucursal&&i.Estado!="ANULADO").ToListAsync();
        return pedidos.Select(p=>new VentaPendienteDto(p.PedidoId,p.PagadoEn??p.CreadoEn,
            items.Where(i=>i.PedidoId==p.PedidoId).Sum(i=>i.Cantidad*i.PrecioUnitario),p.Canal,
            items.Where(i=>i.PedidoId==p.PedidoId).Select(i=>new ComprobanteItemDto(PedidoService.DescripcionItem(i.Nombre,i.SeleccionJson),i.Cantidad,i.PrecioUnitario,
                i.AfectacionIgv,0,0,i.Cantidad*i.PrecioUnitario)).ToList())).ToList();
    }

    public async Task<ComprobanteDto> ObtenerAsync(long id)
    {
        var c=await _db.ComprobantesVenta.AsNoTracking().FirstOrDefaultAsync(x=>x.ComprobanteId==id&&x.SucursalId==Sucursal)
            ??throw ReglaNegocioException.NoEncontrado("Comprobante");
        return (await MapAsync([c])).Single();
    }

    /// <summary>
    /// Auditoría de cuadre fiscal. Revisa, comprobante por comprobante, las tres cuentas que
    /// tienen que dar exactas y devuelve SOLO lo que no calza. Si la lista vuelve vacía,
    /// cada comprobante emitido coincide con su venta al céntimo. Solo lee.
    /// </summary>
    private static string Soles(decimal v) => v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    public async Task<CuadreFiscalDto> CuadreAsync(DateOnly? desde, DateOnly? hasta)
    {
        // Sin fechas audita todo lo emitido en la sucursal. Los límites van en hora Lima.
        var q = _db.ComprobantesVenta.AsNoTracking().Where(c => c.SucursalId == Sucursal);
        if (desde.HasValue) { var ini = DiaLima.Inicio(desde.Value); q = q.Where(c => c.EmitidoEn >= ini); }
        if (hasta.HasValue) { var fin = DiaLima.Inicio(hasta.Value.AddDays(1)); q = q.Where(c => c.EmitidoEn < fin); }
        var docs = await q.ToListAsync();
        if (docs.Count == 0) return new CuadreFiscalDto(0, 0, 0, []);

        var ids = docs.Select(c => c.ComprobanteId).ToList();
        var pedidos = docs.Select(c => c.PedidoId).Distinct().ToList();
        var lineas = await _db.ComprobantesVentaItems.AsNoTracking()
            .Where(i => ids.Contains(i.ComprobanteId))
            .GroupBy(i => i.ComprobanteId)
            .Select(g => new { ComprobanteId = g.Key, Suma = g.Sum(x => x.Total), Cuantas = g.Count() })
            .ToListAsync();
        // Lo cobrado de cada pedido, SIN restar devoluciones: una devolución compensatoria
        // no anula el comprobante emitido (eso exige nota de crédito), así que el documento
        // conserva su total original. Es el mismo criterio que usa la emisión al validar.
        var cobrado = await _db.Pagos.AsNoTracking()
            .Where(p => p.SucursalId == Sucursal && pedidos.Contains(p.PedidoId))
            .GroupBy(p => p.PedidoId).Select(g => new { PedidoId = g.Key, Suma = g.Sum(x => x.Monto) })
            .ToListAsync();

        var observados = new List<CuadreFiscalObservadoDto>();
        decimal totalDocs = 0m;
        foreach (var c in docs)
        {
            totalDocs += c.Total;
            var motivos = new List<string>();
            var suma = lineas.FirstOrDefault(l => l.ComprobanteId == c.ComprobanteId)?.Suma ?? 0m;
            var cuantas = lineas.FirstOrDefault(l => l.ComprobanteId == c.ComprobanteId)?.Cuantas ?? 0;
            if (cuantas == 0) motivos.Add("No tiene líneas.");
            else if (decimal.Round(suma, 2) != decimal.Round(c.Total, 2))
                motivos.Add($"Las líneas suman S/ {Soles(suma)} y el comprobante dice S/ {Soles(c.Total)}.");

            var porAfectacion = decimal.Round(c.SubtotalGravado + c.SubtotalExonerado + c.SubtotalInafecto + c.Igv, 2);
            if (porAfectacion != decimal.Round(c.Total, 2))
                motivos.Add($"Gravado + exonerado + inafecto + IGV da S/ {Soles(porAfectacion)} y el total dice S/ {Soles(c.Total)}.");

            var pagado = cobrado.FirstOrDefault(p => p.PedidoId == c.PedidoId)?.Suma ?? 0m;
            if (decimal.Round(pagado, 2) != decimal.Round(c.Total, 2))
                motivos.Add($"El pedido #{c.PedidoId} tiene S/ {Soles(pagado)} cobrados y el comprobante dice S/ {Soles(c.Total)}.");

            if (motivos.Count > 0)
                observados.Add(new CuadreFiscalObservadoDto(c.ComprobanteId, c.PedidoId, c.Tipo,
                    $"{c.Serie}-{c.Numero:00000000}", c.Estado, c.Total, suma, pagado, motivos));
        }
        return new CuadreFiscalDto(docs.Count, decimal.Round(totalDocs, 2), observados.Count, observados);
    }

    public async Task<List<ComprobanteDto>> ConciliacionAsync()
    {
        var docs=await _db.ComprobantesVenta.AsNoTracking()
            .Where(x=>x.SucursalId==Sucursal && x.Tipo!="NOTA_VENTA" &&
                (x.Estado=="PENDIENTE" || x.Estado=="INCIERTO" || x.Estado=="ERROR"))
            .OrderBy(x=>x.EmitidoEn).Take(100).ToListAsync();
        return await MapAsync(docs);
    }

    public async Task<ComprobanteDto> ReenviarTrasConciliacionAsync(long id, ReenviarCpeDto dto)
    {
        if(!dto.ConfirmoAusenciaEnProveedor) throw new ReglaNegocioException("Confirma que verificaste la ausencia del CPE en Nubefact.");
        var ticket=Texto.Requerido(dto.TicketSoporte,"Ticket de soporte Nubefact",80,3);
        var motivo=Texto.Requerido(dto.Motivo,"Motivo del reenvío",300,5);
        var config=await _db.ConfiguracionesFacturacion.AsNoTracking().FirstOrDefaultAsync(x=>x.TenantId==Tenant)
            ??throw new ReglaNegocioException("Configura Nubefact antes de reintentar el envío.");
        if(config.TokenCifrado is not { Length: > 0 } || string.IsNullOrWhiteSpace(config.RutaApi))
            throw new ReglaNegocioException("Configura la ruta y token de Nubefact antes de reintentar el envío.");

        var comprobante=await _db.ComprobantesVenta.AsNoTracking()
            .FirstOrDefaultAsync(x=>x.ComprobanteId==id&&x.SucursalId==Sucursal&&x.TenantId==Tenant)
            ??throw ReglaNegocioException.NoEncontrado("Comprobante");
        if(comprobante.Tipo=="NOTA_VENTA") throw new ReglaNegocioException("Una nota de venta interna no se envía a Nubefact.");
        if(comprobante.Estado is not ("PENDIENTE" or "INCIERTO" or "ERROR"))
            throw new ReglaNegocioException("Solo se pueden recuperar comprobantes pendientes o con resultado incierto; los rechazados y aceptados no se reenvían.");
        if(comprobante.Estado=="PENDIENTE" && (!comprobante.EnviadoEn.HasValue || comprobante.EnviadoEn.Value>DateTime.UtcNow.AddMinutes(-5)))
            throw new ReglaNegocioException("El envío aún puede estar procesándose. Espera cinco minutos y consulta Nubefact antes de reintentar.");

        var itemsDb=await _db.ComprobantesVentaItems.AsNoTracking().Where(x=>x.ComprobanteId==id&&x.TenantId==Tenant)
            .OrderBy(x=>x.ComprobanteItemId).ToListAsync();
        if(itemsDb.Count==0) throw new ReglaNegocioException("No se encontró el snapshot fiscal del comprobante; no se puede reconstruir el envío.");
        var items=itemsDb.Select(x=>new LineaFiscal(x.Descripcion,x.Cantidad,x.PrecioUnitario,x.AfectacionIgv,x.BaseImponible,x.Igv,x.Total)).ToList();

        var mensaje=$"Reenvío manual solicitado tras verificar ausencia en Nubefact. Ticket {ticket}. Motivo: {motivo}";
        var ahora=DateTime.UtcNow;
        var cutoff=ahora.AddMinutes(-5);
        var estrategia=_db.Database.CreateExecutionStrategy();
        await estrategia.ExecuteAsync(async()=>
        {
            _db.ChangeTracker.Clear();
            await using var tx=await _db.Database.BeginTransactionAsync();
            var filas=await _db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE comprobante_venta
                   SET estado='PENDIENTE', enviado_en={ahora}, mensaje_respuesta={mensaje}
                 WHERE tenant_id={Tenant} AND sucursal_id={Sucursal} AND comprobante_id={id}
                   AND ((estado IN ('INCIERTO','ERROR')) OR (estado='PENDIENTE' AND enviado_en < {cutoff}))");
            if(filas!=1) throw new ReglaNegocioException("El estado cambió o el envío todavía no cumple el tiempo mínimo. Actualiza la conciliación y vuelve a verificar.");
            await _bitacora.AgregarAsync(Tenant,"CPE_REENVIO_AUTORIZADO","Comprobante",id,
                $"{comprobante.Tipo} {comprobante.Serie}-{comprobante.Numero:00000000}. Ticket {ticket}. Motivo: {motivo}");
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        });

        var comprobanteActualizado=await _db.ComprobantesVenta.FirstAsync(x=>x.ComprobanteId==id&&x.SucursalId==Sucursal&&x.TenantId==Tenant);
        await EnviarNubeFactAsync(comprobanteActualizado,config,items);
        return await ObtenerAsync(id);
    }

    public async Task<ComprobanteDto> EmitirAsync(long pedidoId, EmitirComprobanteDto dto)
    {
        var tipo=(dto.Tipo??"").Trim().ToUpperInvariant();
        if(tipo is not ("NOTA_VENTA" or "BOLETA" or "FACTURA")) throw new ReglaNegocioException("Tipo de comprobante no válido.");
        var key=Guid.TryParse(dto.ClaveIdempotencia,out var guid)?guid.ToString("N"):throw new ReglaNegocioException("Envía una clave de idempotencia válida.");
        var pedido=await _db.Pedidos.FirstOrDefaultAsync(p=>p.PedidoId==pedidoId&&p.SucursalId==Sucursal)
            ??throw ReglaNegocioException.NoEncontrado("Pedido");
        if(pedido.Estado!="PAGADO") throw new ReglaNegocioException("Solo se emite comprobante para pedidos pagados.");
        var pagosVenta=await _db.Pagos.Where(x=>x.PedidoId==pedidoId&&x.SucursalId==Sucursal)
            .OrderBy(x=>x.RegistradoEn).ThenBy(x=>x.PagoId).ToListAsync();
        var pago=pagosVenta.FirstOrDefault() ?? throw new ReglaNegocioException("No se encontró el pago de la venta.");
        var totalPagado=pagosVenta.Sum(x=>x.Monto);
        var items=await _db.PedidoItems.AsNoTracking().Where(i=>i.PedidoId==pedidoId&&i.SucursalId==Sucursal&&i.Estado!="ANULADO").OrderBy(i=>i.PedidoItemId).ToListAsync();
        if(items.Count==0) throw new ReglaNegocioException("La venta no contiene ítems.");
        var lineas=CalcularLineas(items,await TasaIgvAsync());
        var total=decimal.Round(lineas.Sum(i=>i.Total),2,MidpointRounding.AwayFromZero);
        if(total!=decimal.Round(totalPagado,2,MidpointRounding.AwayFromZero)) throw new ReglaNegocioException("El total del comprobante no coincide con los pagos aplicados. Revisa el pedido antes de facturar.");
        var nombre=Texto.Requerido(dto.ClienteNombre??(tipo=="NOTA_VENTA"?"Público en general":null),"Nombre del cliente",200,1);
        var doc=(dto.ClienteNumeroDocumento??"").Trim();
        var docTipo=(dto.ClienteTipoDocumento??"").Trim().ToUpperInvariant();
        var direccion=Texto.Opcional(dto.ClienteDireccion,"Dirección",300);
        var email=Texto.Opcional(dto.ClienteEmail,"Email",200);
        if(tipo=="FACTURA" && (docTipo!="RUC"||!Documento.RucValido(doc))) throw new ReglaNegocioException("La factura requiere un RUC válido de 11 dígitos.");
        if(tipo=="BOLETA" && total>700 && (doc.Length==0||docTipo is not ("DNI" or "RUC" or "CE" or "PASAPORTE")))
            throw new ReglaNegocioException("Para boletas mayores a S/ 700 registra tipo y número de documento del cliente.");
        if(doc.Length>0 && docTipo is not ("DNI" or "RUC" or "CE" or "PASAPORTE")) throw new ReglaNegocioException("Tipo de documento del cliente no válido.");
        if(doc.Length>0 && ((docTipo=="DNI"&& !System.Text.RegularExpressions.Regex.IsMatch(doc,@"^\d{8}$")) ||
            (docTipo=="RUC"&&!Documento.RucValido(doc)) || (docTipo is "CE" or "PASAPORTE"&&(doc.Length<5||doc.Length>20))))
            throw new ReglaNegocioException("El número de documento del cliente no coincide con el tipo seleccionado.");

        var previo=await _db.ComprobantesVenta.AsNoTracking()
            .FirstOrDefaultAsync(x=>x.SucursalId==Sucursal&&x.PedidoId==pedidoId&&x.TenantId==Tenant);
        if(previo!=null)
        {
            if(previo.Tipo!=tipo) throw new ReglaNegocioException("El pedido ya tiene un comprobante de otro tipo.",409);
            if(!await SolicitudCoincideAsync(previo,tipo,docTipo,doc,nombre,direccion,email,total,lineas))
                throw new ReglaNegocioException("El pedido ya tiene un comprobante con datos distintos. No se modificó ni reenvió el documento.",409);
            return (await MapAsync([previo])).Single();
        }
        var claveUsada=await _db.ComprobantesVenta.AsNoTracking().AnyAsync(x=>x.TenantId==Tenant&&x.SucursalId==Sucursal&&x.ClaveIdempotencia==key);
        if(claveUsada) throw new ReglaNegocioException("La clave de idempotencia ya se usó para otro comprobante.",409);

        var config=await _db.ConfiguracionesFacturacion.FirstOrDefaultAsync(x=>x.TenantId==Tenant);
        if(tipo!="NOTA_VENTA" && (config?.TokenCifrado==null||string.IsNullOrWhiteSpace(config.RutaApi)))
            throw new ReglaNegocioException("El administrador debe configurar la ruta y el token de Nubefact antes de emitir comprobantes electrónicos.");
        if(tipo!="NOTA_VENTA")
        {
            var emisor=await _db.Tenants.IgnoreQueryFilters().AsNoTracking().FirstAsync(x=>x.TenantId==Tenant);
            if(!Documento.RucValido(emisor.Ruc)) throw new ReglaNegocioException("Completa un RUC válido para la empresa antes de emitir comprobantes electrónicos.");
        }

        ComprobanteVenta? comprobante=null;
        var estrategia=_db.Database.CreateExecutionStrategy();
        try
        {
            await estrategia.ExecuteAsync(async()=>
            {
                _db.ChangeTracker.Clear();
                await using var tx=await _db.Database.BeginTransactionAsync();
                if(tipo!="NOTA_VENTA")
                {
                    var pedidoBloqueado=await _db.Pedidos.FromSqlInterpolated(
                        $"SELECT * FROM pedido WHERE tenant_id={Tenant} AND sucursal_id={Sucursal} AND pedido_id={pedidoId} FOR UPDATE")
                        .IgnoreQueryFilters().SingleOrDefaultAsync()
                        ??throw ReglaNegocioException.NoEncontrado("Pedido");
                    if(pedidoBloqueado.Estado!="PAGADO") throw new ReglaNegocioException("Solo se emite comprobante electrónico para ventas pagadas.");
                    if(await _db.AjustesVenta.AnyAsync(a=>a.PedidoId==pedidoId&&a.SucursalId==Sucursal&&a.TenantId==Tenant&&a.Tipo=="DEVOLUCION"))
                        throw new ReglaNegocioException("La venta ya tiene una devolución registrada. Resuelve el ajuste antes de emitir un comprobante electrónico.");
                }
                var serie=await ObtenerSerieBloqueadaAsync(tipo);
                var numero=serie.SiguienteNumero-1;
                comprobante=new ComprobanteVenta{TenantId=Tenant,SucursalId=Sucursal,PedidoId=pedidoId,PagoId=pago.PagoId,Tipo=tipo,
                    Serie=serie.Serie,Numero=numero,Estado=tipo=="NOTA_VENTA"?"INTERNO":"PENDIENTE",Moneda="PEN",
                    ClienteTipoDocumento=docTipo.Length==0?null:docTipo,ClienteNumeroDocumento=doc.Length==0?null:doc,ClienteNombre=nombre,
                    ClienteDireccion=direccion,ClienteEmail=email,
                    SubtotalGravado=lineas.Where(i=>i.AfectacionIgv=="GRAVADO").Sum(i=>i.BaseImponible),SubtotalExonerado=lineas.Where(i=>i.AfectacionIgv=="EXONERADO").Sum(i=>i.Total),
                    SubtotalInafecto=lineas.Where(i=>i.AfectacionIgv=="INAFECTO").Sum(i=>i.Total),Igv=lineas.Sum(i=>i.Igv),Total=total,TasaIgv=config?.IgvTasa??.18m,
                    ClaveIdempotencia=key,EmitidoPor=Usuario,EmitidoEn=DateTime.UtcNow};
                _db.ComprobantesVenta.Add(comprobante);
                await _db.SaveChangesAsync();
                foreach(var aplicado in pagosVenta)
                    _db.ComprobantesVentaPagos.Add(new ComprobanteVentaPago{TenantId=Tenant,SucursalId=Sucursal,
                        ComprobanteId=comprobante.ComprobanteId,PagoId=aplicado.PagoId,Monto=aplicado.Monto});
                await _db.SaveChangesAsync();
                foreach(var l in lineas) _db.ComprobantesVentaItems.Add(new ComprobanteVentaItem{TenantId=Tenant,ComprobanteId=comprobante.ComprobanteId,
                    Descripcion=l.Descripcion,Cantidad=l.Cantidad,PrecioUnitario=l.PrecioUnitario,AfectacionIgv=l.AfectacionIgv,
                    BaseImponible=l.BaseImponible,Igv=l.Igv,Total=l.Total});
                await _db.SaveChangesAsync();
                await _bitacora.AgregarAsync(Tenant,tipo=="NOTA_VENTA"?"NOTA_VENTA_EMITIDA":"CPE_EMISION_INICIADA","Comprobante",comprobante.ComprobanteId,
                    $"{tipo} {serie.Serie}-{numero:00000000} por S/ {total:0.00}.");
                await _db.SaveChangesAsync();
                await tx.CommitAsync();
            });
        }
        catch(DbUpdateException ex) when(ex.GetBaseException() is PostgresException pg && pg.SqlState=="23505")
        {
            // Dos cajas pueden emitir para el mismo pedido a la vez. La restricción
            // única decide el ganador; el perdedor responde con ese mismo comprobante.
            _db.ChangeTracker.Clear();
            var existente=await _db.ComprobantesVenta.AsNoTracking()
                .FirstOrDefaultAsync(x=>x.SucursalId==Sucursal&&x.PedidoId==pedidoId&&x.TenantId==Tenant);
            if(existente==null)
            {
                var claveExistente=await _db.ComprobantesVenta.AsNoTracking()
                    .AnyAsync(x=>x.TenantId==Tenant&&x.SucursalId==Sucursal&&x.ClaveIdempotencia==key);
                if(claveExistente) throw new ReglaNegocioException("La clave de idempotencia ya se usó para otro comprobante.",409);
                throw;
            }
            if(existente.Tipo!=tipo) throw new ReglaNegocioException("El pedido ya tiene un comprobante de otro tipo.",409);
            if(!await SolicitudCoincideAsync(existente,tipo,docTipo,doc,nombre,direccion,email,total,lineas))
                throw new ReglaNegocioException("El pedido ya tiene un comprobante con datos distintos. No se modificó ni reenvió el documento.",409);
            return (await MapAsync([existente])).Single();
        }
        if(comprobante==null) throw new InvalidOperationException("No se pudo persistir el comprobante.");
        if(tipo!="NOTA_VENTA") await EnviarNubeFactAsync(comprobante,config!,lineas);
        return await ObtenerAsync(comprobante.ComprobanteId);
    }

    private async Task<decimal> TasaIgvAsync()=> (await _db.ConfiguracionesFacturacion.AsNoTracking().FirstOrDefaultAsync(x=>x.TenantId==Tenant))?.IgvTasa??.18m;

    private async Task<bool> SolicitudCoincideAsync(ComprobanteVenta existente,string tipo,string docTipo,string doc,
        string nombre,string? direccion,string? email,decimal total,List<LineaFiscal> lineas)
    {
        if(existente.Tipo!=tipo || existente.ClienteTipoDocumento!=(docTipo.Length==0?null:docTipo) ||
            existente.ClienteNumeroDocumento!=(doc.Length==0?null:doc) || existente.ClienteNombre!=nombre ||
            existente.ClienteDireccion!=direccion || existente.ClienteEmail!=email || existente.Total!=total) return false;
        var snapshot=await _db.ComprobantesVentaItems.AsNoTracking()
            .Where(x=>x.TenantId==Tenant&&x.ComprobanteId==existente.ComprobanteId)
            .OrderBy(x=>x.ComprobanteItemId).ToListAsync();
        if(snapshot.Count!=lineas.Count) return false;
        for(var i=0;i<snapshot.Count;i++)
        {
            var actual=snapshot[i]; var solicitado=lineas[i];
            if(actual.Descripcion!=solicitado.Descripcion || actual.Cantidad!=solicitado.Cantidad ||
                actual.PrecioUnitario!=solicitado.PrecioUnitario || actual.AfectacionIgv!=solicitado.AfectacionIgv ||
                actual.Total!=solicitado.Total) return false;
        }
        return true;
    }

    private async Task<ComprobanteSerie> ObtenerSerieBloqueadaAsync(string tipo)
    {
        return await _db.ComprobantesSerie.FromSqlInterpolated($"SELECT * FROM reservar_serie_comprobante({Tenant},{Sucursal},{tipo})")
            .IgnoreQueryFilters().AsNoTracking().SingleAsync();
    }

    private record LineaFiscal(string Descripcion,int Cantidad,decimal PrecioUnitario,string AfectacionIgv,decimal BaseImponible,decimal Igv,decimal Total);
    private static List<LineaFiscal> CalcularLineas(List<PedidoItem> items,decimal tasa)
    {
        var r=new List<LineaFiscal>();
        foreach(var i in items)
        {
            var total=decimal.Round(i.Cantidad*i.PrecioUnitario,2,MidpointRounding.AwayFromZero);
            var afectacion=i.AfectacionIgv.Trim().ToUpperInvariant();
            if(afectacion is not ("GRAVADO" or "EXONERADO" or "INAFECTO")) throw new ReglaNegocioException($"El producto {i.Nombre} no tiene afectación IGV válida.");
            var baseNeta=afectacion=="GRAVADO"?decimal.Round(total/(1+tasa),2,MidpointRounding.AwayFromZero):total;
            var igv=afectacion=="GRAVADO"?total-baseNeta:0m;
            r.Add(new LineaFiscal(PedidoService.DescripcionItem(i.Nombre,i.SeleccionJson),i.Cantidad,i.PrecioUnitario,afectacion,baseNeta,igv,total));
        }
        return r;
    }

    private async Task EnviarNubeFactAsync(ComprobanteVenta c,ConfiguracionFacturacion config,List<LineaFiscal> items)
    {
        var solicitudIniciada=false;
        try
        {
            // Guardar el inicio antes de la llamada externa. Si el proceso cae aquí,
            // el comprobante queda visible para conciliación y jamás se reenvía solo.
            c.EnviadoEn=DateTime.UtcNow;
            await _db.SaveChangesAsync();
            var token=_protector.Unprotect(Encoding.UTF8.GetString(config.TokenCifrado!));
            var tenant=await _db.Tenants.IgnoreQueryFilters().AsNoTracking().FirstAsync(x=>x.TenantId==Tenant);
            var pagos=await _db.ComprobantesVentaPagos.AsNoTracking().Where(x=>x.ComprobanteId==c.ComprobanteId&&x.SucursalId==Sucursal)
                .Join(_db.Pagos.AsNoTracking(),x=>x.PagoId,p=>p.PagoId,(x,p)=>new { p.Medio,x.Monto })
                .OrderBy(x=>x.Medio).ToListAsync();
            var resumenPagos=pagos.Count==0?$"Pago {c.PagoId}":string.Join(", ",pagos.Select(x=>$"{x.Medio} S/ {x.Monto:0.00}"));
            var request=NubeFactPayload(tenant,c,items,resumenPagos);
            using var client=_httpFactory.CreateClient("nubefact");
            using var message=new HttpRequestMessage(HttpMethod.Post,config.RutaApi);
            message.Headers.Authorization=new AuthenticationHeaderValue("Token",token);
            message.Content=new StringContent(JsonSerializer.Serialize(request),Encoding.UTF8,"application/json");
            solicitudIniciada=true;
            using var response=await client.SendAsync(message);
            var body=await response.Content.ReadAsStringAsync();
            JsonObject? json=null; try{json=JsonNode.Parse(body) as JsonObject;}catch{ }
            c.NubefactRespuesta=NormalizarRespuestaJson(body);
            var accepted=BoolValue(json?["aceptada_por_sunat"]);
            // HTTP 2xx solo confirma que hubo respuesta HTTP. Sin el indicador
            // explícito de SUNAT no se puede asumir rechazo ni permitir una nueva emisión.
            var indeterminado=accepted is null && (response.IsSuccessStatusCode ||
                (int)response.StatusCode>=500 || response.StatusCode is System.Net.HttpStatusCode.RequestTimeout or System.Net.HttpStatusCode.TooManyRequests);
            c.Estado=accepted==true?"ACEPTADO":accepted==false?"RECHAZADO":indeterminado?"INCIERTO":"ERROR";
            c.MensajeRespuesta=LimitarMensaje(ReadString(json,"sunat_description")??ReadString(json,"errors")??(indeterminado
                ?$"Nubefact respondió HTTP {(int)response.StatusCode}, pero no confirmó aceptación ni rechazo por SUNAT. Verifica el documento en Nubefact antes de tomar otra acción. No reenvíes automáticamente."
                :response.IsSuccessStatusCode?"Nubefact no confirmó aceptación por SUNAT.":$"Nubefact respondió HTTP {(int)response.StatusCode}."));
            c.PdfUrl=ReadString(json,"enlace_del_pdf"); c.XmlUrl=ReadString(json,"enlace_del_xml"); c.HashCpe=ReadString(json,"sunat_hash");
            c.QrContenido=ReadString(json,"cadena_para_codigo_qr"); c.CdrUrl=ReadString(json,"enlace_del_cdr");
            await _db.SaveChangesAsync();
            await _bitacora.AgregarAsync(Tenant,accepted==true?"CPE_ACEPTADO":indeterminado?"CPE_INCIERTO":"CPE_RESULTADO","Comprobante",c.ComprobanteId,
                $"{c.Tipo} {c.Serie}-{c.Numero:00000000}: {c.Estado}."); await _db.SaveChangesAsync();
        }
        catch(Exception ex)
        {
            _logger.LogError(ex,"Error al enviar comprobante {ComprobanteId} a Nubefact",c.ComprobanteId);
            // Un fallo posterior a guardar el resultado (por ejemplo, bitácora caída)
            // no puede degradar una respuesta definitiva del proveedor.
            if(c.Estado is "ACEPTADO" or "RECHAZADO" or "ERROR") return;
            c.Estado=solicitudIniciada?"INCIERTO":"ERROR";
            c.MensajeRespuesta=solicitudIniciada
                ?"No se pudo confirmar la respuesta de Nubefact. Verifica este número en Nubefact antes de cualquier reintento; no se generará un envío automático."
                :"No se pudo preparar el envío a Nubefact. El sistema no reintentará automáticamente; verifica la configuración antes de volver a operar.";
            await _db.SaveChangesAsync();
            try
            {
                await _bitacora.AgregarAsync(Tenant,solicitudIniciada?"CPE_INCIERTO":"CPE_ERROR","Comprobante",c.ComprobanteId,
                    $"{c.Tipo} {c.Serie}-{c.Numero:00000000}: {(solicitudIniciada?"respuesta externa no confirmada":"fallo antes de iniciar la solicitud externa")}");
                await _db.SaveChangesAsync();
            }
            catch(Exception auditEx) { _logger.LogError(auditEx,"No se pudo registrar auditoría del comprobante incierto {ComprobanteId}",c.ComprobanteId); }
        }
    }

    private static string? ReadString(JsonObject? node,string key)
    { var v=node?[key]; return v is JsonValue j&&j.TryGetValue<string>(out var s)?s:v?.ToJsonString(); }

    private static bool? BoolValue(JsonNode? node)
    {
        if(node is not JsonValue value) return null;
        if(value.TryGetValue<bool>(out var boolean)) return boolean;
        return value.TryGetValue<string>(out var text)&&bool.TryParse(text,out var parsed)?parsed:null;
    }

    private static string LimitarMensaje(string value) => value.Length<=2000?value:value[..1997]+"...";

    private static TimeZoneInfo ResolverZonaPeru()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Lima"); }
        catch(TimeZoneNotFoundException)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time"); }
            catch(TimeZoneNotFoundException)
            {
                return TimeZoneInfo.CreateCustomTimeZone("America/Lima-UTC-05",TimeSpan.FromHours(-5),"America/Lima","America/Lima");
            }
        }
    }

    private static string FechaEmisionPeru(DateTime fechaUtc)
    {
        var utc=DateTime.SpecifyKind(fechaUtc,DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc,ZonaPeru).ToString("dd-MM-yyyy",System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string NormalizarRespuestaJson(string body)
    {
        if(body.Length<=30000)
        {
            try { using var _=JsonDocument.Parse(body); return body; }
            catch(JsonException) { }
        }
        var fragmento=body.Length<=28000?body:body[..28000];
        return JsonSerializer.Serialize(new { respuesta_truncada=body.Length>fragmento.Length, respuesta_texto=fragmento });
    }

    private static object NubeFactPayload(Tenant t,ComprobanteVenta c,List<LineaFiscal> items,string resumenPagos)
    {
        // JSON V1 de Nubefact: numeración administrada localmente por serie/sucursal.
        var tipo=c.Tipo=="FACTURA"?1:2;
        var sunatDoc=c.ClienteTipoDocumento switch{"RUC"=>6,"DNI"=>1,"CE"=>4,"PASAPORTE"=>7,_=>0};
        return new { operacion="generar_comprobante",tipo_de_comprobante=tipo,serie=c.Serie,numero=c.Numero,
            sunat_transaction=1,cliente_tipo_de_documento=sunatDoc,cliente_numero_de_documento=c.ClienteNumeroDocumento??"",
            cliente_denominacion=c.ClienteNombre,cliente_direccion=c.ClienteDireccion??"",cliente_email=c.ClienteEmail??"",
            fecha_de_emision=FechaEmisionPeru(c.EmitidoEn),moneda=1,porcentaje_de_igv=c.TasaIgv*100,
            total_gravada=c.SubtotalGravado,total_exonerada=c.SubtotalExonerado,total_inafecta=c.SubtotalInafecto,
            total_igv=c.Igv,total=c.Total,observaciones=$"Pagos: {resumenPagos} · Empresa: {t.RazonSocial??t.NombreComercial}",
            items=items.Select(x=>new { unidad_de_medida="NIU",codigo="",descripcion=x.Descripcion,cantidad=x.Cantidad,
                valor_unitario=x.AfectacionIgv=="GRAVADO"?x.BaseImponible/x.Cantidad:x.Total/x.Cantidad,
                precio_unitario=x.PrecioUnitario,descuento=0,subtotal=x.BaseImponible, tipo_de_igv=x.AfectacionIgv switch{"GRAVADO"=>1,"EXONERADO"=>8,_=>9},
                igv=x.Igv,total=x.Total }).ToArray() };
    }

    private async Task<List<ComprobanteDto>> MapAsync(List<ComprobanteVenta> docs)
    {
        var ids=docs.Select(x=>x.ComprobanteId).ToList();
        var lines=await _db.ComprobantesVentaItems.AsNoTracking().Where(x=>ids.Contains(x.ComprobanteId)).OrderBy(x=>x.ComprobanteItemId).ToListAsync();
        return docs.Select(c=>new ComprobanteDto(c.ComprobanteId,c.PedidoId,c.Tipo,c.Serie,c.Numero,c.Estado,c.ClienteNombre,
            c.ClienteTipoDocumento,c.ClienteNumeroDocumento,c.SubtotalGravado,c.SubtotalExonerado,c.SubtotalInafecto,c.Igv,c.Total,c.Moneda,
            c.EmitidoEn,c.EnviadoEn,c.MensajeRespuesta,c.PdfUrl,c.XmlUrl,c.HashCpe,c.QrContenido,c.CdrUrl,lines.Where(x=>x.ComprobanteId==c.ComprobanteId)
              .Select(x=>new ComprobanteItemDto(x.Descripcion,x.Cantidad,x.PrecioUnitario,x.AfectacionIgv,x.BaseImponible,x.Igv,x.Total)).ToList())).ToList();
    }
}
