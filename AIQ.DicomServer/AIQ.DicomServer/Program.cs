using FellowOakDicom;
using FellowOakDicom.Imaging.Codec;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Azure;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "DICOM Azure API", Version = "v1" });
});

// Add this to your Program.cs where you configure services
builder.Services.AddFellowOakDicom();

// If you need specific DICOM transcoder services
builder.Services.AddTransient<IDicomTranscoder>(sp =>
    new DicomTranscoder(
        DicomTransferSyntax.ExplicitVRLittleEndian,
        DicomTransferSyntax.JPEG2000Lossless));

// Configure file upload size limits
builder.Services.Configure<FormOptions>(options =>
{
    // Set the limit to 2GB
    options.MultipartBodyLengthLimit = int.MaxValue;
    options.ValueLengthLimit = int.MaxValue;
    options.MemoryBufferThreshold = 104857600; // 100MB before using disk
});

builder.Services.AddAzureClients(clientBuilder =>
{
    clientBuilder.AddBlobServiceClient(builder.Configuration["AzureStorage:ConnectionString"]);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
