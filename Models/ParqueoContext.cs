using Microsoft.EntityFrameworkCore;

namespace GimnasioApi.Models;

public class ParqueoContext : DbContext
{
    public ParqueoContext(DbContextOptions<ParqueoContext> options) : base(options)
    {
    }

    public DbSet<RegistroParqueo> RegistrosParqueo => Set<RegistroParqueo>();
    public DbSet<VistaVehiculosActivos> VistaVehiculosActivos => Set<VistaVehiculosActivos>();
    public DbSet<HistorialOcupacion> HistorialOcupacion => Set<HistorialOcupacion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<VistaVehiculosActivos>()
            .ToView("VistaVehiculosActivos")
            .HasNoKey();
    }
}
