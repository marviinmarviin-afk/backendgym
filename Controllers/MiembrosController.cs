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

    public MiembrosController(GimnasioContext context, IHubContext<GimnasioHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    /// <summary>
    /// Obtiene todos los miembros registrados
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Miembro>>> GetMiembros()
    {
        var miembros = await _context.Miembros.ToListAsync();
        return Ok(miembros);
    }

    /// <summary>
    /// Obtiene los miembros activos desde la vista VistaMiembrosActivos
    /// </summary>
    [HttpGet("activos")]
    public async Task<ActionResult<IEnumerable<VistaMiembrosActivos>>> GetMiembrosActivos()
    {
        var activos = await _context.VistaMiembrosActivos.ToListAsync();
        return Ok(activos);
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

        var nuevoMiembro = new Miembro
        {
            DPI = dto.DPI.Trim(),
            NombreCompleto = dto.NombreCompleto.Trim(),
            Telefono = dto.Telefono?.Trim(),
            FechaInscripcion = DateTime.UtcNow,
            FechaCancelacion = null,
            Activo = true
        };

        _context.Miembros.Add(nuevoMiembro);
        await _context.SaveChangesAsync();

        // Calcular la cantidad total de miembros activos inscritos
        var totalInscritos = await _context.Miembros.CountAsync(m => m.Activo);

        var historial = new HistorialInscripcion
        {
            FechaRegistro = DateTime.UtcNow,
            CantidadTotalInscritos = totalInscritos,
            Accion = "Inscripcion"
        };
        _context.HistorialInscripciones.Add(historial);
        await _context.SaveChangesAsync();

        // Emitir los datos de la vista a través del WebSocket
        var listaActivos = await _context.VistaMiembrosActivos.ToListAsync();
        await _hubContext.Clients.All.SendAsync("ActualizarLista", listaActivos);

        return Ok(new
        {
            mensaje = "Miembro inscrito exitosamente.",
            miembro = nuevoMiembro,
            totalActivos = totalInscritos
        });
    }

    /// <summary>
    /// Cancela la membresía de un miembro
    /// PUT /api/miembros/cancelar/{id}
    /// </summary>
    [HttpPut("cancelar/{id}")]
    public async Task<IActionResult> Cancelar(int id)
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

        miembro.Activo = false;
        miembro.FechaCancelacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Calcular la cantidad total de miembros activos restante
        var totalInscritos = await _context.Miembros.CountAsync(m => m.Activo);

        var historial = new HistorialInscripcion
        {
            FechaRegistro = DateTime.UtcNow,
            CantidadTotalInscritos = totalInscritos,
            Accion = "Cancelacion"
        };
        _context.HistorialInscripciones.Add(historial);
        await _context.SaveChangesAsync();

        // Emitir actualización por WebSocket
        var listaActivos = await _context.VistaMiembrosActivos.ToListAsync();
        await _hubContext.Clients.All.SendAsync("ActualizarLista", listaActivos);

        return Ok(new
        {
            mensaje = "Membresía cancelada exitosamente.",
            miembro,
            totalActivos = totalInscritos
        });
    }
}

public class InscribirMiembroDto
{
    public string DPI { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string? Telefono { get; set; }
}
