using Microsoft.EntityFrameworkCore;
using ValexaFood.Application.Common;
using ValexaFood.Domain.Entities;
using ValexaFood.Infrastructure.Context;

namespace ValexaFood.Application.Services;

public record HallazgoDto(string Area, string Severidad, string Titulo, string Detalle, string QueHacer);
public record SaludEmpresaDto(int TenantId, string Empresa, string? Plan, bool Activo, string EstadoCobranza,
    string NivelUso, string NivelDatos, string NivelGeneral, DateTime? UltimoAcceso, int? DiasSinAcceso,
    int Accesos7Dias, int Fallidos24h, int UsuariosActivos, bool CelularPeru, string? Telefono, List<HallazgoDto> Hallazgos);

// Semáforo por empresa, calculado en vivo y SOLO LECTURA. Detecta abandono y
// datos incompletos antes de perder al cliente. Del negocio del restaurante
// solo se miran conteos, nunca montos. Cuando existan pedidos se suman aquí
// las señales de venta (sin ventas en N días, caída contra el mes pasado).
public class SaludService
{
    private const string Critico = "CRITICO", Atencion = "ATENCION", Info = "INFO", Ok = "OK";
    private readonly ValexaFoodContext _db;
    private readonly LimitePlan _limite;

    public SaludService(ValexaFoodContext db, LimitePlan limite)
    {
        _db = db;
        _limite = limite;
    }

    public async Task<List<SaludEmpresaDto>> ListarAsync(int? soloTenant = null)
    {
        var ahora = DateTime.UtcNow;
        var hoy = DiaLima.Hoy();
        var porVencer = await _db.ConfiguracionSaas.Select(c => (int?)c.DiasPorVencer).FirstOrDefaultAsync() ?? 7;

        var tenants = await _db.Tenants.AsNoTracking().Include(t => t.Plan)
            .Where(t => soloTenant == null || t.TenantId == soloTenant).ToListAsync();
        var ids = tenants.Select(t => t.TenantId).ToList();

        var hace7 = ahora.AddDays(-7);
        var hace24 = ahora.AddHours(-24);
        var accesos = await _db.AccesosLog.AsNoTracking().Where(a => a.TenantId != null && ids.Contains(a.TenantId!.Value))
            .GroupBy(a => a.TenantId!.Value)
            .Select(g => new
            {
                TenantId = g.Key,
                Ultimo = g.Where(a => a.Exito).Max(a => (DateTime?)a.Fecha),
                Ok7 = g.Count(a => a.Exito && a.Fecha >= hace7),
                Fallidos24 = g.Count(a => !a.Exito && a.Fecha >= hace24)
            }).ToListAsync();

        var usuarios = await _db.Usuarios.IgnoreQueryFilters().AsNoTracking()
            .Where(u => u.TenantId != null && ids.Contains(u.TenantId!.Value))
            .Select(u => new { u.TenantId, u.RolId, u.Activo, u.DebeCambiarPassword, u.BloqueadoHasta, u.Nombre, u.UltimoLogin })
            .ToListAsync();
        var uso = await _limite.UsoAsync(ids);
        var roles = await _limite.NombresRolAsync();
        var cupos = await _db.PlanRolCupos.AsNoTracking().ToListAsync();
        var funciones = await _db.PlanFunciones.AsNoTracking().ToListAsync();

        var resultado = new List<SaludEmpresaDto>();
        foreach (var t in tenants)
        {
            var h = new List<HallazgoDto>();
            void Uso(string sev, string titulo, string detalle, string hacer) => h.Add(new("USO", sev, titulo, detalle, hacer));
            void Datos(string sev, string titulo, string detalle, string hacer) => h.Add(new("DATOS", sev, titulo, detalle, hacer));

            int? dias = t.PlanFechaVencimiento.HasValue ? t.PlanFechaVencimiento.Value.DayNumber - hoy.DayNumber : null;
            var estado = SuscripcionService.Estado(t, dias, porVencer);
            var a = accesos.FirstOrDefault(x => x.TenantId == t.TenantId);
            var suyos = usuarios.Where(u => u.TenantId == t.TenantId).ToList();
            var u = uso.GetValueOrDefault(t.TenantId) ?? new UsoEmpresa(t.TenantId, 0, [], false);
            int? diasSinAcceso = a?.Ultimo == null ? null : (int)(ahora - a.Ultimo.Value).TotalDays;
            var diasCreada = (int)(ahora - t.CreatedAt).TotalDays;

            // ── Servicio y cobranza ──
            switch (estado)
            {
                case "SUSPENDIDA":
                    Uso(Info, "Empresa suspendida", t.MotivoSuspension ?? "Sin motivo registrado.", "Reactívala cuando regularice.");
                    break;
                case "SIN_PLAN":
                    Uso(Critico, "Sin plan asignado", "No puede ingresar al sistema.", "Asígnale un plan y registra el cobro.");
                    break;
                case "VENCIDA":
                    Uso(Critico, "Plan vencido", $"Venció hace {-dias} día(s): su equipo no puede entrar.", "Registra el pago o recuérdale por WhatsApp.");
                    break;
                case "POR_VENCER":
                    Uso(Atencion, "Plan por vencer", dias == 0 ? "Vence hoy." : $"Vence en {dias} día(s).", "Envíale el recordatorio de pago.");
                    break;
            }

            // ── Uso del sistema (no se alerta abandono de una suspendida) ──
            if (t.Activo)
            {
                if (a?.Ultimo == null)
                {
                    if (diasCreada >= 3)
                        Uso(Critico, "Nunca ingresó", $"Se creó hace {diasCreada} día(s) y nadie ha entrado todavía.", "Llama al Administrador y acompaña su primer ingreso.");
                    else
                        Uso(Info, "Aún sin primer ingreso", "La empresa es nueva.", "Verifica que el Administrador recibió su acceso.");
                }
                else if (diasSinAcceso >= 7)
                    Uso(Critico, "Sin actividad", $"Nadie entra hace {diasSinAcceso} días.", "Riesgo de abandono: contacta al cliente.");
                else if (diasSinAcceso >= 3)
                    Uso(Atencion, "Poca actividad", $"Nadie entra hace {diasSinAcceso} días.", "Pregunta si necesita ayuda.");

                if (u.UsuariosOperativos == 0 && diasCreada >= 3)
                    Uso(Atencion, "Solo el Administrador", "Todavía no registra a su equipo (cajeros, meseros, cocina).", "Ayúdalo a crear sus usuarios.");

                var admin = suyos.FirstOrDefault(x => x.RolId == RolIds.Administrador && x.Activo);
                if (admin == null)
                    Uso(Critico, "Sin Administrador activo", "Nadie puede gestionar la empresa.", "Crea o reactiva al Administrador desde la ficha.");
                else if (admin.DebeCambiarPassword && admin.UltimoLogin == null)
                    Uso(Info, "Administrador sin estrenar", $"{admin.Nombre} aún no usa su contraseña temporal.", "Confirma que la recibió.");
            }

            if (a?.Fallidos24 >= 10)
                Uso(Atencion, "Muchos intentos fallidos", $"{a.Fallidos24} ingresos fallidos en 24 h.", "Puede ser una clave olvidada o un intento de acceso indebido.");
            var bloqueados = suyos.Count(x => x.BloqueadoHasta > ahora);
            if (bloqueados > 0)
                Uso(Info, "Usuarios bloqueados", $"{bloqueados} usuario(s) bloqueado(s) por intentos fallidos.", "Desbloquéalos desde la ficha si lo piden.");

            // ── Plan: excesos y oportunidad de upgrade ──
            if (t.Plan != null && t.Activo)
            {
                var probs = LimitePlan.Problemas(u, t.Plan.MaxSucursales, t.Plan.MaxUsuarios,
                    cupos.Where(c => c.PlanId == t.PlanId).Select(c => (c.RolId, c.Maximo, c.PorSucursal)), roles,
                    t.Plan.PrecioSucursalAdicional);
                if (probs.Count > 0)
                    Uso(Atencion, "Excede su plan", string.Join("; ", probs) + ".", "Ofrécele un plan mayor.");
                else if (t.Plan.MaxUsuarios > 0 && u.UsuariosOperativos >= t.Plan.MaxUsuarios)
                    Uso(Info, "Cupo de usuarios lleno", $"Usa {u.UsuariosOperativos} de {t.Plan.MaxUsuarios}.", "Oportunidad de subir de plan.");
                if (t.Plan.MaxSucursales > 0 && u.Sucursales >= t.Plan.MaxSucursales && t.Plan.MaxSucursales > 1)
                    Uso(Info, "Cupo de sucursales lleno", $"Usa {u.Sucursales} de {t.Plan.MaxSucursales}.", "Oportunidad de subir de plan.");
            }

            // ── Datos de la empresa ──
            var tieneFe = funciones.Any(f => f.PlanId == t.PlanId && f.FuncionCodigo == "FACTURACION_SUNAT");
            if (t.Ruc == null)
                Datos(tieneFe ? Critico : Atencion, "Sin RUC",
                    tieneFe ? "Su plan incluye facturación SUNAT y no tiene RUC registrado." : "No tiene RUC registrado.",
                    "Pídele el RUC y regístralo en la ficha.");
            else if (!Documento.RucValido(t.Ruc))
                Datos(Critico, "RUC inválido", $"El RUC {t.Ruc} no pasa el dígito verificador.", "Corrígelo en la ficha.");
            if (t.RazonSocial == null && tieneFe)
                Datos(Atencion, "Sin razón social", "La necesita para emitir facturas.", "Complétala en la ficha.");
            if (!Documento.EsCelularPeru(t.Telefono))
                Datos(Info, "Sin celular", "No se le puede escribir por WhatsApp.", "Registra un celular de contacto.");
            if (t.Email == null)
                Datos(Info, "Sin correo", "No tiene correo de contacto.", "Regístralo en la ficha.");

            var nivelUso = Nivel(h.Where(x => x.Area == "USO"));
            var nivelDatos = Nivel(h.Where(x => x.Area == "DATOS"));
            resultado.Add(new SaludEmpresaDto(t.TenantId, t.NombreComercial, t.Plan?.Nombre, t.Activo, estado,
                nivelUso, nivelDatos, Nivel(h), a?.Ultimo, diasSinAcceso, a?.Ok7 ?? 0, a?.Fallidos24 ?? 0,
                suyos.Count(x => x.Activo), Documento.EsCelularPeru(t.Telefono), t.Telefono,
                h.OrderBy(x => Orden(x.Severidad)).ToList()));
        }

        return resultado.OrderBy(r => Orden(r.NivelGeneral)).ThenBy(r => r.Empresa).ToList();
    }

    private static string Nivel(IEnumerable<HallazgoDto> h)
    {
        var lista = h.ToList();
        if (lista.Any(x => x.Severidad == Critico)) return Critico;
        if (lista.Any(x => x.Severidad == Atencion)) return Atencion;
        if (lista.Any(x => x.Severidad == Info)) return Info;
        return Ok;
    }

    private static int Orden(string s) => s switch { Critico => 0, Atencion => 1, Info => 2, _ => 3 };
}
