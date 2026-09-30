using GimnasioApi.Hubs;
using GimnasioApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace GimnasioApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MiembrosController : ControllerBase
{
    private readonly GimnasioContext _context;
    private readonly IHubContext<GimnasioHub> _hubContext;
    private readonly ILogger<MiembrosController> _logger;

    public MiembrosController(
        GimnasioContext context,
        IHubContext<GimnasioHub> hubContext,
        ILogger<MiembrosController> logger)
    {
        _context = context;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene todos los miembros registrados
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Miembro>>> GetMiembros()
    {
        try
        {
            var miembros = await _context.Miembros.ToListAsync();
            return Ok(miembros);
        }
        catch (Exception ex)
        {
            return ErrorInterno(ex, "obtener los miembros");
        }
    }

    /// <summary>
    /// Obtiene los miembros activos desde la vista VistaMiembrosActivos
    /// </summary>
    [HttpGet("activos")]
    public async Task<ActionResult<IEnumerable<VistaMiembrosActivos>>> GetMiembrosActivos()
    {
        try
        {
            var activos = await _context.VistaMiembrosActivos.ToListAsync();
            return Ok(activos);
        }
        catch (Exception ex)
        {
            return ErrorInterno(ex, "obtener los miembros activos");
        }
    }

    /// <summary>
    /// Inscribe un nuevo miembro al gimnasio
    /// POST /api/miembros/inscribir
    /// </summary>
    [HttpPost("inscribir")]
    public async Task<IActionResult> Inscribir([FromBody] InscribirMiembroDto dto)
    {
        if (dto == null)
        {
            return BadRequest(new { mensaje = "Los datos del miembro son requeridos." });
        }

        if (string.IsNullOrWhiteSpace(dto.DPI) || string.IsNullOrWhiteSpace(dto.NombreCompleto))
        {
            return BadRequest(new { mensaje = "DPI y NombreCompleto son campos obligatorios." });
        }

        try
        {
            var dpi = dto.DPI.Trim();

            if (await _context.Miembros.AnyAsync(m => m.DPI == dpi && m.Activo))
            {
                return Conflict(new { mensaje = $"Ya existe un miembro activo con el DPI {dpi}." });
            }

            // Total de activos incluyendo al nuevo miembro
            var totalInscritos = await _context.Miembros.CountAsync(m => m.Activo) + 1;

            var nuevoMiembro = new Miembro
            {
                DPI = dpi,
                NombreCompleto = dto.NombreCompleto.Trim(),
                Telefono = dto.Telefono?.Trim(),
                FechaInscripcion = DateTime.UtcNow,
                FechaCancelacion = null,
                Activo = true
            };

            // Miembro e historial se guardan juntos en una sola operación (atómica)
            _context.Miembros.Add(nuevoMiembro);
            _context.HistorialInscripciones.Add(new HistorialInscripcion
            {
                FechaRegistro = DateTime.UtcNow,
                CantidadTotalInscritos = totalInscritos,
                Accion = "Inscripcion"
            });
            await _context.SaveChangesAsync();

            await NotificarListaActivosAsync();

            return Ok(new
            {
                mensaje = "Miembro inscrito exitosamente.",
                miembro = nuevoMiembro,
                totalActivos = totalInscritos
            });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "No se pudo guardar la inscripción");
            return Conflict(new { mensaje = "No se pudo guardar el miembro. Verifica que el DPI no esté duplicado." });
        }
        catch (Exception ex)
        {
            return ErrorInterno(ex, "inscribir al miembro");
        }
    }

    /// <summary>
    /// Cancela la membresía de un miembro
    /// PUT /api/miembros/cancelar/{id}
    /// </summary>
    [HttpPut("cancelar/{id}")]
    public async Task<IActionResult> Cancelar(int id)
    {
        try
        {
            var miembro = await _context.Miembros.FindAsync(id);
            if (miembro == null)
            {
                return NotFound(new { mensaje = $"No se encontró ningún miembro con Id {id}." });
            }

            if (!miembro.Activo)
            {
                return BadRequest(new { mensaje = $"El miembro con Id {id} ya se encuentra inactivo/cancelado." });
            }

            // Total de activos restantes sin contar al miembro que se cancela
            var totalInscritos = await _context.Miembros.CountAsync(m => m.Activo) - 1;

            miembro.Activo = false;
            miembro.FechaCancelacion = DateTime.UtcNow;

            _context.HistorialInscripciones.Add(new HistorialInscripcion
            {
                FechaRegistro = DateTime.UtcNow,
                CantidadTotalInscritos = totalInscritos,
                Accion = "Cancelacion"
            });
            await _context.SaveChangesAsync();

            await NotificarListaActivosAsync();

            return Ok(new
            {
                mensaje = "Membresía cancelada exitosamente.",
                miembro,
                totalActivos = totalInscritos
            });
        }
        catch (Exception ex)
        {
            return ErrorInterno(ex, "cancelar la membresía");
        }
    }

    /// <summary>
    /// Emite la lista de activos por SignalR. Si falla, no debe romper la operación ya guardada.
    /// </summary>
    private async Task NotificarListaActivosAsync()
    {
        try
        {
            var listaActivos = await _context.VistaMiembrosActivos.ToListAsync();
            await _hubContext.Clients.All.SendAsync("ActualizarLista", listaActivos);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo emitir la actualización por SignalR");
        }
    }

    private ObjectResult ErrorInterno(Exception ex, string accion)
    {
        _logger.LogError(ex, "Error al {Accion}", accion);
        return StatusCode(StatusCodes.Status500InternalServerError,
            new { mensaje = $"Error al {accion}. Intenta de nuevo más tarde." });
    }
}

public class InscribirMiembroDto
{
    public string DPI { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string? Telefono { get; set; }
}
