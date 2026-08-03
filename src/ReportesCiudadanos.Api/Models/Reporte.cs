namespace ReportesCiudadanos.Api.Models;

public class Reporte
{
    public int Id { get; set; }
    public required string Titulo { get; set; }
    public required string Descripcion { get; set; }
    public required string Direccion { get; set; }
    public DateTime FechaRegistro { get; set; }
    public required string Estado { get; set; }
    public string? Categoria { get; set; }
    public string? Prioridad { get; set; }
    public double? Latitud { get; set; }
    public double? Longitud { get; set; }
    public string? UbicacionNormalizada { get; set; }
    public AnalisisIA? AnalisisIA { get; set; }
}
