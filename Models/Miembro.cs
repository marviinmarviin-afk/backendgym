using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GimnasioApi.Models;

[Table("Miembros")]
public class Miembro
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("Id")]
    public int Id { get; set; }

    [Required]
    [Column("DPI")]
    public string DPI { get; set; } = string.Empty;

    [Required]
    [Column("NombreCompleto")]
    public string NombreCompleto { get; set; } = string.Empty;

    [Column("Telefono")]
    public string? Telefono { get; set; }

    [Column("FechaInscripcion", TypeName = "timestamp with time zone")]
    public DateTime FechaInscripcion { get; set; } = DateTime.UtcNow;

    [Column("FechaCancelacion", TypeName = "timestamp with time zone")]
    public DateTime? FechaCancelacion { get; set; }

    [Column("Activo")]
    public bool Activo { get; set; } = true;
}
