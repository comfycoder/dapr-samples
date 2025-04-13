using Dapr.Client;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MyFrontEnd.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly DaprClient _daprClient;

        public IndexModel(ILogger<IndexModel> logger, DaprClient daprClient)
        {
            _logger = logger;
            _daprClient = daprClient;
        }

        public async Task OnGet()
        {
            var forecasts = await _daprClient.InvokeMethodAsync<IEnumerable<WeatherForecast>>(
                HttpMethod.Get,
                "mybackend",
                "weatherforecast");

            ViewData["WeatherForecastData"] = forecasts;
        }
    }
}