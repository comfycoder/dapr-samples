using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using TestAppModels;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Define an endpoint that matches the method name invoked by the client.
app.MapPost("/api/hello", (dynamic requestData) =>
{
    // Process the request data.
    string message = requestData?.Message + string.Empty;
    return Results.Ok($"Received your message: {message}");
});

// Run the app on the designated port.
app.Run();
