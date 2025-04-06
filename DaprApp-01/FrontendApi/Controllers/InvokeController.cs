using Dapr.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FrontendApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class InvokeController : ControllerBase
    {
        private readonly DaprClient _daprClient;

        public InvokeController(DaprClient daprClient)
        {
            _daprClient = daprClient;
        }

        // GET /invoke/invoke
        [HttpGet("invoke")]
        public async Task<IActionResult> InvokeTargetService()
        {
            // Prepare the payload to send.
            var requestData = new { Message = "Hello from the Web API client!" };

            // Call the target service using its app-id and the desired endpoint.
            var response = await _daprClient.InvokeMethodAsync<object, string>(
                "target-app-id", // target service's app-id
                "api/hello",     // target service's endpoint
                requestData);

            // Return the response from the target service.
            return Ok(new { response });
        }
    }
}
