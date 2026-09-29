using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GimnasioApi.Models;

[Table("HistorialInscripciones")]
public class HistorialInscripcion
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("Id")]
    public int Id { get; set; }

    [Column("FechaRegistro", TypeName = "timestamp with time zone")]
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    [Column("CantidadTotalInscritos")]
    public int CantidadTotalInscritos { get; set; }

    [Required]
    [Column("Accion")]
    public string Accion { get; set; } = string.Empty;
}
