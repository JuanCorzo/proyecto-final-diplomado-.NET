using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReportesCiudadanos.Api.Services;

public sealed record GeocodingResult(
    double Latitud,
    double Longitud,
    string UbicacionNormalizada);

public interface IGeocodingService
{
    Task<GeocodingResult?> BuscarAsync(
        string direccion,
        CancellationToken cancellationToken = default);
}

public sealed class NominatimGeocodingService(
    HttpClient httpClient,
    ILogger<NominatimGeocodingService> logger) : IGeocodingService
{
    public async Task<GeocodingResult?> BuscarAsync(
        string direccion,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = Uri.EscapeDataString(direccion);
            using var response = await httpClient.GetAsync(
                $"search?format=jsonv2&limit=1&q={query}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Nominatim respondió con código {StatusCode}.",
                    response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var resultados = await JsonSerializer.DeserializeAsync<List<NominatimResponse>>(
                stream,
                cancellationToken: cancellationToken);
            var resultado = resultados?.FirstOrDefault();

            if (resultado is null
                || !double.TryParse(resultado.Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitud)
                || !double.TryParse(resultado.Lon, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitud))
            {
                return null;
            }

            return new GeocodingResult(latitud, longitud, resultado.DisplayName);
        }
        catch (Exception ex) when (ex is HttpRequestException
                                   or TaskCanceledException
                                   or JsonException)
        {
            logger.LogWarning(ex, "No fue posible geocodificar la dirección.");
            return null;
        }
    }

    private sealed class NominatimResponse
    {
        [JsonPropertyName("lat")]
        public string Lat { get; set; } = string.Empty;

        [JsonPropertyName("lon")]
        public string Lon { get; set; } = string.Empty;

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; } = string.Empty;
    }
}
