using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GimnasioApi.Models;

[Keyless]
public class VistaMiembrosActivos
{
    [Column("Id")]
    public int Id { get; set; }

    [Column("DPI")]
    public string DPI { get; set; } = string.Empty;

    [Column("NombreCompleto")]
    public string NombreCompleto { get; set; } = string.Empty;

    [Column("Telefono")]
    public string? Telefono { get; set; }

    [Column("FechaInscripcion", TypeName = "timestamp with time zone")]
    public DateTime FechaInscripcion { get; set; }

    [Column("FechaCancelacion", TypeName = "timestamp with time zone")]
    public DateTime? FechaCancelacion { get; set; }

    [Column("Activo")]
    public bool Activo { get; set; }
}
