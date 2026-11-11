using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using QRCoder;
using ValexaFood.Application.Common;
using ValexaFood.Domain.Entities;
using ValexaFood.Infrastructure.Context;

namespace ValexaFood.Application.Services;

// ─────────────────────────────────────────────────────────────────────────────
// La carta que ve el cliente desde el QR de la mesa.
//
// Es el único servicio del sistema que atiende SIN sesión, así que se escribe
// al revés que los demás: nada viene del contexto, todo se deriva del código de
// la URL, y cualquier duda termina en 404. Nunca en "existe pero está apagada":
// eso ya confirmaría que el restaurante existe.
//
// Solo mirar. No se piden platos desde aquí.
// ─────────────────────────────────────────────────────────────────────────────
public record CartaPublicaVarianteDto(string Nombre, decimal Precio);
public record CartaPublicaPlatoDto(string Nombre, string? Descripcion, decimal Precio,
    string? Imagen, string[]? Etiquetas, bool Agotado, List<CartaPublicaVarianteDto> Variantes);
public record CartaPublicaCategoriaDto(string Nombre, string Color, List<CartaPublicaPlatoDto> Platos);
public record CartaPublicaDto(string Negocio, string Sucursal, string? Direccion, string? Telefono,
    List<CartaPublicaCategoriaDto> Categorias);

/// <summary>Lo que el dueño ve en su pantalla para publicar e imprimir el QR.</summary>
public record CartaQrDto(bool Activa, string? Codigo, string? Url, string? Svg);
public record CartaQrGuardarDto(bool Activa, bool Regenerar = false);

public class CartaPublicaService
{
    public const string Funcion = "CARTA_QR";
    private static readonly Regex Codigo = new("^[A-Za-z0-9]{12}$", RegexOptions.Compiled);
    // Sin I, l, O ni 0: el dueño va a dictar este código por teléfono alguna vez.
    private const string Alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz123456789";

    private readonly ValexaFoodContext _db;
    private readonly IConfiguration _config;
    public CartaPublicaService(ValexaFoodContext db, IConfiguration config) => (_db, _config) = (db, config);

    public static string NuevoCodigo()
    {
        var chars = new char[12];
        for (var i = 0; i < chars.Length; i++) chars[i] = Alfabeto[RandomNumberGenerator.GetInt32(Alfabeto.Length)];
        return new string(chars);
    }

    /// <summary>Dónde vive la página pública. El QR apunta aquí, no a la API.</summary>
    public string UrlDe(string codigo) =>
        $"{(_config["Carta:UrlPublica"] ?? "https://valexafood.pe").TrimEnd('/')}/m/{codigo}";

    /// <summary>El QR como SVG: se imprime a cualquier tamaño sin pixelarse.</summary>
    public static string SvgDe(string url)
    {
        using var generador = new QRCodeGenerator();
        // Nivel Q: el QR de una mesa termina manchado de ají. Aguanta ~25 % de daño.
        using var datos = generador.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        return new SvgQRCode(datos).GetGraphic(8);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PÚBLICO — sin sesión
    // ═════════════════════════════════════════════════════════════════════════
    public async Task<CartaPublicaDto> VerAsync(string? codigo)
    {
        var limpio = (codigo ?? "").Trim();
        // Todo lo que no cuadra responde lo mismo, y en el mismo orden: quien
        // prueba códigos al azar no aprende nada de la respuesta.
        if (!Codigo.IsMatch(limpio)) throw NoHayCarta();

        var sucursal = await _db.Sucursales.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(s => s.CartaCodigo == limpio && s.CartaPublica && s.Activo) ?? throw NoHayCarta();

        var tenant = await _db.Tenants.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(t => t.TenantId == sucursal.TenantId && t.Activo) ?? throw NoHayCarta();
        // El plan manda también aquí: una empresa vencida o sin la función no
        // publica carta. Si no, el QR impreso sobreviviría a la suscripción.
        if (tenant.PlanId == null || PlanVigencia.EstaVencido(tenant.PlanFechaVencimiento)) throw NoHayCarta();
        if (!await _db.PlanFunciones.AsNoTracking()
                .AnyAsync(f => f.PlanId == tenant.PlanId && f.FuncionCodigo == Funcion)) throw NoHayCarta();

        var hoy = DiaLima.Hoy();
        var sid = sucursal.SucursalId;
        var productos = await _db.Productos.IgnoreQueryFilters().AsNoTracking()
            .Include(p => p.Categoria).Include(p => p.Variantes).Include(p => p.Sucursales)
            .AsSplitQuery()
            .Where(p => p.TenantId == tenant.TenantId && p.Activo && p.Categoria!.Activo
                        && p.Sucursales.Any(s => s.SucursalId == sid))
            .ToListAsync();

        CartaPublicaPlatoDto Mapear(Producto p)
        {
            var ps = p.Sucursales.First(s => s.SucursalId == sid);
            return new CartaPublicaPlatoDto(p.Nombre, p.Descripcion, ps.Precio ?? p.Precio, p.Imagen,
                p.Etiquetas, ps.AgotadoFecha == hoy,
                p.Variantes.Where(v => v.Activo).OrderBy(v => v.Orden)
                    .Select(v => new CartaPublicaVarianteDto(v.Nombre, v.Precio)).ToList());
        }

        var categorias = productos.GroupBy(p => p.CategoriaId)
            .Select(g => (Cat: g.First().Categoria!, Items: g))
            .OrderBy(g => g.Cat.Orden).ThenBy(g => g.Cat.Nombre)
            .Select(g => new CartaPublicaCategoriaDto(g.Cat.Nombre, g.Cat.Color,
                g.Items.OrderBy(p => p.Orden).ThenBy(p => p.Nombre).Select(Mapear).ToList()))
            .Where(c => c.Platos.Count > 0).ToList();

        return new CartaPublicaDto(tenant.NombreComercial, sucursal.Nombre,
            sucursal.Direccion, sucursal.Telefono, categorias);
    }

    private static ReglaNegocioException NoHayCarta() =>
        new("Esta carta no está disponible.", 404);
}
