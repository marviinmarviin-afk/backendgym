using GimnasioApi.Hubs;
using GimnasioApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace GimnasioApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VehiculosController : ControllerBase
{
    private readonly ParqueoContext _context;
    private readonly IHubContext<ParqueoHub> _hubContext;
    private readonly ILogger<VehiculosController> _logger;

    public VehiculosController(
        ParqueoContext context,
        IHubContext<ParqueoHub> hubContext,
        ILogger<VehiculosController> logger)
    {
        _context = context;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene todos los registros históricos de vehículos en el parqueo
    /// GET /api/vehiculos
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<RegistroParqueo>>> GetVehiculos()
    {
        try
        {
            var vehiculos = await _context.RegistrosParqueo.ToListAsync();
            return Ok(vehiculos);
        }
        catch (Exception ex)
        {
            return ErrorInterno(ex, "obtener los registros de vehículos");
        }
    }

    /// <summary>
    /// Obtiene los vehículos activos desde la vista VistaVehiculosActivos
    /// GET /api/vehiculos/activos
    /// </summary>
    [HttpGet("activos")]
    public async Task<ActionResult<IEnumerable<VistaVehiculosActivos>>> GetVehiculosActivos()
    {
        try
        {
            var activos = await _context.VistaVehiculosActivos.ToListAsync();
            return Ok(activos);
        }
        catch (Exception ex)
        {
            return ErrorInterno(ex, "obtener los vehículos activos");
        }
    }

    /// <summary>
    /// Obtiene el historial de ocupación
    /// GET /api/vehiculos/historial
    /// </summary>
    [HttpGet("historial")]
    public async Task<ActionResult<IEnumerable<HistorialOcupacion>>> GetHistorial()
    {
        try
        {
            var historial = await _context.HistorialOcupacion
                .OrderByDescending(h => h.FechaRegistro)
                .ToListAsync();
            return Ok(historial);
        }
        catch (Exception ex)
        {
            return ErrorInterno(ex, "obtener el historial de ocupación");
        }
    }

    /// <summary>
    /// Registra la entrada de un vehículo
    /// POST /api/vehiculos/entrada
    /// </summary>
    [HttpPost("entrada")]
    public async Task<IActionResult> Entrada([FromBody] EntradaVehiculoDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Placa))
        {
            return BadRequest(new { mensaje = "La placa del vehículo es obligatoria." });
        }

        var placa = dto.Placa.Trim();
        if (placa.Length > 20)
        {
            return BadRequest(new { mensaje = "La placa no puede tener más de 20 caracteres." });
        }

        try
        {
            var nuevoRegistro = new RegistroParqueo
            {
                Placa = placa,
                HoraEntrada = DateTime.UtcNow,
                Activo = true
            };

            _context.RegistrosParqueo.Add(nuevoRegistro);
            await _context.SaveChangesAsync();

            var totalActivos = await _context.RegistrosParqueo.CountAsync(r => r.Activo);

            var historial = new HistorialOcupacion
            {
                FechaRegistro = DateTime.UtcNow,
                CantidadVehiculos = totalActivos,
                Accion = "Entrada"
            };

            _context.HistorialOcupacion.Add(historial);
            await _context.SaveChangesAsync();

            var listaActivos = await NotificarParqueoAsync();

            return Ok(new
            {
                mensaje = "Entrada registrada exitosamente.",
                registro = nuevoRegistro,
                totalActivos,
                vehiculosActivos = listaActivos
            });
        }
        catch (Exception ex)
        {
            return ErrorInterno(ex, "registrar la entrada del vehículo");
        }
    }

    /// <summary>
    /// Registra la salida de un vehículo por Id
    /// PUT /api/vehiculos/salida/{id}
    /// </summary>
    [HttpPut("salida/{id}")]
    public async Task<IActionResult> Salida(int id)
    {
        try
        {
            var vehiculo = await _context.RegistrosParqueo.FindAsync(id);
            if (vehiculo == null)
            {
                return NotFound(new { mensaje = $"No se encontró ningún vehículo con Id {id}." });
            }

            if (!vehiculo.Activo)
            {
                return BadRequest(new { mensaje = $"El vehículo con Id {id} ya se encuentra inactivo/ha salido." });
            }

            vehiculo.Activo = false;
            vehiculo.HoraSalida = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var totalActivos = await _context.RegistrosParqueo.CountAsync(r => r.Activo);

            var historial = new HistorialOcupacion
            {
                FechaRegistro = DateTime.UtcNow,
                CantidadVehiculos = totalActivos,
                Accion = "Salida"
            };

            _context.HistorialOcupacion.Add(historial);
            await _context.SaveChangesAsync();

            var listaActivos = await NotificarParqueoAsync();

            return Ok(new
            {
                mensaje = "Salida registrada exitosamente.",
                registro = vehiculo,
                totalActivos,
                vehiculosActivos = listaActivos
            });
        }
        catch (Exception ex)
        {
            return ErrorInterno(ex, "registrar la salida del vehículo");
        }
    }

    /// <summary>
    /// Consulta VistaVehiculosActivos y emite la lista completa por WebSocket con el evento ActualizarParqueo
    /// </summary>
    private async Task<List<VistaVehiculosActivos>> NotificarParqueoAsync()
    {
        try
        {
            var vehiculosActivos = await _context.VistaVehiculosActivos.ToListAsync();
            await _hubContext.Clients.All.SendAsync("ActualizarParqueo", vehiculosActivos);
            return vehiculosActivos;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo emitir la actualización de parqueo por SignalR");
            return new List<VistaVehiculosActivos>();
        }
    }

    private ObjectResult ErrorInterno(Exception ex, string accion)
    {
        _logger.LogError(ex, "Error al {Accion}", accion);
        return StatusCode(StatusCodes.Status500InternalServerError,
            new { mensaje = $"Error al {accion}. Intenta de nuevo más tarde." });
    }
}

public class EntradaVehiculoDto
{
    public string Placa { get; set; } = string.Empty;
}
