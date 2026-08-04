using Microsoft.EntityFrameworkCore;
using ReportesCiudadanos.Api.Data;
using ReportesCiudadanos.Api.DTOs;
using ReportesCiudadanos.Api.Models;

namespace ReportesCiudadanos.Api.Services;

public interface IReportesService
{
    Task<IReadOnlyList<ReporteDto>> ListarAsync(
        string? categoria,
        string? estado,
        string? prioridad,
        DateTime? fechaInicio,
        DateTime? fechaFin);

    Task<ReporteDto?> ObtenerAsync(int id);
    Task<ReporteDto> CrearAsync(CrearReporteDto dto);
    Task<ReporteDto?> ActualizarAsync(int id, ActualizarReporteDto dto);
    Task<bool> EliminarAsync(int id);
    Task<ResultadoAnalisisDto?> AnalizarAsync(int id);
}

public sealed class ReportesService : IReportesService
{
    private readonly AppDbContext _dbContext;
    private readonly IGeocodingService _geocodingService;
    private readonly IGroqService _groqService;

    private static readonly Dictionary<string, string> Estados = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Pendiente"] = "Pendiente",
        ["En proceso"] = "En proceso",
        ["Resuelto"] = "Resuelto",
        ["Pendiente de análisis"] = "Pendiente de análisis"
    };

    public ReportesService(
        AppDbContext dbContext,
        IGeocodingService geocodingService,
        IGroqService groqService)
    {
        _dbContext = dbContext;
        _geocodingService = geocodingService;
        _groqService = groqService;
    }

    public async Task<IReadOnlyList<ReporteDto>> ListarAsync(
        string? categoria,
        string? estado,
        string? prioridad,
        DateTime? fechaInicio,
        DateTime? fechaFin)
    {
        if (fechaInicio.HasValue && fechaFin.HasValue && fechaInicio > fechaFin)
        {
            throw new ArgumentException("fechaInicio no puede ser posterior a fechaFin.");
        }

        var query = _dbContext.Reportes
            .AsNoTracking()
            .Include(x => x.AnalisisIA)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(categoria))
        {
            query = query.Where(x =>
                x.Categoria != null && EF.Functions.Like(x.Categoria, $"%{categoria.Trim()}%"));
        }

        if (!string.IsNullOrWhiteSpace(estado))
        {
            query = query.Where(x =>
                EF.Functions.Like(x.Estado, estado.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(prioridad))
        {
            query = query.Where(x =>
                x.Prioridad != null && EF.Functions.Like(x.Prioridad, prioridad.Trim()));
        }

        if (fechaInicio.HasValue)
        {
            query = query.Where(x => x.FechaRegistro >= fechaInicio.Value);
        }

        if (fechaFin.HasValue)
        {
            query = query.Where(x => x.FechaRegistro <= fechaFin.Value);
        }

        var reportes = await query
            .OrderByDescending(x => x.FechaRegistro)
            .ToListAsync();

        return reportes.Select(Mapear).ToList();
    }

    public async Task<ReporteDto?> ObtenerAsync(int id)
    {
        var reporte = await _dbContext.Reportes
            .AsNoTracking()
            .Include(x => x.AnalisisIA)
            .FirstOrDefaultAsync(x => x.Id == id);

        return reporte is null ? null : Mapear(reporte);
    }

    public async Task<ReporteDto> CrearAsync(CrearReporteDto dto)
    {
        var reporte = new Reporte
        {
            Titulo = dto.Titulo.Trim(),
            Descripcion = dto.Descripcion.Trim(),
            Direccion = dto.Direccion.Trim(),
            FechaRegistro = DateTime.UtcNow,
            Estado = "Pendiente"
        };

        await AplicarGeocodificacionAsync(reporte);
        _dbContext.Reportes.Add(reporte);
        await _dbContext.SaveChangesAsync();

        return Mapear(reporte);
    }

    public async Task<ReporteDto?> ActualizarAsync(int id, ActualizarReporteDto dto)
    {
        var reporte = await _dbContext.Reportes
            .Include(x => x.AnalisisIA)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (reporte is null)
        {
            return null;
        }

        if (!Estados.TryGetValue(dto.Estado.Trim(), out var estadoNormalizado))
        {
            throw new ArgumentException(
                "Estado no válido. Use Pendiente, En proceso, Resuelto o Pendiente de análisis.");
        }

        var direccionCambio = !reporte.Direccion.Equals(
            dto.Direccion.Trim(),
            StringComparison.OrdinalIgnoreCase);

        reporte.Titulo = dto.Titulo.Trim();
        reporte.Descripcion = dto.Descripcion.Trim();
        reporte.Direccion = dto.Direccion.Trim();
        reporte.Estado = estadoNormalizado;

        if (direccionCambio)
        {
            reporte.Latitud = null;
            reporte.Longitud = null;
            reporte.UbicacionNormalizada = null;
            await AplicarGeocodificacionAsync(reporte);
        }

        await _dbContext.SaveChangesAsync();
        return Mapear(reporte);
    }

    public async Task<bool> EliminarAsync(int id)
    {
        var reporte = await _dbContext.Reportes.FindAsync(id);
        if (reporte is null)
        {
            return false;
        }

        _dbContext.Reportes.Remove(reporte);
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<ResultadoAnalisisDto?> AnalizarAsync(int id)
    {
        var reporte = await _dbContext.Reportes
            .Include(x => x.AnalisisIA)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (reporte is null)
        {
            return null;
        }

        try
        {
            var resultado = await _groqService.AnalizarAsync(
                reporte.Titulo,
                reporte.Descripcion,
                reporte.Direccion);

            reporte.Categoria = resultado.Categoria;
            reporte.Prioridad = resultado.Prioridad;
            if (reporte.Estado == "Pendiente de análisis")
            {
                reporte.Estado = "Pendiente";
            }

            if (reporte.AnalisisIA is null)
            {
                reporte.AnalisisIA = new AnalisisIA
                {
                    ReporteId = reporte.Id,
                    Resumen = resultado.Resumen,
                    Recomendacion = resultado.Recomendacion,
                    FechaAnalisis = DateTime.UtcNow
                };
            }
            else
            {
                reporte.AnalisisIA.Resumen = resultado.Resumen;
                reporte.AnalisisIA.Recomendacion = resultado.Recomendacion;
                reporte.AnalisisIA.FechaAnalisis = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();
            return new ResultadoAnalisisDto(
                resultado.Categoria,
                resultado.Prioridad,
                reporte.AnalisisIA.Resumen,
                reporte.AnalisisIA.Recomendacion,
                reporte.AnalisisIA.FechaAnalisis);
        }
        catch (GroqServiceException)
        {
            reporte.Estado = "Pendiente de análisis";
            await _dbContext.SaveChangesAsync();
            throw;
        }
    }

    private async Task AplicarGeocodificacionAsync(Reporte reporte)
    {
        var ubicacion = await _geocodingService.BuscarAsync(reporte.Direccion);
        if (ubicacion is null)
        {
            return;
        }

        reporte.Latitud = ubicacion.Latitud;
        reporte.Longitud = ubicacion.Longitud;
        reporte.UbicacionNormalizada = ubicacion.UbicacionNormalizada;
    }

    private static ReporteDto Mapear(Reporte reporte)
    {
        var analisis = reporte.AnalisisIA is null
            ? null
            : new AnalisisIADto(
                reporte.AnalisisIA.Id,
                reporte.AnalisisIA.Resumen,
                reporte.AnalisisIA.Recomendacion,
                reporte.AnalisisIA.FechaAnalisis);

        return new ReporteDto(
            reporte.Id,
            reporte.Titulo,
            reporte.Descripcion,
            reporte.Direccion,
            reporte.FechaRegistro,
            reporte.Estado,
            reporte.Categoria,
            reporte.Prioridad,
            reporte.Latitud,
            reporte.Longitud,
            reporte.UbicacionNormalizada,
            analisis);
    }
}
