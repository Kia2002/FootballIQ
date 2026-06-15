using FootballIQ.Application.Interfaces;
using FootballIQ.Infrastructure.FootballData;
using FootballIQ.Infrastructure.Persistence;
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

app.Run();
