using DaprConsoleApp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// 🔐 Add support for user secrets
builder.Configuration
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>(optional: true);

// You can now access secrets or env vars like this:
var mySecret = builder.Configuration["MySuperSecret"];
Console.WriteLine($"MySuperSecret: {mySecret}");

builder.Services.AddDaprClient(); // Dapr client

builder.Services.AddScoped<WeatherHandler>();

var app = builder.Build();

app.MapGet("/weatherforecast", async (WeatherHandler handler) =>
{
    var forecast = await handler.GetForecastAsync();

    return Results.Ok(forecast);
});

app.MapSubscribeHandler(); // <-- enables pub/sub subscription discovery

app.Run();
