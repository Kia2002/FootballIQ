using FootballIQ.Application.Interfaces;
using FootballIQ.Infrastructure.BackgroundServices;
using FootballIQ.Infrastructure.Enrichment;
using FootballIQ.Infrastructure.FootballData;
using FootballIQ.Infrastructure.Persistence;
using FootballIQ.Infrastructure.StatsBomb;
using FootballIQ.WebAPI.Endpoints;
using Microsoft.EntityFrameworkCore;
using Polly;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var connectionString = builder.Configuration["POSTGRES_CONNECTION_STRING"]
    ?? throw new InvalidOperationException("POSTGRES_CONNECTION_STRING is not configured.");

builder.Services.AddDbContext<FootballIQDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<FootballIQDbContext>();

builder.Services.AddScoped<IPlayerRepository, PlayerRepository>();

var statsBombDataRoot = builder.Configuration["STATSBOMB_DATA_ROOT"]
    ?? throw new InvalidOperationException("STATSBOMB_DATA_ROOT is not configured.");

builder.Services.AddScoped<IStatsBombReader>(_ => new StatsBombReader(statsBombDataRoot));
builder.Services.AddScoped<PlayerStatsAggregator>();
builder.Services.AddScoped<IStatsBombIngestionService, StatsBombIngestionService>();

builder.Services.AddSingleton<IngestionWorkQueue>();
builder.Services.AddSingleton<IIngestionQueue>(sp => sp.GetRequiredService<IngestionWorkQueue>());
builder.Services.AddHostedService<DataIngestionBackgroundService>();

builder.Services.AddHttpClient<IWikidataClient, WikidataClient>();
builder.Services.AddScoped<IPlayerEnrichmentService, WikidataEnrichmentService>();

builder.Services.AddSingleton<EnrichmentWorkQueue>();
builder.Services.AddSingleton<IEnrichmentQueue>(sp => sp.GetRequiredService<EnrichmentWorkQueue>());
builder.Services.AddHostedService<PlayerDemographicsBackgroundService>();

var footballDataApiKey = builder.Configuration["FOOTBALLDATA_API_KEY"]
    ?? throw new InvalidOperationException("FOOTBALLDATA_API_KEY is not configured.");

builder.Services.AddHttpClient<FootballDataClient>(client =>
{
    client.BaseAddress = new Uri("https://api.football-data.org/v4/");
    client.DefaultRequestHeaders.Add("X-Auth-Token", footballDataApiKey);
})
.AddTransientHttpErrorPolicy(policyBuilder =>
    policyBuilder.WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapHealthChecks("/api/health");
app.MapPlayerEndpoints();
app.MapAdminEndpoints();

app.Run();
