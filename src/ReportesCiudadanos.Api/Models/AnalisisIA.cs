namespace ReportesCiudadanos.Api.Models;

public class AnalisisIA
{
    public int Id { get; set; }
    public int ReporteId { get; set; }
    public required string Resumen { get; set; }
    public required string Recomendacion { get; set; }
    public DateTime FechaAnalisis { get; set; }
    public Reporte Reporte { get; set; } = null!;
}
