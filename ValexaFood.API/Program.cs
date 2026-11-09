using System.Text;
using System.Text.Json;
using System.Security.Cryptography.X509Certificates;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ValexaFood.Application.Common;
using ValexaFood.Application.Services;
using ValexaFood.API;
using ValexaFood.Infrastructure.Context;
using ValexaFood.Infrastructure.Sesion;

var builder = WebApplication.CreateBuilder(args);

// ─── Configuración: variables de entorno primero (producción), luego appsettings ───
var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Falta la cadena de conexión.");
var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY") ?? builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Falta Jwt:Key.");
if (jwtKey.Length < 32) throw new InvalidOperationException("Jwt:Key debe tener al menos 32 caracteres.");
var configuredKeyPath = builder.Configuration["DataProtection:KeysPath"];
if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(configuredKeyPath))
    throw new InvalidOperationException("Configura DataProtection:KeysPath con la ruta absoluta del volumen persistente para proteger los secretos guardados.");
if (!builder.Environment.IsDevelopment() && !Path.IsPathFullyQualified(configuredKeyPath!))
    throw new InvalidOperationException("DataProtection:KeysPath debe ser una ruta absoluta del volumen persistente.");
X509Certificate2? dataProtectionCertificate = null;
X509Certificate2[] dataProtectionDecryptionCertificates = [];
if (!builder.Environment.IsDevelopment())
{
    var certificatePath = builder.Configuration["DataProtection:CertificatePath"];
    var certificatePassword = Environment.GetEnvironmentVariable("DATA_PROTECTION_CERT_PASSWORD");
    var activeThumbprint = builder.Configuration["DataProtection:ActiveCertificateThumbprint"]?.Replace(" ", "", StringComparison.Ordinal);
    if (string.IsNullOrWhiteSpace(certificatePath) || !Path.IsPathFullyQualified(certificatePath))
        throw new InvalidOperationException("Configura DataProtection:CertificatePath con la ruta absoluta del certificado PFX para cifrar el anillo de claves.");
    if (string.IsNullOrEmpty(certificatePassword))
        throw new InvalidOperationException("Falta el secreto de entorno DATA_PROTECTION_CERT_PASSWORD para abrir el certificado PFX.");
    if (string.IsNullOrWhiteSpace(activeThumbprint))
        throw new InvalidOperationException("Configura DataProtection:ActiveCertificateThumbprint para seleccionar el certificado activo del bundle PFX.");
    var certificateBundle = new X509Certificate2Collection();
    certificateBundle.Import(certificatePath, certificatePassword, X509KeyStorageFlags.EphemeralKeySet);
    var privateCertificates = certificateBundle.Cast<X509Certificate2>().Where(c => c.HasPrivateKey).ToArray();
    var certificate = privateCertificates.FirstOrDefault(c =>
        string.Equals(c.Thumbprint?.Replace(" ", "", StringComparison.Ordinal), activeThumbprint, StringComparison.OrdinalIgnoreCase));
    if (certificate is null)
    {
        foreach (var loaded in certificateBundle) loaded.Dispose();
        throw new InvalidOperationException("El thumbprint activo no corresponde a un certificado con clave privada del bundle PFX.");
    }
    var nowUtc = DateTime.UtcNow;
    if (!certificate.HasPrivateKey || certificate.NotBefore.ToUniversalTime() > nowUtc || certificate.NotAfter.ToUniversalTime() <= nowUtc)
    {
        foreach (var loaded in certificateBundle) loaded.Dispose();
        throw new InvalidOperationException("El certificado PFX debe incluir clave privada y estar vigente.");
    }
    dataProtectionCertificate = certificate;
    dataProtectionDecryptionCertificates = privateCertificates;
}

builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
var keyPath = configuredKeyPath ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys");
Directory.CreateDirectory(keyPath);
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("ValexaFood")
    .PersistKeysToFileSystem(new DirectoryInfo(keyPath));
if (dataProtectionCertificate is not null) dataProtection.ProtectKeysWithCertificate(dataProtectionCertificate);
if (dataProtectionDecryptionCertificates.Length > 0)
    dataProtection.UnprotectKeysWithAnyCertificate(dataProtectionDecryptionCertificates);
builder.Services.AddHttpClient("nubefact", client => client.Timeout = TimeSpan.FromSeconds(25));

builder.Services.AddDbContext<ValexaFoodContext>(o =>
{
    o.UseNpgsql(connectionString, n => { n.CommandTimeout(30); n.EnableRetryOnFailure(3); })
     .UseSnakeCaseNamingConvention();
    if (builder.Environment.IsDevelopment()) o.EnableDetailedErrors();
});

// ─── Servicios ───
builder.Services.AddScoped<ISesionActual, SesionActual>();
builder.Services.AddSingleton<JwtService>();
builder.Services.AddScoped<ValidadorSesion>();
builder.Services.AddScoped<CacheSesiones>();
builder.Services.AddScoped<PermisosEfectivos>();
builder.Services.AddScoped<BitacoraService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<LimitePlan>();
builder.Services.AddScoped<PlanService>();
builder.Services.AddScoped<SuscripcionService>();
builder.Services.AddScoped<EmpresaService>();
builder.Services.AddScoped<SaludService>();
builder.Services.AddScoped<CatalogoService>();
builder.Services.AddScoped<CartaService>();
builder.Services.AddScoped<ContextoSucursalService>();
builder.Services.AddScoped<CajaService>();
builder.Services.AddScoped<InventarioService>();
builder.Services.AddScoped<PedidoService>();
builder.Services.AddScoped<DeliveryService>();
builder.Services.AddScoped<ReporteVentasService>();
builder.Services.AddScoped<FacturacionService>();

// ─── Autenticación ───
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "ValexaFoodAPI",
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "ValexaFoodUsers",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.FromSeconds(30)
    };
    o.Events = new JwtBearerEvents
    {
        // Un token bien firmado no basta: usuario activo, sesión vigente, empresa al día.
        OnTokenValidated = async ctx =>
        {
            var validador = ctx.HttpContext.RequestServices.GetRequiredService<ValidadorSesion>();
            var r = await validador.ValidarAsync(ctx.Principal!);
            if (r.Valida) return;
            if (r.PlanVencido) ctx.HttpContext.Response.Headers.Append("X-Plan-Vencido", "true");
            ctx.HttpContext.Response.Headers.Append("X-Sesion-Motivo", Uri.EscapeDataString(r.Motivo ?? ""));
            ctx.Fail(r.Motivo ?? "Sesión inválida");
        }
    };
});
builder.Services.AddAuthorization();

// ─── Límites de peticiones ───
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.ContentType = "application/json";
        await ctx.HttpContext.Response.WriteAsync("""{"mensaje":"Demasiados intentos seguidos. Espera un minuto e intenta de nuevo."}""", ct);
    };
    // Login: 10 por minuto por IP (el bloqueo por usuario va aparte, en AuthService).
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "?",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
    .ConfigureApiBehaviorOptions(o =>
    {
        // Un JSON mal formado o con tipos incorrectos sale como 400 con mensaje legible.
        o.InvalidModelStateResponseFactory = ctx => new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(
            new { mensaje = "Los datos enviados no tienen el formato esperado. Revisa los campos numéricos y las fechas." });
    });

var origenes = builder.Configuration.GetSection("Cors:Origenes").Get<string[]>() ?? ["http://localhost:4400"];
builder.Services.AddCors(o => o.AddPolicy("web", p => p.WithOrigins(origenes)
    .AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("X-Plan-Vencido", "X-Sesion-Motivo")));

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
}

builder.WebHost.ConfigureKestrel(k =>
{
    k.Limits.MaxRequestBodySize = 5 * 1024 * 1024;
    k.AddServerHeader = false;
});

var app = builder.Build();

// ─── Errores: nada sale como 500 si es un dato del usuario ───
app.Use(async (ctx, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        if (ctx.Response.HasStarted)
        {
            ctx.RequestServices.GetRequiredService<ILogger<Program>>()
                .LogError(ex, "La respuesta ya había comenzado en {Ruta}", ctx.Request.Path);
            throw;
        }

        ctx.Response.Clear();
        var log = ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Errores");
        ctx.Response.ContentType = "application/json";

        if (ex is ReglaNegocioException rn)
        {
            ctx.Response.StatusCode = rn.Status;
            var cuerpo = new Dictionary<string, object?> { ["mensaje"] = rn.Message };
            if (rn.Datos != null)
                foreach (var p in rn.Datos.GetType().GetProperties())
                    cuerpo[JsonNamingPolicy.CamelCase.ConvertName(p.Name)] = p.GetValue(rn.Datos);
            await ctx.Response.WriteAsJsonAsync(cuerpo, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            return;
        }

        Npgsql.PostgresException? pg = null;
        for (var e = ex; e != null && pg == null; e = e.InnerException) pg = e as Npgsql.PostgresException;
        var mensaje = pg?.SqlState switch
        {
            "P0001" => pg.MessageText, // candados de la base (el plan manda, inmutables): mensaje ya escrito para el usuario
            "22001" => "Uno de los datos es más largo de lo permitido.",
            "23505" => "Ya existe un registro con esos datos.",
            "23502" => "Falta un dato obligatorio.",
            "23514" => "Uno de los datos no es un valor válido.",
            "23503" => "El registro relacionado no existe.",
            _ => null
        };
        if (mensaje != null)
        {
            log.LogWarning("Dato rechazado por la base ({Codigo})", pg!.SqlState);
            ctx.Response.StatusCode = 400;
            await ctx.Response.WriteAsJsonAsync(new { mensaje });
            return;
        }

        log.LogError(ex, "Error no controlado en {Ruta}", ctx.Request.Path);
        ctx.Response.StatusCode = 500;
        await ctx.Response.WriteAsJsonAsync(new { mensaje = "Ocurrió un error interno. Ya quedó registrado." });
    }
});

app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});

app.UseCors("web");

// Fotos de la carta. Solo imágenes (validadas por sus bytes al subirlas), sin listado de carpetas.
var rutaArchivos = builder.Configuration["Archivos:Ruta"] ?? Path.Combine(AppContext.BaseDirectory, "archivos");
Directory.CreateDirectory(rutaArchivos);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(rutaArchivos),
    RequestPath = "/archivos",
    ServeUnknownFileTypes = false,
    OnPrepareResponse = c =>
    {
        c.Context.Response.Headers["Cache-Control"] = "public, max-age=604800, immutable";
        c.Context.Response.Headers["Cross-Origin-Resource-Policy"] = "cross-origin";
    }
});
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<ContextoSucursalMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/api/health/live", () => Results.Ok(new { ok = true, status = "live" })).AllowAnonymous();
app.MapGet("/api/health/ready", Readiness).AllowAnonymous();
app.MapGet("/api/health", Readiness).AllowAnonymous();

async Task<IResult> Readiness(ValexaFoodContext db, CancellationToken cancellationToken)
{
    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    timeout.CancelAfter(TimeSpan.FromSeconds(3));
    try
    {
        if (await db.Database.CanConnectAsync(timeout.Token))
            return Results.Ok(new { ok = true, status = "ready", checks = new { database = "ready" } });
        return Results.Json(new { ok = false, status = "not_ready", checks = new { database = "unavailable" } }, statusCode: 503);
    }
    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
    {
        app.Logger.LogWarning("Health readiness timed out while checking PostgreSQL.");
        return Results.Json(new { ok = false, status = "not_ready", checks = new { database = "timeout" } }, statusCode: 503);
    }
    catch (OperationCanceledException)
    {
        throw;
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Health readiness failed while checking PostgreSQL.");
        return Results.Json(new { ok = false, status = "not_ready", checks = new { database = "unavailable" } }, statusCode: 503);
    }
}

app.Run();
