using GimnasioApi.Hubs;
using GimnasioApi.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configuración de puerto para despliegue en Render (Render inyecta la variable de entorno PORT)
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// 1. Resolver la cadena de conexión DefaultConnection (desde appsettings o variables de entorno)
string connectionString = ResolveConnectionString(builder.Configuration);

// 2. Registrar DbContext con PostgreSQL (Npgsql)
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<GimnasioContext>(options =>
        options.UseNpgsql(connectionString));
}
else
{
    // Registro diferido por si la variable aún no fue provista durante build
    builder.Services.AddDbContext<GimnasioContext>(options => { });
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

// Mostrar errores detallados para diagnosticar problemas de conexión
app.UseDeveloperExceptionPage();

// Configurar Swagger en desarrollo y producción para pruebas fáciles en Render
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Gimnasio API v1");
    c.RoutePrefix = "swagger";
});

app.UseRouting();

// Middleware CORS (debe ir después de UseRouting y antes de UseAuthorization/MapControllers)
app.UseCors("PermitirFrontend");

app.UseAuthorization();

// Mapeo de Controladores y Hub de SignalR
app.MapControllers();
app.MapHub<GimnasioHub>("/ws/gimnasio");

// Endpoint de diagnóstico de base de datos
app.MapGet("/api/health-db", async (GimnasioContext db) =>
{
    try
    {
        var canConnect = await db.Database.CanConnectAsync();
        return Results.Ok(new { conectado = canConnect, mensaje = "Conexión a la base de datos exitosa" });
    }
    catch (Exception ex)
    {
        return Results.Json(new { conectado = false, error = ex.Message, detalle = ex.ToString() }, statusCode: 500);
    }
});

// Endpoint raíz de bienvenida y estado
app.MapGet("/", () => Results.Ok(new
{
    mensaje = "API de Gestión de Gimnasio activa",
    signalr = "/ws/gimnasio",
    swagger = "/swagger",
    databaseConfigurada = !string.IsNullOrWhiteSpace(connectionString)
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

    rawConnection = rawConnection.Trim();

    // Si viene en formato URL (postgres://user:password@host:port/database) común en Render/Supabase
    if (rawConnection.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
        rawConnection.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        try
        {
            var uri = new Uri(rawConnection);
            var userInfo = uri.UserInfo.Split(':');
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
    rawConnection = System.Text.RegularExpressions.Regex.Replace(
        rawConnection,
        @"(^|;)\s*Server\s*=",
        "$1Host=",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    rawConnection = System.Text.RegularExpressions.Regex.Replace(
        rawConnection,
        @"(^|;)\s*User\s+Id\s*=",
        "$1Username=",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    return rawConnection;
}
