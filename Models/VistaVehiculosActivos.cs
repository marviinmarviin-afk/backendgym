using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GimnasioApi.Models;

[Keyless]
[Table("VistaVehiculosActivos")]
public class VistaVehiculosActivos
{
    [Column("Id")]
    public int Id { get; set; }

    [Column("Placa")]
    public string Placa { get; set; } = string.Empty;

    [Column("HoraEntrada")]
    public DateTime HoraEntrada { get; set; }
}
