using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using ReportesCiudadanos.Api.Data;
using ReportesCiudadanos.Api.Services;

DotNetEnv.Env.NoClobber().TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

var groqApiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY");
if (!string.IsNullOrWhiteSpace(groqApiKey))
{
    builder.Configuration["Groq:ApiKey"] = groqApiKey;
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Sistema Inteligente de Reportes Ciudadanos con IA",
        Version = "v1",
        Description =
            "API REST para registrar, administrar y analizar problemáticas urbanas, alineada con el ODS 11."
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IReportesService, ReportesService>();
builder.Services.AddHttpClient<IGroqService, GroqService>(client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["Groq:BaseUrl"]
        ?? "https://api.groq.com/openai/v1/");
    client.Timeout = TimeSpan.FromSeconds(45);
});
builder.Services.AddHttpClient<IGeocodingService, NominatimGeocodingService>(client =>
{
    client.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "ReportesCiudadanosAcademico/1.0 (proyecto-final-diplomado)");
    client.Timeout = TimeSpan.FromSeconds(10);
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.EnsureCreated();
}

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Reportes Ciudadanos v1");
    options.RoutePrefix = string.Empty;
});

app.UseAuthorization();
app.MapControllers();

app.Run();
