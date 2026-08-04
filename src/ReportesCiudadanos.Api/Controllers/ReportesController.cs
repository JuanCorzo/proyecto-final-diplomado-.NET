using Microsoft.AspNetCore.Mvc;
using ReportesCiudadanos.Api.DTOs;
using ReportesCiudadanos.Api.Services;

namespace ReportesCiudadanos.Api.Controllers;

[ApiController]
[Route("api/reportes")]
public class ReportesController : ControllerBase
{
    private readonly IReportesService _reportesService;

    public ReportesController(IReportesService reportesService)
    {
        _reportesService = reportesService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReporteDto>>> Listar(
        [FromQuery] string? categoria,
        [FromQuery] string? estado,
        [FromQuery] string? prioridad,
        [FromQuery] DateTime? fechaInicio,
        [FromQuery] DateTime? fechaFin)
    {
        try
        {
            var reportes = await _reportesService.ListarAsync(
                categoria,
                estado,
                prioridad,
                fechaInicio,
                fechaFin);
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
    public async Task<ActionResult<ReporteDto>> Obtener(int id)
    {
        var reporte = await _reportesService.ObtenerAsync(id);
        return reporte is null ? NotFound() : Ok(reporte);
    }

    [HttpPost]
    public async Task<ActionResult<ReporteDto>> Crear(CrearReporteDto dto)
    {
        var reporte = await _reportesService.CrearAsync(dto);
        return CreatedAtAction(nameof(Obtener), new { id = reporte.Id }, reporte);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ReporteDto>> Actualizar(int id, ActualizarReporteDto dto)
    {
        try
        {
            var reporte = await _reportesService.ActualizarAsync(id, dto);
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
    public async Task<IActionResult> Eliminar(int id)
    {
        var eliminado = await _reportesService.EliminarAsync(id);
        return eliminado ? NoContent() : NotFound();
    }

    [HttpPost("{id:int}/analizar")]
    public async Task<ActionResult<ResultadoAnalisisDto>> Analizar(int id)
    {
        try
        {
            var resultado = await _reportesService.AnalizarAsync(id);
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
