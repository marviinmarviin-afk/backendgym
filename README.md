# Parqueo API - Backend .NET 8 (Web API + EF Core + SignalR)

Backend en .NET 8 para el control de parqueo vehicular con PostgreSQL (Supabase) y actualizaciones en tiempo real mediante SignalR (WebSockets).

---

## 🚀 Arquitectura y Tecnologías

- **Framework**: .NET 8 Web API
- **ORM**: Entity Framework Core con `Npgsql.EntityFrameworkCore.PostgreSQL`
- **WebSockets**: ASP.NET Core SignalR (`/ws/parqueo`)
- **Base de Datos**: PostgreSQL en Supabase
- **Documentación API**: Swagger / OpenAPI en `/swagger`
- **Contenedor**: `Dockerfile` multi-stage optimizado para Render

---

## 📁 Estructura del Proyecto

```text
├── Controllers/
│   └── VehiculosController.cs      # Endpoints REST (entrada, salida, listar, historial)
├── Hubs/
│   └── ParqueoHub.cs               # Canal de transmisión SignalR
├── Models/
│   ├── RegistroParqueo.cs          # Entidad tabla "RegistrosParqueo"
│   ├── VistaVehiculosActivos.cs    # Entidad keyless para la vista "VistaVehiculosActivos"
│   ├── HistorialOcupacion.cs       # Entidad tabla "HistorialOcupacion"
│   └── ParqueoContext.cs           # DbContext con mapeos en PascalCase
├── Dockerfile                      # Despliegue en Render
├── Program.cs                      # Configuración de DI, CORS, SignalR y Rutas
├── appsettings.json                # Plantilla de configuración
└── GimnasioApi.http                # Pruebas HTTP interactivas
```

---

## ⚙️ Configuración de Cadena de Conexión

### Local (`appsettings.json`)
Edita `appsettings.json` con tus credenciales de Supabase:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=db.TU_PROYECTO.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=TU_PASSWORD;SSL Mode=Require;Trust Server Certificate=true;"
  }
}
```

### En Render
Puedes configurar cualquiera de las siguientes variables de entorno en el Dashboard de Render:
- `ConnectionStrings__DefaultConnection` (recomendada): cadena ADO.NET descrita arriba, en una sola línea y **sin prefijos** como `Value:`.
- `DATABASE_URL`: URI de conexión directa de Supabase (`postgres://postgres:password@host:port/postgres`), la cual es convertida y normalizada automáticamente por el backend.

---

## 📡 Endpoints REST

| Método | Ruta | Descripción |
|---|---|---|
| `GET` | `/` | Estado general del servicio |
| `GET` | `/swagger` | Interfaz interactiva Swagger UI |
| `GET` | `/api/vehiculos` | Lista histórica de todos los registros de vehículos |
| `GET` | `/api/vehiculos/activos` | Obtiene los vehículos dentro del parqueo desde `VistaVehiculosActivos` |
| `GET` | `/api/vehiculos/historial` | Historial de ocupación del parqueo |
| `POST` | `/api/vehiculos/entrada` | Registra entrada de vehículo, actualiza historial y emite SignalR (`ActualizarParqueo`) |
| `PUT` | `/api/vehiculos/salida/{id}` | Registra salida de vehículo, actualiza historial y emite SignalR (`ActualizarParqueo`) |

---

## 🔌 Conexión WebSocket (SignalR)

- **Ruta del Hub**: `/ws/parqueo`
- **Evento emitido**: `ActualizarParqueo`
- **Payload recibido**: Lista de objetos de `VistaVehiculosActivos`

### Ejemplo en JavaScript / TypeScript:
```javascript
import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
    .withUrl("https://tu-servicio-en-render.onrender.com/ws/parqueo")
    .withAutomaticReconnect()
    .build();

connection.on("ActualizarParqueo", (vehiculosActivos) => {
    console.log("Vehículos activos en tiempo real:", vehiculosActivos);
});

await connection.start();
```
