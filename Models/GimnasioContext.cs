using Microsoft.EntityFrameworkCore;

namespace GimnasioApi.Models;

public class GimnasioContext : DbContext
{
    public GimnasioContext(DbContextOptions<GimnasioContext> options) : base(options)
    {
    }

    public DbSet<Miembro> Miembros => Set<Miembro>();
    public DbSet<VistaMiembrosActivos> VistaMiembrosActivos => Set<VistaMiembrosActivos>();
    public DbSet<HistorialInscripcion> HistorialInscripciones => Set<HistorialInscripcion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<VistaMiembrosActivos>()
            .ToView("VistaMiembrosActivos")
            .HasNoKey();
    }
}
