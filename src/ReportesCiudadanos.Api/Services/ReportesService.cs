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
        DateTime? fechaFin,
        CancellationToken cancellationToken = default);

    Task<ReporteDto?> ObtenerAsync(int id, CancellationToken cancellationToken = default);
    Task<ReporteDto> CrearAsync(CrearReporteDto dto, CancellationToken cancellationToken = default);
    Task<ReporteDto?> ActualizarAsync(int id, ActualizarReporteDto dto, CancellationToken cancellationToken = default);
    Task<bool> EliminarAsync(int id, CancellationToken cancellationToken = default);
    Task<ResultadoAnalisisDto?> AnalizarAsync(int id, CancellationToken cancellationToken = default);
}

public sealed class ReportesService(
    AppDbContext dbContext,
    IGeocodingService geocodingService,
    IGroqService groqService) : IReportesService
{
    private static readonly Dictionary<string, string> Estados = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Pendiente"] = "Pendiente",
        ["En proceso"] = "En proceso",
        ["Resuelto"] = "Resuelto",
        ["Pendiente de análisis"] = "Pendiente de análisis"
    };

    public async Task<IReadOnlyList<ReporteDto>> ListarAsync(
        string? categoria,
        string? estado,
        string? prioridad,
        DateTime? fechaInicio,
        DateTime? fechaFin,
        CancellationToken cancellationToken = default)
    {
        if (fechaInicio.HasValue && fechaFin.HasValue && fechaInicio > fechaFin)
        {
            throw new ArgumentException("fechaInicio no puede ser posterior a fechaFin.");
        }

        var query = dbContext.Reportes
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
            .ToListAsync(cancellationToken);

        return reportes.Select(Mapear).ToList();
    }

    public async Task<ReporteDto?> ObtenerAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var reporte = await dbContext.Reportes
            .AsNoTracking()
            .Include(x => x.AnalisisIA)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return reporte is null ? null : Mapear(reporte);
    }

    public async Task<ReporteDto> CrearAsync(
        CrearReporteDto dto,
        CancellationToken cancellationToken = default)
    {
        var reporte = new Reporte
        {
            Titulo = dto.Titulo.Trim(),
            Descripcion = dto.Descripcion.Trim(),
            Direccion = dto.Direccion.Trim(),
            FechaRegistro = DateTime.UtcNow,
            Estado = "Pendiente"
        };

        await AplicarGeocodificacionAsync(reporte, cancellationToken);
        dbContext.Reportes.Add(reporte);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Mapear(reporte);
    }

    public async Task<ReporteDto?> ActualizarAsync(
        int id,
        ActualizarReporteDto dto,
        CancellationToken cancellationToken = default)
    {
        var reporte = await dbContext.Reportes
            .Include(x => x.AnalisisIA)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
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
            await AplicarGeocodificacionAsync(reporte, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Mapear(reporte);
    }

    public async Task<bool> EliminarAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var reporte = await dbContext.Reportes.FindAsync([id], cancellationToken);
        if (reporte is null)
        {
            return false;
        }

        dbContext.Reportes.Remove(reporte);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ResultadoAnalisisDto?> AnalizarAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var reporte = await dbContext.Reportes
            .Include(x => x.AnalisisIA)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (reporte is null)
        {
            return null;
        }

        try
        {
            var resultado = await groqService.AnalizarAsync(
                reporte.Titulo,
                reporte.Descripcion,
                reporte.Direccion,
                cancellationToken);

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

            await dbContext.SaveChangesAsync(cancellationToken);
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
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private async Task AplicarGeocodificacionAsync(
        Reporte reporte,
        CancellationToken cancellationToken)
    {
        var ubicacion = await geocodingService.BuscarAsync(
            reporte.Direccion,
            cancellationToken);
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
