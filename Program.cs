using System.Text.RegularExpressions;
using GimnasioApi.Hubs;
using GimnasioApi.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Configuración de puerto para despliegue en Render (Render inyecta la variable de entorno PORT)
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// 1. Resolver la cadena de conexión DefaultConnection (desde appsettings o variables de entorno)
string connectionString = ResolveConnectionString(builder.Configuration);

// 1.1 Validar el formato de la cadena al iniciar para detectar errores de configuración temprano
string? connectionError = null;
if (string.IsNullOrWhiteSpace(connectionString))
{
    connectionError = "No hay cadena de conexión configurada (ConnectionStrings__DefaultConnection).";
    Console.Error.WriteLine($"[DB] {connectionError}");
}
else
{
    try
    {
        _ = new NpgsqlConnectionStringBuilder(connectionString);
    }
    catch (Exception ex)
    {
        connectionError = $"Cadena de conexión inválida: {ex.Message}";
        Console.Error.WriteLine($"[DB] {connectionError}");
    }
}

// 2. Registrar DbContext con PostgreSQL (Npgsql), con reintentos ante fallos transitorios de red
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<ParqueoContext>(options =>
        options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(3)));
}
else
{
    // Registro diferido por si la variable aún no fue provista durante build
    builder.Services.AddDbContext<ParqueoContext>(options => { });
}

// 3. Registrar Controladores
builder.Services.AddControllers();

// 4. Registrar SignalR
builder.Services.AddSignalR();

// 5. Configurar CORS permitiendo frontend local y en Vercel con credenciales habilitadas
builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "https://frontgym-liart.vercel.app")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Esto es vital para que no se caiga el WebSocket
    });
});

// 6. Configurar Swagger / OpenAPI para documentación y pruebas
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Errores detallados solo en desarrollo; en producción se devuelve un mensaje genérico
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new { mensaje = "Ocurrió un error interno en el servidor." });
    }));
}

// Configurar Swagger en desarrollo y producción para pruebas fáciles en Render
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Parqueo API v1");
    c.RoutePrefix = "swagger";
});

app.UseRouting();

// Middleware CORS (debe ir después de UseRouting y antes de UseAuthorization/MapControllers)
app.UseCors("PermitirFrontend");

app.UseAuthorization();

// Mapeo de Controladores y Hub de SignalR
app.MapControllers();
app.MapHub<ParqueoHub>("/ws/parqueo");

// Endpoint de diagnóstico de base de datos (no expone datos sensibles)
app.MapGet("/api/health-db", async (ParqueoContext db) =>
{
    if (connectionError != null)
    {
        return Results.Json(new { conectado = false, error = connectionError }, statusCode: 500);
    }

    try
    {
        var canConnect = await db.Database.CanConnectAsync();
        return Results.Ok(new { conectado = canConnect, mensaje = canConnect
            ? "Conexión a la base de datos exitosa"
            : "No se pudo conectar a la base de datos" });
    }
    catch (Exception ex)
    {
        return Results.Json(new { conectado = false, error = ex.GetBaseException().Message }, statusCode: 500);
    }
});

// Endpoint temporal para diagnóstico detallado
app.MapGet("/api/health-db-detail", async (ParqueoContext db, IConfiguration config) =>
{
    try
    {
        await db.Database.OpenConnectionAsync();
        return Results.Ok(new { conectado = true });
    }
    catch (Exception ex)
    {
        return Results.Json(new { 
            conectado = false, 
            error = ex.Message, 
            detalle = ex.ToString(),
            // No retornar la contraseña!
            cs = Regex.Replace(db.Database.GetConnectionString() ?? "", @"Password=[^;]+", "Password=***")
        }, statusCode: 500);
    }
});

// Endpoint raíz de bienvenida y estado
app.MapGet("/", () => Results.Ok(new
{
    version = "1.0.5",
    mensaje = "API de Control de Parqueo activa",
    signalr = "/ws/parqueo",
    swagger = "/swagger",
    databaseConfigurada = connectionError == null
}));

app.Run();

// Función auxiliar para leer y normalizar cadenas de conexión (compatible con Render y Supabase)
static string ResolveConnectionString(IConfiguration configuration)
{
    var rawConnection = configuration.GetConnectionString("DefaultConnection")
                        ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                        ?? Environment.GetEnvironmentVariable("DefaultConnection")
                        ?? Environment.GetEnvironmentVariable("DATABASE_URL")
                        ?? configuration["DefaultConnection"];

    if (string.IsNullOrWhiteSpace(rawConnection))
    {
        return string.Empty;
    }

    // Quitar comillas y el prefijo accidental "Value:" (pegado desde otros paneles), y unir saltos de línea
    rawConnection = rawConnection.Trim().Trim('"', '\'').Trim();
    rawConnection = Regex.Replace(rawConnection, @"^\s*value\s*:\s*", "", RegexOptions.IgnoreCase);
    rawConnection = Regex.Replace(rawConnection, @"\s*[\r\n]+\s*", "");
    rawConnection = rawConnection.Trim().Trim('"', '\'').Trim();

    // Si viene en formato URL (postgres://user:password@host:port/database) común en Render/Supabase
    if (rawConnection.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
        rawConnection.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        try
        {
            var uri = new Uri(rawConnection);
            var userInfo = uri.UserInfo.Split(':', 2); // 2: la contraseña puede contener ':'
            var user = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "";
            var pass = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
            var db = uri.AbsolutePath.TrimStart('/');
            var hostPort = uri.Port > 0 ? uri.Port : 5432;

            return $"Host={uri.Host};Port={hostPort};Database={db};Username={user};Password={pass};SSL Mode=Require;Trust Server Certificate=true;";
        }
        catch
        {
            // Continuar con normalización estándar si falla el parseo de URI
        }
    }

    // Normalizar palabras clave no soportadas por Npgsql (como Server= o User Id=)
    rawConnection = Regex.Replace(rawConnection, @"(^|;)\s*Server\s*=", "$1Host=", RegexOptions.IgnoreCase);
    rawConnection = Regex.Replace(rawConnection, @"(^|;)\s*User\s+Id\s*=", "$1Username=", RegexOptions.IgnoreCase);

    return rawConnection;
}
