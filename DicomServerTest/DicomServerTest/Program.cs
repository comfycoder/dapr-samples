using FellowOakDicom;
using Microsoft.Extensions.Configuration;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.Json;

// Read configuration from appsettings.json
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

// Parse configuration values
var dicomConfig = configuration.GetSection("DicomGenerator");
var apiUrl = dicomConfig["ApiUrl"] ?? "https://localhost:5001/api/dicom/upload";
var count = int.Parse(dicomConfig["Count"] ?? "10");
var patientName = dicomConfig["PatientName"] ?? "Doe^John";
var patientId = dicomConfig["PatientId"] ?? "PAT12345";
var studyDescription = dicomConfig["StudyDescription"] ?? "Test Study";
var useZip = bool.Parse(dicomConfig["UseZip"] ?? "false");
var imageSize = int.Parse(dicomConfig["ImageSize"] ?? "512");

// Display configuration
Console.WriteLine("DICOM Generation and Upload Client");
Console.WriteLine("----------------------------------");
Console.WriteLine($"API URL: {apiUrl}");
Console.WriteLine($"Count: {count}");
Console.WriteLine($"Patient Name: {patientName}");
Console.WriteLine($"Patient ID: {patientId}");
Console.WriteLine($"Study Description: {studyDescription}");
Console.WriteLine($"Use ZIP: {useZip}");
Console.WriteLine($"Image Size: {imageSize}px");
Console.WriteLine("----------------------------------");
Console.WriteLine($"Generating {count} DICOM files for patient {patientName}...");

try
{
    if (useZip)
    {
        await GenerateAndUploadAsZip(apiUrl, count, patientName, patientId, studyDescription, imageSize);
    }
    else
    {
        await GenerateAndUploadIndividual(apiUrl, count, patientName, patientId, studyDescription, imageSize);
    }

    Console.WriteLine("Operation completed successfully!");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    Environment.Exit(1);
}

// Function to generate and upload individual DICOM files
async Task GenerateAndUploadIndividual(string apiUrl, int count, string patientName, string patientId, string studyDescription, int imageSize)
{
    // Create a unique study ID
    var studyUid = DicomUIDGenerator.GenerateDerivedFromUUID().UID;
    var studyDate = DateTime.Now.ToString("yyyyMMdd");

    // Setup HTTP client
    using var client = new HttpClient();

    for (int i = 0; i < count; i++)
    {
        Console.WriteLine($"Generating DICOM file {i + 1}/{count}...");

        // Generate a unique series ID for each 5 images
        var seriesIndex = i / 333;
        var seriesUid = DicomUIDGenerator.GenerateDerivedFromUUID().UID;
        var seriesDescription = $"Series {seriesIndex + 1}";

        // Generate a DICOM file
        var dicomFile = GenerateDicomFile(
            patientName,
            patientId,
            studyUid,
            studyDescription,
            studyDate,
            seriesUid,
            seriesDescription,
            i + 1,
            imageSize);

        // Save to temp file
        var tempFilePath = Path.Combine("C:\\temp\\dicoms\\", $"dicom_{i + 1}.dcm");
        await dicomFile.SaveAsync(tempFilePath);

        Console.WriteLine($"Uploading DICOM file {i + 1}/{count}...");

        // Upload to API
        using (var content = new MultipartFormDataContent())
        {
            var fileStream = new FileStream(tempFilePath, FileMode.Open);
            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/dicom");
            content.Add(fileContent, "file", Path.GetFileName(tempFilePath));

            var response = await client.PostAsync(apiUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Error uploading DICOM file: {response.StatusCode}, {errorContent}");
            }

            // Print response
            var responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Response: {response.StatusCode}");

            // Cleanup temp file
            try { File.Delete(tempFilePath); } catch { }
        }
    }
}

// Function to generate and upload DICOM files as a ZIP
async Task GenerateAndUploadAsZip(string apiUrl, int count, string patientName, string patientId, string studyDescription, int imageSize)
{
    // Create a unique study ID
    var studyUid = DicomUIDGenerator.GenerateDerivedFromUUID().UID;
    var studyDate = DateTime.Now.ToString("yyyyMMdd");

    // Create a temp directory to store the DICOM files
    var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    Directory.CreateDirectory(tempDir);

    try
    {
        Console.WriteLine("Generating DICOM files...");

        // Generate all DICOM files
        for (int i = 0; i < count; i++)
        {
            // Generate a unique series ID for each 5 images
            var seriesIndex = i / 5;
            var seriesUid = DicomUIDGenerator.GenerateDerivedFromUUID().UID;
            var seriesDescription = $"Series {seriesIndex + 1}";

            // Generate a DICOM file
            var dicomFile = GenerateDicomFile(
                patientName,
                patientId,
                studyUid,
                studyDescription,
                studyDate,
                seriesUid,
                seriesDescription,
                i + 1,
                imageSize);

            // Save to temp directory
            var filePath = Path.Combine(tempDir, $"dicom_{i + 1}.dcm");
            await dicomFile.SaveAsync(filePath);

            Console.Write(".");
            if ((i + 1) % 20 == 0) Console.WriteLine();
        }

        Console.WriteLine();
        Console.WriteLine("Creating ZIP file...");

        // Create a zip file
        var zipPath = Path.Combine(Path.GetTempPath(), $"dicom_batch_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
        ZipFile.CreateFromDirectory(tempDir, zipPath);

        Console.WriteLine($"Uploading ZIP file containing {count} DICOM files...");

        // Upload to API
        using (var client = new HttpClient())
        using (var content = new MultipartFormDataContent())
        {
            var fileStream = new FileStream(zipPath, FileMode.Open);
            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            content.Add(fileContent, "file", Path.GetFileName(zipPath));

            var response = await client.PostAsync(apiUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Error uploading ZIP file: {response.StatusCode}, {errorContent}");
            }

            // Print response summary
            var responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Response: {response.StatusCode}");

            try
            {
                var responseObj = JsonSerializer.Deserialize<JsonElement>(responseContent);

                if (responseObj.TryGetProperty("SuccessfulFiles", out var successfulFiles) &&
                    responseObj.TryGetProperty("FailedFiles", out var failedFiles))
                {
                    Console.WriteLine($"Successfully uploaded: {successfulFiles.GetInt32()}, Failed: {failedFiles.GetInt32()}");
                }
            }
            catch { /* Ignore parsing errors */ }

            // Cleanup temp files
            try { File.Delete(zipPath); } catch { }
        }
    }
    finally
    {
        // Cleanup temp directory
        try { Directory.Delete(tempDir, true); } catch { }
    }
}

// Function to generate a DICOM file with a test pattern image
DicomFile GenerateDicomFile(
    string patientName,
    string patientId,
    string studyUid,
    string studyDescription,
    string studyDate,
    string seriesUid,
    string seriesDescription,
    int instanceNumber,
    int imageSize)
{
    // Create a new DICOM dataset
    var dataset = new DicomDataset();

    // Add patient information
    dataset.Add(DicomTag.PatientName, patientName);
    dataset.Add(DicomTag.PatientID, patientId);
    dataset.Add(DicomTag.PatientBirthDate, "19700101");
    dataset.Add(DicomTag.PatientSex, "O");

    // Add study information
    dataset.Add(DicomTag.StudyInstanceUID, studyUid);
    dataset.Add(DicomTag.StudyDate, studyDate);
    dataset.Add(DicomTag.StudyTime, DateTime.Now.ToString("HHmmss"));
    dataset.Add(DicomTag.AccessionNumber, $"A{DateTime.Now:yyyyMMdd}");
    dataset.Add(DicomTag.ReferringPhysicianName, "Referring^Doctor");
    dataset.Add(DicomTag.StudyDescription, studyDescription);

    // Add series information
    dataset.Add(DicomTag.SeriesInstanceUID, seriesUid);
    dataset.Add(DicomTag.SeriesNumber, (int)(instanceNumber / 5 + 1));
    dataset.Add(DicomTag.Modality, "CT");
    dataset.Add(DicomTag.SeriesDescription, seriesDescription);

    // Add instance information
    dataset.Add(DicomTag.SOPInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID().UID);
    dataset.Add(DicomTag.SOPClassUID, DicomUID.CTImageStorage);
    dataset.Add(DicomTag.InstanceNumber, (int)(instanceNumber % 5 + 1));

    // Create a simple test pattern image - smaller and simpler to avoid issues
    // Just create a solid gray background with text
    using var bitmap = new Bitmap(imageSize, imageSize);
    using var graphics = Graphics.FromImage(bitmap);

    // Fill with a gradient background
    graphics.Clear(Color.Black);

    // Draw test text
    using var font = new Font("Arial", 24, FontStyle.Bold);
    using var brush = new SolidBrush(Color.White);
    graphics.DrawString($"DICOM #{instanceNumber}", font, brush, 20, 20);
    graphics.DrawString($"Patient: {patientId}", font, brush, 20, 60);
    graphics.DrawString($"Series: {instanceNumber / 5 + 1}", font, brush, 20, 100);
    graphics.DrawString($"Date: {studyDate}", font, brush, 20, 140);

    // Draw crosshair
    using var pen = new Pen(Color.White, 2);
    int centerX = imageSize / 2;
    int centerY = imageSize / 2;
    graphics.DrawLine(pen, centerX - 50, centerY, centerX + 50, centerY);
    graphics.DrawLine(pen, centerX, centerY - 50, centerX, centerY + 50);

    // Convert bitmap to byte array for DICOM pixel data
    using var memoryStream = new MemoryStream();
    bitmap.Save(memoryStream, ImageFormat.Bmp);
    memoryStream.Position = 0;

    // Read the bitmap data
    byte[] pixelData = new byte[memoryStream.Length];
    memoryStream.Read(pixelData, 0, pixelData.Length);

    // Extract raw pixel data (skipping BMP header)
    int bmpHeaderSize = 54; // Standard BMP header size
    int stride = ((imageSize * 3 + 3) / 4) * 4; // BMP stride (row size, aligned to 4 bytes)
    byte[] rawPixelData = new byte[imageSize * imageSize];

    // Convert RGB to grayscale by taking average of RGB values
    for (int y = 0; y < imageSize; y++)
    {
        for (int x = 0; x < imageSize; x++)
        {
            int pixelOffset = bmpHeaderSize + (imageSize - 1 - y) * stride + x * 3;
            if (pixelOffset + 2 < pixelData.Length)
            {
                byte b = pixelData[pixelOffset];
                byte g = pixelData[pixelOffset + 1];
                byte r = pixelData[pixelOffset + 2];

                // Convert to grayscale
                byte gray = (byte)((r + g + b) / 3);

                // Set in the destination array
                rawPixelData[y * imageSize + x] = gray;
            }
        }
    }

    // Add pixel data to dataset
    dataset.Add(DicomTag.Rows, (ushort)imageSize);
    dataset.Add(DicomTag.Columns, (ushort)imageSize);
    dataset.Add(DicomTag.BitsAllocated, (ushort)8);
    dataset.Add(DicomTag.BitsStored, (ushort)8);
    dataset.Add(DicomTag.HighBit, (ushort)7);
    dataset.Add(DicomTag.PixelRepresentation, (ushort)0);
    dataset.Add(DicomTag.PhotometricInterpretation, "MONOCHROME2");
    dataset.Add(DicomTag.SamplesPerPixel, (ushort)1);

    // Add the pixel data
    dataset.Add(new DicomOtherByte(DicomTag.PixelData, rawPixelData));

    // Create and return the DICOM file
    return new DicomFile(dataset);
}