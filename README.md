# Gimnasio API - Backend .NET 8 (Web API + EF Core + SignalR)

Backend en .NET 8 para la gestión de inscripciones de gimnasio con PostgreSQL (Supabase) y actualizaciones en tiempo real mediante SignalR (WebSockets).

---

## 🚀 Arquitectura y Tecnologías

- **Framework**: .NET 8 Web API
- **ORM**: Entity Framework Core con `Npgsql.EntityFrameworkCore.PostgreSQL`
- **WebSockets**: ASP.NET Core SignalR (`/ws/gimnasio`)
- **Base de Datos**: PostgreSQL en Supabase
- **Documentación API**: Swagger / OpenAPI en `/swagger`
- **Contenedor**: `Dockerfile` multi-stage optimizado para Render

---

## 📁 Estructura del Proyecto

```text
├── Controllers/
│   └── MiembrosController.cs       # Endpoints REST (inscribir, cancelar, listar)
├── Hubs/
│   └── GimnasioHub.cs              # Canal de transmisión SignalR
├── Models/
│   ├── Miembro.cs                  # Entidad tabla "Miembros"
│   ├── VistaMiembrosActivos.cs     # Entidad para la vista "VistaMiembrosActivos"
│   ├── HistorialInscripcion.cs     # Entidad tabla "HistorialInscripciones"
│   └── GimnasioContext.cs          # DbContext con mapeos en PascalCase
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
- `DefaultConnection`: Misma cadena ADO.NET descrita arriba.
- `DATABASE_URL`: URI de conexión directa de Supabase (`postgres://postgres:password@host:port/postgres`), la cual es convertida y normalizada automáticamente por el backend.

---

## 📡 Endpoints REST

| Método | Ruta | Descripción |
|---|---|---|
| `GET` | `/` | Estado general del servicio |
| `GET` | `/swagger` | Interfaz interactiva Swagger UI |
| `GET` | `/api/miembros` | Lista histórica de todos los miembros |
| `GET` | `/api/miembros/activos` | Obtiene los miembros de `VistaMiembrosActivos` |
| `POST` | `/api/miembros/inscribir` | Registra miembro, agrega historial y emite SignalR |
| `PUT` | `/api/miembros/cancelar/{id}` | Cancela membresía, agrega historial y emite SignalR |

---

## 🔌 Conexión WebSocket (SignalR)

- **Ruta del Hub**: `/ws/gimnasio`
- **Evento emitido**: `ActualizarLista`
- **Payload recibido**: Lista de objetos de `VistaMiembrosActivos`

### Ejemplo en JavaScript / TypeScript:
```javascript
import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
    .withUrl("https://tu-servicio-en-render.onrender.com/ws/gimnasio")
    .withAutomaticReconnect()
    .build();

connection.on("ActualizarLista", (miembrosActivos) => {
    console.log("Lista actualizada en tiempo real:", miembrosActivos);
});

await connection.start();
```
