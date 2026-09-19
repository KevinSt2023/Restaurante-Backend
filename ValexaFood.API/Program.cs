using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ValexaFood.Application.Common;
using ValexaFood.Application.Services;
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

builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();

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
app.UseExceptionHandler(err => err.Run(async ctx =>
{
    var ex = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
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
        log.LogWarning(ex, "Dato rechazado por la base ({Codigo})", pg!.SqlState);
        ctx.Response.StatusCode = 400;
        await ctx.Response.WriteAsJsonAsync(new { mensaje });
        return;
    }

    log.LogError(ex, "Error no controlado en {Ruta}", ctx.Request.Path);
    ctx.Response.StatusCode = 500;
    await ctx.Response.WriteAsJsonAsync(new { mensaje = "Ocurrió un error interno. Ya quedó registrado." });
}));

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
app.UseAuthorization();
app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { ok = true })).AllowAnonymous();

app.Run();
