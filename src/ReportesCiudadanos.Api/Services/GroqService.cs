using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReportesCiudadanos.Api.Services;

public sealed record GroqAnalysisResult(
    string Categoria,
    string Prioridad,
    string Resumen,
    string Recomendacion);

public interface IGroqService
{
    Task<GroqAnalysisResult> AnalizarAsync(
        string titulo,
        string descripcion,
        string direccion,
        CancellationToken cancellationToken = default);
}

public sealed class GroqService(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<GroqService> logger) : IGroqService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<GroqAnalysisResult> AnalizarAsync(
        string titulo,
        string descripcion,
        string direccion,
        CancellationToken cancellationToken = default)
    {
        var apiKey = configuration["Groq:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new GroqServiceException(
                "La clave de Groq no está configurada. Use Groq:ApiKey en User Secrets o variables de entorno.");
        }

        var model = configuration["Groq:Model"] ?? "llama-3.3-70b-versatile";
        var requestBody = new
        {
            model,
            temperature = 0.1,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content =
                        """
                        Analiza reportes ciudadanos urbanos. Responde exclusivamente con un objeto JSON
                        con las propiedades categoria, prioridad, resumen y recomendacion.
                        La prioridad debe ser exactamente Alta, Media o Baja.
                        La categoría debe ser concreta, por ejemplo: Baches, Alumbrado público,
                        Basuras, Zonas verdes, Espacio público u Otra.
                        La recomendación debe indicar la entidad o dependencia responsable.
                        """
                },
                new
                {
                    role = "user",
                    content = $"Título: {titulo}\nDescripción: {descripcion}\nDirección: {direccion}"
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Groq respondió con código {StatusCode}: {Response}",
                    response.StatusCode,
                    responseJson);
                throw new GroqServiceException(
                    $"Groq no pudo completar el análisis (HTTP {(int)response.StatusCode}).");
            }

            var completion = JsonSerializer.Deserialize<GroqCompletionResponse>(responseJson, JsonOptions);
            var content = completion?.Choices.FirstOrDefault()?.Message.Content;
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new GroqServiceException("Groq devolvió una respuesta vacía.");
            }

            var analysis = JsonSerializer.Deserialize<GroqAnalysisPayload>(
                ExtraerJson(content),
                JsonOptions);

            if (analysis is null
                || string.IsNullOrWhiteSpace(analysis.Categoria)
                || string.IsNullOrWhiteSpace(analysis.Prioridad)
                || string.IsNullOrWhiteSpace(analysis.Resumen)
                || string.IsNullOrWhiteSpace(analysis.Recomendacion))
            {
                throw new GroqServiceException("La respuesta de Groq no tiene la estructura esperada.");
            }

            var prioridades = new[] { "Alta", "Media", "Baja" };
            var prioridad = prioridades.FirstOrDefault(
                x => x.Equals(analysis.Prioridad.Trim(), StringComparison.OrdinalIgnoreCase));
            if (prioridad is null)
            {
                throw new GroqServiceException("Groq devolvió una prioridad no válida.");
            }

            return new GroqAnalysisResult(
                analysis.Categoria.Trim(),
                prioridad,
                analysis.Resumen.Trim(),
                analysis.Recomendacion.Trim());
        }
        catch (GroqServiceException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException
                                   or TaskCanceledException
                                   or JsonException)
        {
            logger.LogWarning(ex, "Error al comunicarse con Groq.");
            throw new GroqServiceException(
                "No fue posible comunicarse con el servicio de inteligencia artificial.",
                ex);
        }
    }

    private static string ExtraerJson(string content)
    {
        var inicio = content.IndexOf('{');
        var fin = content.LastIndexOf('}');
        return inicio >= 0 && fin > inicio
            ? content[inicio..(fin + 1)]
            : content;
    }

    private sealed class GroqCompletionResponse
    {
        public List<GroqChoice> Choices { get; set; } = [];
    }

    private sealed class GroqChoice
    {
        public GroqMessage Message { get; set; } = new();
    }

    private sealed class GroqMessage
    {
        public string Content { get; set; } = string.Empty;
    }

    private sealed class GroqAnalysisPayload
    {
        public string Categoria { get; set; } = string.Empty;
        public string Prioridad { get; set; } = string.Empty;
        public string Resumen { get; set; } = string.Empty;

        [JsonPropertyName("recomendacion")]
        public string Recomendacion { get; set; } = string.Empty;
    }
}

public sealed class GroqServiceException : Exception
{
    public GroqServiceException(string message) : base(message)
    {
    }

    public GroqServiceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
