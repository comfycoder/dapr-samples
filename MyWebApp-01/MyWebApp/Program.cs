
using Dapr;

namespace MyWebApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddDaprClient(client =>
            {
                client.UseGrpcEndpoint("http://localhost:50001");
            });

            builder.Services.AddControllers().AddDapr();

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseAuthorization();

            // Enable CloudEvents and Dapr subscribe handler if needed
            app.UseCloudEvents();

            app.MapControllers();

            app.MapSubscribeHandler();

            app.Run();
        }
    }
}
