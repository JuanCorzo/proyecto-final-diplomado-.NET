using Microsoft.AspNetCore.Mvc;
using ReportesCiudadanos.Api.DTOs;
using ReportesCiudadanos.Api.Services;

namespace ReportesCiudadanos.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportesController(IReportesService reportesService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ReporteDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ReporteDto>>> Listar(
        [FromQuery] string? categoria,
        [FromQuery] string? estado,
        [FromQuery] string? prioridad,
        [FromQuery] DateTime? fechaInicio,
        [FromQuery] DateTime? fechaFin,
        CancellationToken cancellationToken)
    {
        try
        {
            var reportes = await reportesService.ListarAsync(
                categoria,
                estado,
                prioridad,
                fechaInicio,
                fechaFin,
                cancellationToken);
            return Ok(reportes);
        }
        catch (ArgumentException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Filtros no válidos",
                detail: ex.Message);
        }
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<ReporteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReporteDto>> Obtener(
        int id,
        CancellationToken cancellationToken)
    {
        var reporte = await reportesService.ObtenerAsync(id, cancellationToken);
        return reporte is null ? NotFound() : Ok(reporte);
    }

    [HttpPost]
    [ProducesResponseType<ReporteDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReporteDto>> Crear(
        CrearReporteDto dto,
        CancellationToken cancellationToken)
    {
        var reporte = await reportesService.CrearAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(Obtener), new { id = reporte.Id }, reporte);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<ReporteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReporteDto>> Actualizar(
        int id,
        ActualizarReporteDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var reporte = await reportesService.ActualizarAsync(id, dto, cancellationToken);
            return reporte is null ? NotFound() : Ok(reporte);
        }
        catch (ArgumentException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Datos no válidos",
                detail: ex.Message);
        }
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eliminar(
        int id,
        CancellationToken cancellationToken)
    {
        var eliminado = await reportesService.EliminarAsync(id, cancellationToken);
        return eliminado ? NoContent() : NotFound();
    }

    [HttpPost("{id:int}/analizar")]
    [ProducesResponseType<ResultadoAnalisisDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<ResultadoAnalisisDto>> Analizar(
        int id,
        CancellationToken cancellationToken)
    {
        try
        {
            var resultado = await reportesService.AnalizarAsync(id, cancellationToken);
            return resultado is null ? NotFound() : Ok(resultado);
        }
        catch (GroqServiceException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Análisis pendiente",
                detail: $"{ex.Message} El reporte se conservó con estado 'Pendiente de análisis' y puede reintentarse.");
        }
    }
}
