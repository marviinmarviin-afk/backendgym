using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GimnasioApi.Models;

[Table("RegistrosParqueo")]
public class RegistroParqueo
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("Id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("Placa")]
    public string Placa { get; set; } = string.Empty;

    [Column("HoraEntrada")]
    public DateTime HoraEntrada { get; set; } = DateTime.UtcNow;

    [Column("HoraSalida")]
    public DateTime? HoraSalida { get; set; }

    [Column("Activo")]
    public bool Activo { get; set; } = true;
}
