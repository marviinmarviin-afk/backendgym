using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GimnasioApi.Models;

[Table("HistorialOcupacion")]
public class HistorialOcupacion
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("Id")]
    public int Id { get; set; }

    [Column("FechaRegistro")]
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    [Column("CantidadVehiculos")]
    public int CantidadVehiculos { get; set; }

    [Required]
    [Column("Accion")]
    public string Accion { get; set; } = string.Empty;
}
