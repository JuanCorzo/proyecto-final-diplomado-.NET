using Microsoft.EntityFrameworkCore;
using ReportesCiudadanos.Api.Models;

namespace ReportesCiudadanos.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Reporte> Reportes => Set<Reporte>();
    public DbSet<AnalisisIA> AnalisisIA => Set<AnalisisIA>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Reporte>(entity =>
        {
            entity.Property(x => x.Titulo).HasMaxLength(120);
            entity.Property(x => x.Descripcion).HasMaxLength(2000);
            entity.Property(x => x.Direccion).HasMaxLength(250);
            entity.Property(x => x.Estado).HasMaxLength(30);
            entity.Property(x => x.Categoria).HasMaxLength(100);
            entity.Property(x => x.Prioridad).HasMaxLength(20);
            entity.Property(x => x.UbicacionNormalizada).HasMaxLength(500);

            entity.HasOne(x => x.AnalisisIA)
                .WithOne(x => x.Reporte)
                .HasForeignKey<AnalisisIA>(x => x.ReporteId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
