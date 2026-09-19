using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ValexaFood.Application.Common;
using ValexaFood.Domain.Entities;
using ValexaFood.Infrastructure.Context;
using ValexaFood.Infrastructure.Sesion;

namespace ValexaFood.Application.Services;

public record RegistrarMovimientoDto(string? Tipo, int Meses, decimal Monto, string? MetodoPago, string? NumeroOperacion,
    DateOnly? FechaPago, string? Comprobante, string? Observacion);
public record AnularMovimientoDto(string? Motivo);

public record MovimientoDto(int MovimientoId, int TenantId, string Empresa, string Tipo, int PlanId, string PlanNombre,
    decimal PrecioReferencial, int Meses, decimal Monto, decimal MontoSinIgv, decimal DescuentoPct, string? MetodoPago,
    string? NumeroOperacion, DateOnly FechaPago, string? Comprobante, string? Observacion, DateOnly? VenceAntes,
    DateOnly VenceDespues, string RegistradoPor, DateTime FechaRegistro, string Estado, string? MotivoAnulacion,
    string? AnuladoPor, DateTime? FechaAnulacion, bool Anulable);
public record ResultadoMovimientoDto(MovimientoDto Movimiento, string? Advertencia);

public record SugerenciaCobroDto(int TenantId, string Empresa, string? PlanNombre, decimal PrecioMensual, decimal? PrecioAnual,
    int Meses, decimal PrecioLista, bool BienvenidaDisponible, decimal DescuentoBienvenidaPct, decimal MontoSugerido,
    DateOnly? VenceActual, DateOnly VenceraHasta);

public record CobranzaDto(int TenantId, string Nombre, string? Ruc, string? Telefono, string? Email, bool Activo,
    int? PlanId, string? PlanNombre, decimal? PrecioMensual, decimal? PrecioAnual, DateOnly? Vencimiento,
    int? DiasParaVencer, string Estado, DateOnly? UltimoPagoFecha, decimal? UltimoPagoMonto, decimal Pagado12Meses,
    bool CelularPeru, bool BienvenidaDisponible);

public record TableroMesDto(string Mes, decimal Cobrado, int Altas);
public record TableroPlanDto(int PlanId, string Plan, int Empresas, decimal IngresoMensual);
public record TableroAlertaDto(int TenantId, string Empresa, string? Plan, DateOnly? Vencimiento, int? Dias, string Detalle);
public record TableroDto(DateOnly Hoy, decimal IngresoRecurrente, decimal IngresoRecurrenteSinIgv, decimal TicketPromedio,
    decimal CobradoMes, decimal CobradoMesSinIgv, decimal CobradoMesAnterior, decimal? VariacionPct, decimal DescuentosMes,
    int Empresas, int AlDia, int PorVencer, int Vencidas, int Suspendidas, int SinPlan, int AltasMes, int BajasMes,
    int UsuariosActivos, int SucursalesActivas, List<TableroMesDto> Serie, List<TableroPlanDto> PorPlan,
    List<TableroAlertaDto> PorVencerLista, List<TableroAlertaDto> VencidasLista, List<TableroAlertaDto> Excedidas);

// REGLA DE ORO: ningún día de servicio sin su constancia.
// El vencimiento del plan se mueve SOLO desde aquí, en la misma transacción
// que el movimiento (PAGO o CORTESIA) y con la empresa bloqueada.
public class SuscripcionService
{
    public static readonly string[] Metodos = ["TRANSFERENCIA", "YAPE_PLIN", "DEPOSITO", "TARJETA", "EFECTIVO"];
    private const decimal FactorIgv = 1.18m;

    private readonly ValexaFoodContext _db;
    private readonly ISesionActual _sesion;
    private readonly BitacoraService _bitacora;
    private readonly LimitePlan _limite;
    private readonly CacheSesiones _cache;

    public SuscripcionService(ValexaFoodContext db, ISesionActual sesion, BitacoraService bitacora, LimitePlan limite, CacheSesiones cache)
    {
        _db = db;
        _sesion = sesion;
        _bitacora = bitacora;
        _limite = limite;
        _cache = cache;
    }

    private async Task<ConfiguracionSaas> ConfigAsync() =>
        await _db.ConfiguracionSaas.AsNoTracking().FirstOrDefaultAsync() ?? new ConfiguracionSaas { DescuentoBienvenidaPct = 20, DiasPorVencer = 7 };

    private Task<bool> TuvoPagoAsync(int tenantId) =>
        _db.SuscripcionMovimientos.AnyAsync(m => m.TenantId == tenantId && m.Tipo == "PAGO" && m.Estado == "VIGENTE");

    // ─── Sugerencia antes de cobrar ──────────────────────────────────────────
    public async Task<SugerenciaCobroDto> SugerirAsync(int tenantId, int meses)
    {
        if (meses is < 1 or > 36) throw new ReglaNegocioException("Los meses deben estar entre 1 y 36.");
        var t = await _db.Tenants.AsNoTracking().Include(x => x.Plan).FirstOrDefaultAsync(x => x.TenantId == tenantId)
            ?? throw ReglaNegocioException.NoEncontrado("Empresa");
        if (t.Plan == null) throw new ReglaNegocioException("La empresa no tiene plan asignado.");
        var cfg = await ConfigAsync();
        var lista = PrecioLista(t.Plan, meses);
        var bienvenida = !await TuvoPagoAsync(tenantId) && cfg.DescuentoBienvenidaPct > 0;
        // El descuento de bienvenida aplica a la PRIMERA mensualidad, no a todo el periodo.
        var sugerido = bienvenida ? Math.Round(lista - t.Plan.PrecioMensual * cfg.DescuentoBienvenidaPct / 100, 2) : lista;
        var hoy = DiaLima.Hoy();
        var desde = t.PlanFechaVencimiento.HasValue && t.PlanFechaVencimiento.Value > hoy ? t.PlanFechaVencimiento.Value : hoy;
        return new(tenantId, t.NombreComercial, t.Plan.Nombre, t.Plan.PrecioMensual, t.Plan.PrecioAnual, meses, lista,
            bienvenida, cfg.DescuentoBienvenidaPct, sugerido, t.PlanFechaVencimiento, desde.AddMonths(meses));
    }

    private static decimal PrecioLista(Plan plan, int meses) =>
        meses == 12 && plan.PrecioAnual.HasValue ? plan.PrecioAnual.Value : plan.PrecioMensual * meses;

    // ─── Registrar ───────────────────────────────────────────────────────────
    public async Task<ResultadoMovimientoDto> RegistrarAsync(int tenantId, RegistrarMovimientoDto dto)
    {
        if (!await _db.Tenants.AnyAsync(t => t.TenantId == tenantId)) throw ReglaNegocioException.NoEncontrado("Empresa");

        int movimientoId = 0;
        string? advertencia = null;
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            var tenant = await BloquearAsync(tenantId);
            (movimientoId, advertencia) = await RegistrarEnTransaccionAsync(tenant, dto);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        });
        _cache.Empresa(tenantId);
        return new((await ListarAsync(null, movimientoId, 1)).First(), advertencia);
    }

    // Bloquea la fila de la empresa: dos cobros a la vez no parten de la misma fecha.
    internal async Task<Tenant> BloquearAsync(int tenantId)
    {
        await _db.Database.ExecuteSqlAsync($"SELECT 1 FROM tenants WHERE tenant_id = {tenantId} FOR UPDATE");
        var tenant = await _db.Tenants.Include(t => t.Plan).FirstAsync(t => t.TenantId == tenantId);
        await _db.Entry(tenant).ReloadAsync();
        return tenant;
    }

    // Lo usa también el alta de empresa, dentro de su propia transacción. No guarda.
    internal async Task<(int id, string? advertencia)> RegistrarEnTransaccionAsync(Tenant tenant, RegistrarMovimientoDto dto)
    {
        var tipo = (dto.Tipo ?? "").Trim().ToUpperInvariant();
        if (tipo is not ("PAGO" or "CORTESIA")) throw new ReglaNegocioException("El tipo debe ser PAGO o CORTESIA.");
        if (dto.Meses is < 1 or > 36) throw new ReglaNegocioException("Los meses deben estar entre 1 y 36.");

        var hoy = DiaLima.Hoy();
        var fecha = dto.FechaPago ?? hoy;
        if (fecha > hoy) throw new ReglaNegocioException("La fecha del pago no puede ser futura.");
        if (fecha < hoy.AddYears(-1)) throw new ReglaNegocioException("La fecha del pago no puede tener más de un año.");

        var observacion = Texto.Opcional(dto.Observacion, "Observación", 300);
        var comprobante = Texto.Opcional(dto.Comprobante, "Comprobante", 30)?.ToUpperInvariant();

        if (tenant.PlanId == null || tenant.Plan == null)
            throw new ReglaNegocioException("La empresa no tiene un plan asignado: asígnale uno antes de registrar el cobro.");

        string? metodo = null, operacion = null;
        decimal monto = 0;
        if (tipo == "PAGO")
        {
            monto = dto.Monto;
            if (monto <= 0) throw new ReglaNegocioException("El monto pagado debe ser mayor a cero.");
            if (Math.Round(monto, 2) != monto) throw new ReglaNegocioException("El monto admite como máximo 2 decimales.");
            if (monto > 100000) throw new ReglaNegocioException("El monto parece un error de digitación: revisa la cifra.");
            metodo = (dto.MetodoPago ?? "").Trim().ToUpperInvariant();
            if (!Metodos.Contains(metodo)) throw new ReglaNegocioException($"Elige el medio de pago: {string.Join(", ", Metodos)}.");
            if (metodo != "EFECTIVO")
            {
                operacion = Regex.Replace((dto.NumeroOperacion ?? "").ToUpperInvariant(), @"\s", "");
                if (!Regex.IsMatch(operacion, @"^[A-Z0-9\-]{4,50}$"))
                    throw new ReglaNegocioException("Indica el N° de operación (4 a 50 letras o números): sin él no se puede cuadrar contra el banco.");
                if (await _db.SuscripcionMovimientos.AnyAsync(m => m.MetodoPago == metodo && m.NumeroOperacion == operacion && m.Estado == "VIGENTE"))
                    throw new ReglaNegocioException($"El pago con N° de operación {operacion} por {metodo} ya se registró. El mismo pago no se registra dos veces.");
            }
        }
        else if (observacion == null || observacion.Length < 5)
        {
            throw new ReglaNegocioException("Una cortesía necesita el motivo (mínimo 5 caracteres): son días de servicio sin pago.");
        }

        var venceAntes = tenant.PlanFechaVencimiento;
        // Si ya venció cuenta desde hoy; si no, se extiende desde su vencimiento: no pierde lo pagado.
        var desde = venceAntes.HasValue && venceAntes.Value > hoy ? venceAntes.Value : hoy;
        var venceDespues = desde.AddMonths(dto.Meses);
        var referencial = PrecioLista(tenant.Plan, dto.Meses);
        var descuento = tipo == "PAGO" && referencial > 0 && monto < referencial
            ? Math.Round((1 - monto / referencial) * 100, 2) : 0;

        var mov = new SuscripcionMovimiento
        {
            TenantId = tenant.TenantId,
            Tipo = tipo,
            PlanId = tenant.Plan.PlanId,
            PlanNombre = tenant.Plan.Nombre,
            PrecioReferencial = referencial,
            Meses = dto.Meses,
            Monto = monto,
            DescuentoPct = descuento,
            MetodoPago = metodo,
            NumeroOperacion = operacion,
            FechaPago = fecha,
            Comprobante = comprobante,
            Observacion = observacion,
            VenceAntes = venceAntes,
            VenceDespues = venceDespues,
            RegistradoPor = _sesion.UsuarioId!.Value,
            FechaRegistro = DateTime.UtcNow,
            Estado = "VIGENTE"
        };
        _db.SuscripcionMovimientos.Add(mov);

        tenant.PlanFechaVencimiento = venceDespues;
        tenant.PlanFechaInicio ??= hoy;
        tenant.PlanAvisoUltimoUmbral = null;
        tenant.PlanAvisoParaFecha = null;
        tenant.UpdatedAt = DateTime.UtcNow;

        await _bitacora.AgregarAsync(tenant.TenantId, tipo == "PAGO" ? "SUSCRIPCION_PAGO" : "SUSCRIPCION_CORTESIA",
            "Suscripcion", null,
            tipo == "PAGO"
                ? $"Pago de {dto.Meses} mes(es) del plan {tenant.Plan.Nombre}: S/ {Texto.Soles(monto)} por {metodo}{(operacion != null ? $" N° {operacion}" : "")}. Vence {venceDespues:dd/MM/yyyy}."
                : $"Cortesía de {dto.Meses} mes(es): {observacion}. Vence {venceDespues:dd/MM/yyyy}.");

        await _db.SaveChangesAsync();

        string? advertencia = null;
        if (!tenant.Activo)
            advertencia = $"{tenant.NombreComercial} sigue SUSPENDIDA: el cobro quedó registrado, pero para que vuelva a entrar reactívala.";
        else if (descuento > 0)
            advertencia = $"Se cobró S/ {Texto.Soles(monto)}, {descuento:0.##} % menos que el precio de lista (S/ {Texto.Soles(referencial)}): queda registrado como descuento.";
        return (mov.MovimientoId, advertencia);
    }

    // ─── Anular ──────────────────────────────────────────────────────────────
    public async Task<MovimientoDto> AnularAsync(int movimientoId, AnularMovimientoDto dto)
    {
        var motivo = Texto.Requerido(dto.Motivo, "Motivo de la anulación", 300, 5);
        var original = await _db.SuscripcionMovimientos.AsNoTracking().FirstOrDefaultAsync(m => m.MovimientoId == movimientoId)
            ?? throw ReglaNegocioException.NoEncontrado("Movimiento");
        if (original.Estado != "VIGENTE") throw new ReglaNegocioException("Ese movimiento ya fue anulado.");

        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            var tenant = await BloquearAsync(original.TenantId);

            var ultimo = await _db.SuscripcionMovimientos
                .Where(m => m.TenantId == original.TenantId && m.Estado == "VIGENTE").MaxAsync(m => (int?)m.MovimientoId);
            if (ultimo != movimientoId)
                throw new ReglaNegocioException("Solo se puede anular el último movimiento de la empresa: anula primero los posteriores.");
            if (tenant.PlanFechaVencimiento != original.VenceDespues)
                throw new ReglaNegocioException($"El vencimiento de la empresa ya no es el que dejó este movimiento ({original.VenceDespues:dd/MM/yyyy}).");

            var tomados = await _db.SuscripcionMovimientos
                .Where(m => m.MovimientoId == movimientoId && m.Estado == "VIGENTE")
                .ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.Estado, "ANULADO")
                    .SetProperty(m => m.MotivoAnulacion, motivo)
                    .SetProperty(m => m.AnuladoPor, _sesion.UsuarioId)
                    .SetProperty(m => m.FechaAnulacion, DateTime.UtcNow));
            if (tomados == 0) throw new ReglaNegocioException("Ese movimiento ya fue anulado.");

            tenant.PlanFechaVencimiento = original.VenceAntes;
            tenant.PlanAvisoUltimoUmbral = null;
            tenant.PlanAvisoParaFecha = null;
            tenant.UpdatedAt = DateTime.UtcNow;
            await _bitacora.AgregarAsync(tenant.TenantId, "SUSCRIPCION_ANULADA", "Suscripcion", movimientoId,
                $"Se anuló el {original.Tipo} de {original.Meses} mes(es) (S/ {Texto.Soles(original.Monto)}): {motivo}. " +
                $"El vencimiento vuelve a {(original.VenceAntes.HasValue ? original.VenceAntes.Value.ToString("dd/MM/yyyy") : "sin fecha")}.");
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        });
        _cache.Empresa(original.TenantId);
        return (await ListarAsync(null, movimientoId, 1)).First();
    }

    public async Task<List<MovimientoDto>> ListarAsync(int? tenantId, int? movimientoId = null, int limite = 100)
    {
        var q = _db.SuscripcionMovimientos.AsNoTracking()
            .Include(m => m.Tenant)
            .Include(m => m.RegistradoPorUsuario)
            .Include(m => m.AnuladoPorUsuario)
            .IgnoreQueryFilters().AsQueryable();
        if (tenantId.HasValue) q = q.Where(m => m.TenantId == tenantId);
        if (movimientoId.HasValue) q = q.Where(m => m.MovimientoId == movimientoId);
        var movs = await q.OrderByDescending(m => m.MovimientoId).Take(Math.Clamp(limite, 1, 500)).ToListAsync();

        var tenants = movs.Select(m => m.TenantId).Distinct().ToList();
        var ultimos = await _db.SuscripcionMovimientos.AsNoTracking()
            .Where(m => tenants.Contains(m.TenantId) && m.Estado == "VIGENTE")
            .GroupBy(m => m.TenantId).Select(g => new { TenantId = g.Key, Ultimo = g.Max(m => m.MovimientoId) }).ToListAsync();

        return movs.Select(m => new MovimientoDto(m.MovimientoId, m.TenantId, m.Tenant?.NombreComercial ?? "", m.Tipo,
            m.PlanId, m.PlanNombre, m.PrecioReferencial, m.Meses, m.Monto, Math.Round(m.Monto / FactorIgv, 2), m.DescuentoPct,
            m.MetodoPago, m.NumeroOperacion, m.FechaPago, m.Comprobante, m.Observacion, m.VenceAntes, m.VenceDespues,
            m.RegistradoPorUsuario?.Nombre ?? "", m.FechaRegistro, m.Estado, m.MotivoAnulacion, m.AnuladoPorUsuario?.Nombre,
            m.FechaAnulacion,
            m.Estado == "VIGENTE" && ultimos.Any(u => u.TenantId == m.TenantId && u.Ultimo == m.MovimientoId)
                && m.Tenant?.PlanFechaVencimiento == m.VenceDespues)).ToList();
    }

    // ─── Cobranza ────────────────────────────────────────────────────────────
    public static string Estado(Tenant t, int? dias, int diasPorVencer)
    {
        if (!t.Activo) return "SUSPENDIDA";
        if (t.PlanId == null) return "SIN_PLAN";
        if (!dias.HasValue) return "SIN_VENCIMIENTO";
        if (dias.Value < 0) return "VENCIDA";
        return dias.Value <= diasPorVencer ? "POR_VENCER" : "AL_DIA";
    }

    private static int OrdenEstado(string e) => e switch
    {
        "VENCIDA" => 0, "POR_VENCER" => 1, "SIN_PLAN" => 2, "SUSPENDIDA" => 3, "SIN_VENCIMIENTO" => 4, _ => 5
    };

    public async Task<List<CobranzaDto>> CobranzaAsync()
    {
        var hoy = DiaLima.Hoy();
        var cfg = await ConfigAsync();
        var desde12 = hoy.AddMonths(-12);
        var tenants = await _db.Tenants.AsNoTracking().Include(t => t.Plan).ToListAsync();
        var pagos = await _db.SuscripcionMovimientos.AsNoTracking()
            .Where(m => m.Tipo == "PAGO" && m.Estado == "VIGENTE")
            .Select(m => new { m.TenantId, m.MovimientoId, m.FechaPago, m.Monto }).ToListAsync();

        return tenants.Select(t =>
        {
            var suyos = pagos.Where(p => p.TenantId == t.TenantId).ToList();
            var ultimo = suyos.OrderByDescending(p => p.FechaPago).ThenByDescending(p => p.MovimientoId).FirstOrDefault();
            int? dias = t.PlanFechaVencimiento.HasValue ? t.PlanFechaVencimiento.Value.DayNumber - hoy.DayNumber : null;
            return new CobranzaDto(t.TenantId, t.NombreComercial, t.Ruc, t.Telefono, t.Email, t.Activo, t.PlanId,
                t.Plan?.Nombre, t.Plan?.PrecioMensual, t.Plan?.PrecioAnual, t.PlanFechaVencimiento, dias,
                Estado(t, dias, cfg.DiasPorVencer), ultimo?.FechaPago, ultimo?.Monto,
                suyos.Where(p => p.FechaPago > desde12).Sum(p => p.Monto), Documento.EsCelularPeru(t.Telefono),
                suyos.Count == 0 && cfg.DescuentoBienvenidaPct > 0);
        })
        .OrderBy(c => OrdenEstado(c.Estado)).ThenBy(c => c.DiasParaVencer ?? int.MaxValue).ThenBy(c => c.Nombre)
        .ToList();
    }

    // ─── Tablero ─────────────────────────────────────────────────────────────
    public async Task<TableroDto> TableroAsync()
    {
        var hoy = DiaLima.Hoy();
        var inicioMes = new DateOnly(hoy.Year, hoy.Month, 1);
        var inicioAnterior = inicioMes.AddMonths(-1);
        var inicio12 = inicioMes.AddMonths(-11);

        var cobranza = await CobranzaAsync();
        var tenants = await _db.Tenants.AsNoTracking().Include(t => t.Plan).ToListAsync();
        var pagos = await _db.SuscripcionMovimientos.AsNoTracking()
            .Where(m => m.Tipo == "PAGO" && m.Estado == "VIGENTE" && m.FechaPago >= inicio12)
            .Select(m => new { m.FechaPago, m.Monto, m.PrecioReferencial }).ToListAsync();

        var cobradoMes = pagos.Where(p => p.FechaPago >= inicioMes).Sum(p => p.Monto);
        var cobradoAnterior = pagos.Where(p => p.FechaPago >= inicioAnterior && p.FechaPago < inicioMes).Sum(p => p.Monto);
        var descuentosMes = pagos.Where(p => p.FechaPago >= inicioMes).Sum(p => Math.Max(0, p.PrecioReferencial - p.Monto));

        // Ingreso mensual recurrente: precio de lista mensual de cada empresa con servicio vigente.
        var conServicio = cobranza.Where(c => c.Estado is "AL_DIA" or "POR_VENCER" && c.PrecioMensual.HasValue).ToList();
        var recurrente = conServicio.Sum(c => c.PrecioMensual!.Value);

        var altasPorMes = tenants.Select(t => DiaLima.DeInstante(t.CreatedAt)).ToList();
        var serie = Enumerable.Range(0, 12).Select(i =>
        {
            var mes = inicio12.AddMonths(i);
            var sig = mes.AddMonths(1);
            return new TableroMesDto(mes.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                pagos.Where(p => p.FechaPago >= mes && p.FechaPago < sig).Sum(p => p.Monto),
                altasPorMes.Count(d => d >= mes && d < sig));
        }).ToList();

        var porPlan = conServicio.GroupBy(c => new { c.PlanId, c.PlanNombre })
            .Select(g => new TableroPlanDto(g.Key.PlanId ?? 0, g.Key.PlanNombre ?? "", g.Count(), g.Sum(c => c.PrecioMensual ?? 0)))
            .OrderByDescending(p => p.IngresoMensual).ToList();

        var inicioMesUtc = DiaLima.Inicio(inicioMes);
        var altas = tenants.Count(t => t.CreatedAt >= inicioMesUtc);
        var bajas = tenants.Count(t => !t.Activo && t.FechaSuspension >= inicioMesUtc);

        var uso = await _limite.UsoAsync();
        var roles = await _limite.NombresRolAsync();
        var cupos = await _db.PlanRolCupos.AsNoTracking().ToListAsync();
        var excedidas = tenants.Where(t => t.Plan != null && t.Activo && uso.ContainsKey(t.TenantId)).Select(t =>
        {
            var probs = LimitePlan.Problemas(uso[t.TenantId], t.Plan!.MaxSucursales, t.Plan.MaxUsuarios,
                cupos.Where(c => c.PlanId == t.PlanId).Select(c => (c.RolId, c.Maximo)), roles);
            return probs.Count == 0 ? null : new TableroAlertaDto(t.TenantId, t.NombreComercial, t.Plan.Nombre,
                t.PlanFechaVencimiento, null, $"Excede su plan: {string.Join("; ", probs)}.");
        }).Where(x => x != null).Select(x => x!).ToList();

        TableroAlertaDto Alerta(CobranzaDto c, string d) => new(c.TenantId, c.Nombre, c.PlanNombre, c.Vencimiento, c.DiasParaVencer, d);

        return new TableroDto(hoy, recurrente, Math.Round(recurrente / FactorIgv, 2),
            conServicio.Count > 0 ? Math.Round(recurrente / conServicio.Count, 2) : 0,
            cobradoMes, Math.Round(cobradoMes / FactorIgv, 2), cobradoAnterior,
            cobradoAnterior > 0 ? Math.Round((cobradoMes - cobradoAnterior) / cobradoAnterior * 100, 1) : null,
            descuentosMes,
            cobranza.Count,
            cobranza.Count(c => c.Estado == "AL_DIA"),
            cobranza.Count(c => c.Estado == "POR_VENCER"),
            cobranza.Count(c => c.Estado == "VENCIDA"),
            cobranza.Count(c => c.Estado == "SUSPENDIDA"),
            cobranza.Count(c => c.Estado is "SIN_PLAN" or "SIN_VENCIMIENTO"),
            altas, bajas,
            uso.Values.Sum(u => u.UsuariosOperativos + (u.TieneAdmin ? 1 : 0)),
            uso.Values.Sum(u => u.Sucursales),
            serie, porPlan,
            cobranza.Where(c => c.Estado == "POR_VENCER").Select(c => Alerta(c,
                c.DiasParaVencer == 0 ? "Vence hoy." : $"Vence en {c.DiasParaVencer} día(s).")).ToList(),
            cobranza.Where(c => c.Estado == "VENCIDA").Select(c => Alerta(c,
                $"Vencida hace {-c.DiasParaVencer} día(s): no puede entrar al sistema.")).ToList(),
            excedidas);
    }
}
