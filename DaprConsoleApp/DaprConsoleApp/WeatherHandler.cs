using Microsoft.Extensions.Logging;

namespace DaprConsoleApp
{
    public class WeatherHandler
    {
        private readonly ILogger<WeatherHandler> _logger;

        public WeatherHandler(ILogger<WeatherHandler> logger)
        {
            _logger = logger;
        }

        public async Task<IEnumerable<WeatherForecast>> GetForecastAsync()
        {
            _logger.LogInformation("Fetching forecast...");

            await Task.Delay(200); // simulate async work

            return new[]
            {
                new WeatherForecast
                {
                    Date = DateTime.Now,
                    TempC = 20,
                    Summary = "Sunny"
                },
                new WeatherForecast
                {
                    Date = DateTime.Now.AddDays(1),
                    TempC = 18,
                    Summary = "Cloudy"
                }
            };
        }
    }
}
