using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Dapr.Client;
using System.Threading.Tasks;
using MyWebApp.Models;

namespace MyWebApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StateController : ControllerBase
    {
        // Use the same name as defined in your Dapr component YAML (e.g., "statestore")
        private const string StoreName = "cosmosdb";
        private readonly DaprClient _daprClient;

        public StateController(DaprClient daprClient)
        {
            _daprClient = daprClient;
        }

        // Endpoint to save state using the custom MyStateItem model.
        [HttpPost("{key}")]
        public async Task<IActionResult> SaveState(string key, [FromBody] MyStateItem state)
        {
            try
            {
                await _daprClient.SaveStateAsync(StoreName, key, state);
                return Ok($"State for key '{key}' saved successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving state: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                throw;
            }
        }

        // Endpoint to retrieve state using the custom MyStateItem model.
        [HttpGet("{key}")]
        public async Task<IActionResult> GetState(string key)
        {
            var state = await _daprClient.GetStateAsync<MyStateItem>(StoreName, key);
            if (state == null)
            {
                return NotFound($"State for key '{key}' not found.");
            }
            return Ok(state);
        }
    }
}
