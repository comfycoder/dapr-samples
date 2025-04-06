using Dapr.Client;
using System.Text.Json;
using WeatherClient;

var client = new DaprClientBuilder().Build();

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

// Example: Calling POST /weatherforecast on "myservice"
var response = await client.InvokeMethodAsync<List<WeatherForecast>>(
    HttpMethod.Get,         // or HttpMethod.Post
    "weather-processor",    // app ID of the callee
    "weatherforecast"       // method (path) to call
);

// convert the list back to JSON string
var json = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });

// print it to the console
Console.WriteLine("🌤️ WeatherForecast Response (raw JSON):\n" + json);
