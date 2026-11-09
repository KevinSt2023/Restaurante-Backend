using Microsoft.EntityFrameworkCore;
using ValexaFood.Application.Common;
using ValexaFood.Domain.Entities;
using ValexaFood.Infrastructure.Context;
using ValexaFood.Infrastructure.Sesion;

namespace ValexaFood.Application.Services;

public record CajaGuardarDto(string? Nombre);
public record CajaEstadoDto(bool Activa);
public record CajaAbrirDto(decimal FondoInicial);
public record CajaCerrarDto(decimal EfectivoContado, string? Nota);
public record CajaMovimientoGuardarDto(string? Tipo, decimal Monto, string? Motivo, string? ClaveIdempotencia);
public record SesionCajaResumenDto(long SesionCajaId, string Estado, decimal FondoInicial, DateTime AbiertaEn,
    int AbiertoPor, string AbiertoPorNombre, DateTime? CerradaEn, decimal? EfectivoEsperado,
    decimal? EfectivoContado, decimal? Diferencia, string? NotaCierre);
public record CajaListaDto(int CajaId, int SucursalId, string Nombre, bool Activa, SesionCajaResumenDto? SesionAbierta);
public record CajaMovimientoDto(long CajaMovimientoId, long SesionCajaId, string Tipo, decimal Monto,
    string Motivo, int RegistradoPor, DateTime RegistradoEn);
public record CajaCierreMedioDto(string Medio, decimal Monto, int Operaciones);
public record CajaCierreResultadoDto(SesionCajaResumenDto Sesion, decimal Ingresos, decimal Egresos,
    List<CajaCierreMedioDto> VentasPorMedio);
/// <summary>
/// El cuadre del turno, pieza por pieza, para que el cajero pueda seguirlo con el dedo:
/// fondo + ventas en efectivo + ingresos − egresos − devoluciones en efectivo = esperado.
/// </summary>
public record CajaArqueoDto(SesionCajaResumenDto Sesion, decimal FondoInicial, decimal VentasEfectivo,
    decimal DevolucionesEfectivo, decimal IngresosManuales, decimal EgresosManuales, decimal EfectivoEsperado,
    List<CajaCierreMedioDto> VentasPorMedio, List<CajaMovimientoDto> Movimientos);
public record CajaCobroDisponibleDto(long SesionCajaId, int CajaId, string Caja, DateTime AbiertaEn);
public record AjusteVentaLimitesDto(decimal DescuentoMaximoPct, decimal DescuentoMaximoMonto);

public class CajaService
{
    private const decimal MaximoMonto = 9_999_999_999.99m;
    private readonly ValexaFoodContext _db;
    private readonly ISesionActual _sesion;
    private readonly BitacoraService _bitacora;

    public CajaService(ValexaFoodContext db, ISesionActual sesion, BitacoraService bitacora)
    {
        _db = db;
        _sesion = sesion;
        _bitacora = bitacora;
    }

    private int Tenant => _sesion.TenantId ?? throw new ReglaNegocioException("La sesión no pertenece a una empresa.", 403);
    private int Sucursal => _sesion.SucursalId ?? throw new ReglaNegocioException("Selecciona una sucursal antes de operar.", 428);
    private int Usuario => _sesion.UsuarioId ?? throw new ReglaNegocioException("La sesión no identifica al usuario.", 401);

    public async Task<List<CajaListaDto>> ListarAsync()
    {
        var cajas = await _db.Cajas.AsNoTracking().Where(c => c.SucursalId == Sucursal)
            .OrderBy(c => c.Nombre).ToListAsync();
        var abiertas = await _db.SesionesCaja.AsNoTracking()
            .Where(s => s.SucursalId == Sucursal && s.Estado == "ABIERTA")
            .Join(_db.Usuarios.AsNoTracking(), s => s.AbiertoPor, u => u.UsuarioId,
                (s, u) => new { Sesion = s, Usuario = u.Nombre })
            .ToListAsync();
        return cajas.Select(c =>
        {
            var abierta = abiertas.FirstOrDefault(x => x.Sesion.CajaId == c.CajaId);
            return new CajaListaDto(c.CajaId, c.SucursalId, c.Nombre, c.Activa,
                abierta == null ? null : Resumen(abierta.Sesion, abierta.Usuario));
        }).ToList();
    }

    // El orden va sobre la entidad, ANTES de construir el DTO: EF no sabe traducir
    // un OrderBy sobre las propiedades de un record creado por constructor.
    public async Task<List<CajaCobroDisponibleDto>> SesionesAbiertasCobroAsync() => await _db.SesionesCaja.AsNoTracking()
        .Where(s => s.SucursalId == Sucursal && s.Estado == "ABIERTA")
        .Join(_db.Cajas.AsNoTracking(), s => s.CajaId, c => c.CajaId, (s, c) => new { s, c })
        .OrderByDescending(x => x.s.AbiertaEn)
        .Select(x => new CajaCobroDisponibleDto(x.s.SesionCajaId, x.c.CajaId, x.c.Nombre, x.s.AbiertaEn))
        .ToListAsync();

    public async Task<AjusteVentaLimitesDto> LimitesAjusteAsync()
    {
        var c = await _db.AjustesVentaConfiguracion.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == Tenant);
        return new(c?.DescuentoMaximoPct ?? 20m, c?.DescuentoMaximoMonto ?? 200m);
    }

    public async Task<AjusteVentaLimitesDto> GuardarLimitesAjusteAsync(AjusteVentaLimitesDto dto)
    {
        if (dto.DescuentoMaximoPct is < 0 or > 100 || decimal.Round(dto.DescuentoMaximoPct, 2) != dto.DescuentoMaximoPct
            || dto.DescuentoMaximoMonto is < 0 or > MaximoMonto || decimal.Round(dto.DescuentoMaximoMonto, 2) != dto.DescuentoMaximoMonto)
            throw new ReglaNegocioException("Configura un porcentaje entre 0 y 100 y un monto máximo válido con dos decimales.");
        var c = await _db.AjustesVentaConfiguracion.FirstOrDefaultAsync(x => x.TenantId == Tenant);
        if (c == null) { c = new AjusteVentaConfiguracion { TenantId = Tenant }; _db.AjustesVentaConfiguracion.Add(c); }
        c.DescuentoMaximoPct = dto.DescuentoMaximoPct; c.DescuentoMaximoMonto = dto.DescuentoMaximoMonto;
        c.ActualizadoPor = Usuario; c.ActualizadoEn = DateTime.UtcNow;
        await _bitacora.AgregarAsync(Tenant, "AJUSTE_LIMITES_CONFIGURADOS", "AjusteVentaConfiguracion", Tenant,
            $"Límite de descuento {dto.DescuentoMaximoPct:0.##}% / S/ {dto.DescuentoMaximoMonto:0.00}.");
        await _db.SaveChangesAsync();
        return new(c.DescuentoMaximoPct, c.DescuentoMaximoMonto);
    }

    public async Task<CajaListaDto> CrearAsync(CajaGuardarDto dto)
    {
        var nombre = Texto.Requerido(dto.Nombre, "Nombre de caja", 80, 2);
        if (await _db.Cajas.AnyAsync(c => c.SucursalId == Sucursal && c.Nombre.ToLower() == nombre.ToLower()))
            throw new ReglaNegocioException("Ya existe una caja con ese nombre en esta sucursal.");
        var caja = new Caja
        {
            TenantId = Tenant, SucursalId = Sucursal, Nombre = nombre, Activa = true, CreatedAt = DateTime.UtcNow
        };
        _db.Cajas.Add(caja);
        await _bitacora.AgregarAsync(Tenant, "CAJA_CREADA", "Caja", null, $"Caja {nombre} creada en sucursal {Sucursal}.");
        await _db.SaveChangesAsync();
        return new CajaListaDto(caja.CajaId, caja.SucursalId, caja.Nombre, caja.Activa, null);
    }

    public async Task<List<CajaListaDto>> EstadoAsync(int cajaId, CajaEstadoDto dto)
    {
        var caja = await _db.Cajas.FirstOrDefaultAsync(c => c.CajaId == cajaId && c.SucursalId == Sucursal)
            ?? throw ReglaNegocioException.NoEncontrado("Caja");
        if (caja.Activa == dto.Activa)
            throw new ReglaNegocioException(dto.Activa ? "La caja ya está activa." : "La caja ya está desactivada.");
        if (!dto.Activa)
        {
            if (await _db.SesionesCaja.AnyAsync(s => s.CajaId == cajaId && s.Estado == "ABIERTA"))
                throw new ReglaNegocioException("Cierra el turno de esta caja antes de desactivarla.");
            if (!await _db.Cajas.AnyAsync(c => c.SucursalId == Sucursal && c.Activa && c.CajaId != cajaId))
                throw new ReglaNegocioException("La sucursal debe conservar al menos una caja activa.");
        }
        caja.Activa = dto.Activa;
        await _bitacora.AgregarAsync(Tenant, dto.Activa ? "CAJA_ACTIVADA" : "CAJA_DESACTIVADA", "Caja", cajaId,
            $"Caja {caja.Nombre} {(dto.Activa ? "activada" : "desactivada")} en sucursal {Sucursal}.");
        await _db.SaveChangesAsync();
        return await ListarAsync();
    }

    public async Task<SesionCajaResumenDto> AbrirAsync(int cajaId, CajaAbrirDto dto)
    {
        ValidarMonto(dto.FondoInicial, permitirCero: true, "fondo inicial");
        var caja = await _db.Cajas.FirstOrDefaultAsync(c => c.CajaId == cajaId && c.SucursalId == Sucursal && c.Activa)
            ?? throw ReglaNegocioException.NoEncontrado("Caja activa");
        if (await _db.SesionesCaja.AnyAsync(s => s.CajaId == caja.CajaId && s.Estado == "ABIERTA"))
            throw new ReglaNegocioException("Esta caja ya tiene un turno abierto.");

        var sesion = new SesionCaja
        {
            TenantId = Tenant, SucursalId = Sucursal, CajaId = caja.CajaId, Estado = "ABIERTA",
            FondoInicial = decimal.Round(dto.FondoInicial, 2, MidpointRounding.AwayFromZero),
            AbiertoPor = Usuario, AbiertaEn = DateTime.UtcNow
        };
        _db.SesionesCaja.Add(sesion);
        await _bitacora.AgregarAsync(Tenant, "CAJA_TURNO_ABIERTO", "SesionCaja", null,
            $"Turno abierto en {caja.Nombre}, sucursal {Sucursal}; fondo inicial S/ {sesion.FondoInicial:0.00}.");
        await _db.SaveChangesAsync();
        return Resumen(sesion, await NombreUsuarioAsync(Usuario));
    }

    public async Task<CajaMovimientoDto> MovimientoAsync(long sesionCajaId, CajaMovimientoGuardarDto dto)
    {
        var tipo = (dto.Tipo ?? "").Trim().ToUpperInvariant();
        if (tipo is not ("INGRESO" or "EGRESO"))
            throw new ReglaNegocioException("El tipo de movimiento debe ser INGRESO o EGRESO.");
        ValidarMonto(dto.Monto, permitirCero: false, "monto");
        var motivo = Texto.Requerido(dto.Motivo, "Motivo", 200, 3);
        if (!Guid.TryParse(dto.ClaveIdempotencia, out var clave))
            throw new ReglaNegocioException("Envía una clave de idempotencia válida para este movimiento.");
        var claveTexto = clave.ToString("N");

        return await EnTransaccionAsync(async () =>
        {
            var sesion = await BloquearSesionAbiertaAsync(sesionCajaId)
                ?? throw ReglaNegocioException.NoEncontrado("Turno abierto");

            var previo = await _db.CajaMovimientos.AsNoTracking().FirstOrDefaultAsync(m =>
                m.SucursalId == Sucursal && m.ClaveIdempotencia == claveTexto);
            if (previo != null)
            {
                if (previo.SesionCajaId != sesionCajaId || previo.Tipo != tipo
                    || previo.Monto != decimal.Round(dto.Monto, 2) || previo.Motivo != motivo)
                    throw new ReglaNegocioException("La clave ya se usó con datos distintos o en otro turno.");
                return MovimientoDto(previo);
            }

            var monto = decimal.Round(dto.Monto, 2, MidpointRounding.AwayFromZero);
            if (tipo == "EGRESO")
            {
                var (ingresos, egresos) = await TotalesMovimientosAsync(sesionCajaId);
                if (egresos + monto > sesion.FondoInicial + ingresos)
                    throw new ReglaNegocioException("El egreso supera el efectivo disponible registrado en la caja.");
            }

            var movimiento = new CajaMovimiento
            {
                TenantId = Tenant, SucursalId = Sucursal, SesionCajaId = sesionCajaId, Tipo = tipo,
                Monto = monto, Motivo = motivo, ClaveIdempotencia = claveTexto,
                RegistradoPor = Usuario, RegistradoEn = DateTime.UtcNow
            };
            _db.CajaMovimientos.Add(movimiento);
            await _bitacora.AgregarAsync(Tenant, "CAJA_MOVIMIENTO", "CajaMovimiento", null,
                $"{tipo} S/ {monto:0.00} en sucursal {Sucursal}: {motivo}.");
            await _db.SaveChangesAsync();
            return MovimientoDto(movimiento);
        });
    }

    public async Task<CajaCierreResultadoDto> CerrarAsync(long sesionCajaId, CajaCerrarDto dto)
    {
        ValidarMonto(dto.EfectivoContado, permitirCero: true, "efectivo contado");
        var nota = Texto.Opcional(dto.Nota, "Nota de cierre", 500);
        return await EnTransaccionAsync(async () =>
        {
            var sesion = await BloquearSesionAbiertaAsync(sesionCajaId)
                ?? throw ReglaNegocioException.NoEncontrado("Turno abierto");
            var (ingresos, egresos) = await TotalesMovimientosAsync(sesionCajaId);
            var esperado = decimal.Round(sesion.FondoInicial + ingresos - egresos, 2, MidpointRounding.AwayFromZero);
            if (esperado < 0) throw new ReglaNegocioException("La caja no puede cerrar con efectivo esperado negativo.");

            sesion.Estado = "CERRADA";
            sesion.CerradoPor = Usuario;
            sesion.CerradaEn = DateTime.UtcNow;
            sesion.EfectivoEsperado = esperado;
            sesion.EfectivoContado = decimal.Round(dto.EfectivoContado, 2, MidpointRounding.AwayFromZero);
            sesion.Diferencia = sesion.EfectivoContado - esperado;
            sesion.NotaCierre = nota;
            await _bitacora.AgregarAsync(Tenant, "CAJA_TURNO_CERRADO", "SesionCaja", sesionCajaId,
                $"Cierre sucursal {Sucursal}; esperado S/ {esperado:0.00}, contado S/ {sesion.EfectivoContado:0.00}, diferencia S/ {sesion.Diferencia:0.00}.");
            await _db.SaveChangesAsync();
            // El mismo cálculo que ve el cajero en el arqueo en vivo.
            var porMedio = await VentasPorMedioAsync(sesionCajaId);
            return new CajaCierreResultadoDto(Resumen(sesion, await NombreUsuarioAsync(sesion.AbiertoPor)), ingresos, egresos, porMedio);
        });
    }

    public async Task<List<CajaMovimientoDto>> MovimientosAsync(long sesionCajaId)
    {
        if (!await _db.SesionesCaja.AnyAsync(s => s.SesionCajaId == sesionCajaId && s.SucursalId == Sucursal))
            throw ReglaNegocioException.NoEncontrado("Turno");
        return await _db.CajaMovimientos.AsNoTracking().Where(m => m.SesionCajaId == sesionCajaId)
            .OrderBy(m => m.RegistradoEn)
            .Select(m => new CajaMovimientoDto(m.CajaMovimientoId, m.SesionCajaId, m.Tipo, m.Monto,
                m.Motivo, m.RegistradoPor, m.RegistradoEn)).ToListAsync();
    }

    /// <summary>
    /// Las cuatro piezas del efectivo de un turno, por separado.
    /// Es el ÚNICO lugar donde se calculan: el arqueo en vivo y el cierre leen de aquí,
    /// así no pueden dar números distintos.
    /// </summary>
    private async Task<DesgloseEfectivo> DesgloseEfectivoAsync(long sesionCajaId)
    {
        var movimientos = await _db.CajaMovimientos.AsNoTracking().Where(m => m.SesionCajaId == sesionCajaId)
            .GroupBy(m => m.Tipo).Select(g => new { Tipo = g.Key, Total = g.Sum(x => x.Monto) }).ToListAsync();
        var ventasEfectivo = await _db.Pagos.AsNoTracking().Where(p => p.SesionCajaId == sesionCajaId && p.Medio == "EFECTIVO")
            .SumAsync(p => (decimal?)p.Monto) ?? 0m;
        var devolucionesEfectivo = await (from a in _db.AjustesVenta.AsNoTracking()
            join p in _db.Pagos.AsNoTracking() on a.PagoId equals p.PagoId
            where a.SesionCajaId == sesionCajaId && a.SucursalId == Sucursal && a.Tipo == "DEVOLUCION" && p.Medio == "EFECTIVO"
            select (decimal?)a.Monto).SumAsync() ?? 0m;
        return new DesgloseEfectivo(
            VentasEfectivo: ventasEfectivo,
            DevolucionesEfectivo: devolucionesEfectivo,
            IngresosManuales: movimientos.FirstOrDefault(x => x.Tipo == "INGRESO")?.Total ?? 0m,
            EgresosManuales: movimientos.FirstOrDefault(x => x.Tipo == "EGRESO")?.Total ?? 0m);
    }

    private sealed record DesgloseEfectivo(decimal VentasEfectivo, decimal DevolucionesEfectivo,
        decimal IngresosManuales, decimal EgresosManuales)
    {
        public decimal Ingresos => IngresosManuales + VentasEfectivo;
        public decimal Egresos => EgresosManuales + DevolucionesEfectivo;
    }

    private async Task<(decimal Ingresos, decimal Egresos)> TotalesMovimientosAsync(long sesionCajaId)
    {
        var d = await DesgloseEfectivoAsync(sesionCajaId);
        return (d.Ingresos, d.Egresos);
    }

    /// <summary>Lo vendido por medio de pago en el turno; las devoluciones restan.</summary>
    private async Task<List<CajaCierreMedioDto>> VentasPorMedioAsync(long sesionCajaId)
    {
        var pagos = await _db.Pagos.AsNoTracking().Where(p => p.SesionCajaId == sesionCajaId && p.SucursalId == Sucursal)
            .Select(p => new { p.Medio, p.Monto }).ToListAsync();
        var devoluciones = await (from a in _db.AjustesVenta.AsNoTracking()
            join p in _db.Pagos.AsNoTracking() on a.PagoId equals p.PagoId
            where a.SesionCajaId == sesionCajaId && a.SucursalId == Sucursal && a.Tipo == "DEVOLUCION"
            select new { p.Medio, a.Monto }).ToListAsync();
        return pagos.Select(p => (p.Medio, Monto: p.Monto, Operaciones: 1))
            .Concat(devoluciones.Select(d => (d.Medio, Monto: -d.Monto, Operaciones: 1)))
            .GroupBy(p => p.Medio)
            .Select(g => new CajaCierreMedioDto(g.Key, g.Sum(p => p.Monto), g.Sum(p => p.Operaciones)))
            .OrderBy(x => x.Medio).ToList();
    }

    /// <summary>
    /// Arqueo de un turno ABIERTO, sin cerrarlo: el cajero ve qué debería haber en la caja
    /// antes de contar, no después. Solo lee.
    /// </summary>
    public async Task<CajaArqueoDto> ArqueoAsync(long sesionCajaId)
    {
        var sesion = await _db.SesionesCaja.AsNoTracking()
            .FirstOrDefaultAsync(s => s.SesionCajaId == sesionCajaId && s.SucursalId == Sucursal)
            ?? throw ReglaNegocioException.NoEncontrado("Turno");
        var d = await DesgloseEfectivoAsync(sesionCajaId);
        var esperado = decimal.Round(sesion.FondoInicial + d.Ingresos - d.Egresos, 2, MidpointRounding.AwayFromZero);
        return new CajaArqueoDto(
            Resumen(sesion, await NombreUsuarioAsync(sesion.AbiertoPor)),
            sesion.FondoInicial, d.VentasEfectivo, d.DevolucionesEfectivo,
            d.IngresosManuales, d.EgresosManuales, esperado,
            await VentasPorMedioAsync(sesionCajaId),
            await MovimientosAsync(sesionCajaId));
    }

    private async Task<SesionCaja?> BloquearSesionAbiertaAsync(long sesionCajaId)
    {
        var filas = await _db.SesionesCaja.FromSqlInterpolated($"""
            SELECT * FROM sesion_caja
             WHERE tenant_id = {Tenant} AND sucursal_id = {Sucursal}
               AND sesion_caja_id = {sesionCajaId} AND estado = 'ABIERTA'
             FOR UPDATE
            """).IgnoreQueryFilters().ToListAsync();
        return filas.FirstOrDefault();
    }

    private async Task<T> EnTransaccionAsync<T>(Func<Task<T>> accion)
    {
        T? resultado = default;
        var estrategia = _db.Database.CreateExecutionStrategy();
        await estrategia.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync();
            resultado = await accion();
            await tx.CommitAsync();
        });
        return resultado!;
    }

    private async Task<string> NombreUsuarioAsync(int usuarioId) =>
        await _db.Usuarios.AsNoTracking().Where(u => u.UsuarioId == usuarioId).Select(u => u.Nombre).FirstAsync();

    private static SesionCajaResumenDto Resumen(SesionCaja s, string nombre) => new(s.SesionCajaId,
        s.Estado, s.FondoInicial, s.AbiertaEn, s.AbiertoPor, nombre, s.CerradaEn,
        s.EfectivoEsperado, s.EfectivoContado, s.Diferencia, s.NotaCierre);

    private static CajaMovimientoDto MovimientoDto(CajaMovimiento m) => new(m.CajaMovimientoId,
        m.SesionCajaId, m.Tipo, m.Monto, m.Motivo, m.RegistradoPor, m.RegistradoEn);

    private static void ValidarMonto(decimal monto, bool permitirCero, string campo)
    {
        if (monto < 0 || (!permitirCero && monto == 0) || monto > MaximoMonto || decimal.Round(monto, 2) != monto)
            throw new ReglaNegocioException($"El {campo} debe tener {(permitirCero ? "un valor de 0" : "un valor mayor a 0")} a {MaximoMonto:0.00} con máximo dos decimales.");
    }
}
