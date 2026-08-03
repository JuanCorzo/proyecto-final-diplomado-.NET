using System.ComponentModel.DataAnnotations;

namespace ReportesCiudadanos.Api.DTOs;

public sealed class CrearReporteDto
{
    [Required, StringLength(120, MinimumLength = 3)]
    public string Titulo { get; set; } = string.Empty;

    [Required, StringLength(2000, MinimumLength = 10)]
    public string Descripcion { get; set; } = string.Empty;

    [Required, StringLength(250, MinimumLength = 5)]
    public string Direccion { get; set; } = string.Empty;
}

public sealed class ActualizarReporteDto
{
    [Required, StringLength(120, MinimumLength = 3)]
    public string Titulo { get; set; } = string.Empty;

    [Required, StringLength(2000, MinimumLength = 10)]
    public string Descripcion { get; set; } = string.Empty;

    [Required, StringLength(250, MinimumLength = 5)]
    public string Direccion { get; set; } = string.Empty;

    [Required]
    public string Estado { get; set; } = string.Empty;
}

public sealed record AnalisisIADto(
    int Id,
    string Resumen,
    string Recomendacion,
    DateTime FechaAnalisis);

public sealed record ReporteDto(
    int Id,
    string Titulo,
    string Descripcion,
    string Direccion,
    DateTime FechaRegistro,
    string Estado,
    string? Categoria,
    string? Prioridad,
    double? Latitud,
    double? Longitud,
    string? UbicacionNormalizada,
    AnalisisIADto? AnalisisIA);

public sealed record ResultadoAnalisisDto(
    string Categoria,
    string Prioridad,
    string Resumen,
    string Recomendacion,
    DateTime FechaAnalisis);
