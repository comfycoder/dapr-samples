using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using FellowOakDicom;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;

namespace AIQ.DicomServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DicomController : ControllerBase
    {
        private readonly string _connectionString;
        private readonly string _containerName;
        private readonly ILogger<DicomController> _logger;

        public DicomController(ILogger<DicomController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _connectionString = configuration["AzureStorage:ConnectionString"] + string.Empty;
            _containerName = configuration["AzureStorage:ContainerName"] ?? "dicomfiles";
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadDicom(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file was uploaded");

            try
            {
                // Determine if it's a zip file or direct DICOM
                if (Path.GetExtension(file.FileName).ToLower() == ".zip")
                {
                    // Handle zip file with multiple DICOM files
                    return await ProcessZipFile(file);
                }
                else
                {
                    // Handle individual DICOM file
                    return await ProcessDicomFile(file);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing DICOM upload");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpPost("upload/batch")]
        public async Task<IActionResult> UploadMultipleDicom(List<IFormFile> files)
        {
            if (files == null || files.Count == 0)
                return BadRequest("No files were uploaded");

            var results = new List<DicomFileResult>();

            foreach (var file in files)
            {
                try
                {
                    var result = await ProcessSingleDicomFile(file);
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error processing file {file.FileName}");
                    results.Add(new DicomFileResult
                    {
                        Filename = file.FileName,
                        Success = false,
                        Error = ex.Message
                    });
                }
            }

            return Ok(results);
        }

        private async Task<IActionResult> ProcessDicomFile(IFormFile file)
        {
            var result = await ProcessSingleDicomFile(file);

            if (result.Success)
                return Ok(result);
            else
                return BadRequest(result);
        }

        private async Task<DicomFileResult> ProcessSingleDicomFile(IFormFile file)
        {
            var result = new DicomFileResult
            {
                Filename = file.FileName,
                Success = false
            };

            try
            {
                // Create a temporary memory stream to hold the file
                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                // Load the DICOM file to validate and extract metadata
                DicomFile dicomFile;
                try
                {
                    dicomFile = await DicomFile.OpenAsync(memoryStream);
                }
                catch (Exception ex)
                {
                    result.Error = $"Not a valid DICOM file: {ex.Message}";
                    return result;
                }

                // Extract DICOM metadata
                var dataset = dicomFile.Dataset;

                // Get key DICOM identifiers to create a structured blob path
                var patientId = dataset.GetString(DicomTag.PatientID) ?? "unknown_patient";
                var studyInstanceUid = dataset.GetString(DicomTag.StudyInstanceUID) ?? Guid.NewGuid().ToString();
                var seriesInstanceUid = dataset.GetString(DicomTag.SeriesInstanceUID) ?? Guid.NewGuid().ToString();
                var sopInstanceUid = dataset.GetString(DicomTag.SOPInstanceUID) ?? Guid.NewGuid().ToString();

                // Get additional metadata for more user-friendly paths
                var patientName = dataset.GetString(DicomTag.PatientName) ?? "Unknown";
                var studyDescription = dataset.GetString(DicomTag.StudyDescription) ?? "No_Description";
                var seriesDescription = dataset.GetString(DicomTag.SeriesDescription) ?? "No_Description";
                var modality = dataset.GetString(DicomTag.Modality) ?? "Unknown";
                var studyDate = dataset.GetString(DicomTag.StudyDate) ?? "00000000";

                // Create DICOM date in friendly format (YYYYMMDD to YYYY-MM-DD)
                var friendlyDate = studyDate.Length == 8 ?
                    $"{studyDate.Substring(0, 4)}-{studyDate.Substring(4, 2)}-{studyDate.Substring(6, 2)}" :
                    "Unknown-Date";

                // Format patient name for path (Last^First to Last_First)
                var formattedPatientName = patientName.Replace('^', '_').Replace(' ', '_');

                // Create a structured path for the blob that's human-readable
                var blobPath = $"Patient_{SafePath(formattedPatientName)}_{SafePath(patientId)}/" +
                               $"{friendlyDate}_{SafePath(studyDescription)}/" +
                               $"{modality}_{SafePath(seriesDescription)}/" +
                               $"{Path.GetFileNameWithoutExtension(file.FileName) ?? sopInstanceUid}.dcm";

                _logger.LogInformation(blobPath);

                //// Upload to Azure Blob Storage
                //var blobClient = new BlobContainerClient(_connectionString, _containerName);

                //// Create the container if it doesn't exist
                //await blobClient.CreateIfNotExistsAsync();

                //// Get a reference to the blob
                //var blob = blobClient.GetBlobClient(blobPath);

                //// Reset the stream position for upload
                //memoryStream.Position = 0;

                //// Upload the DICOM file to Azure
                //await blob.UploadAsync(memoryStream, new BlobHttpHeaders { ContentType = "application/dicom" });

                // Populate result information
                result.Success = true;
                //result.BlobUrl = blob.Uri.ToString();
                result.Metadata = new DicomMetadata
                {
                    PatientId = patientId,
                    PatientName = dataset.GetString(DicomTag.PatientName),
                    StudyInstanceUid = studyInstanceUid,
                    SeriesInstanceUid = seriesInstanceUid,
                    SopInstanceUid = sopInstanceUid,
                    Modality = dataset.GetString(DicomTag.Modality),
                    StudyDate = dataset.GetString(DicomTag.StudyDate)
                };

                return result;
            }
            catch (Exception ex)
            {
                result.Error = $"Error processing DICOM file: {ex.Message}";
                return result;
            }
        }

        private async Task<IActionResult> ProcessZipFile(IFormFile file)
        {
            try
            {
                // Initialize blob client
                //var blobClient = new BlobContainerClient(_connectionString, _containerName);
                //await blobClient.CreateIfNotExistsAsync();

                var dicomResults = new List<DicomFileResult>();
                int totalEntries = 0;
                int processedEntries = 0;

                // Create a stream for reading the zip file
                using (var zipStream = file.OpenReadStream())
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
                {
                    totalEntries = archive.Entries.Count;
                    _logger.LogInformation($"Processing ZIP file with {totalEntries} entries");

                    // Process each entry one at a time
                    foreach (var entry in archive.Entries)
                    {
                        processedEntries++;

                        // Skip directories
                        if (string.IsNullOrEmpty(entry.Name) || entry.Name.EndsWith("/") || entry.Name.EndsWith("\\"))
                            continue;

                        try
                        {
                            _logger.LogDebug($"Processing entry {processedEntries}/{totalEntries}: {entry.FullName}");

                            // Process only files of reasonable size to avoid memory issues
                            if (entry.Length > 100 * 1024 * 1024) // 100 MB limit per file
                            {
                                dicomResults.Add(new DicomFileResult
                                {
                                    Filename = entry.FullName,
                                    Success = false,
                                    Error = "File too large (exceeds 100 MB)"
                                });
                                continue;
                            }

                            // Create a buffer just large enough for this specific entry
                            using var entryStream = entry.Open();

                            // First try to validate it's a DICOM file and extract metadata
                            DicomFile dicomFile;
                            DicomDataset dataset;

                            try
                            {
                                // Create a memory stream just for DICOM header validation
                                // We'll read the stream again for upload to avoid keeping full file in memory
                                using var memoryStreamForValidation = new MemoryStream();

                                // Copy the entry contents for validation
                                await entryStream.CopyToAsync(memoryStreamForValidation);
                                memoryStreamForValidation.Position = 0;

                                // Try to open as DICOM and read metadata
                                dicomFile = await DicomFile.OpenAsync(memoryStreamForValidation);
                                dataset = dicomFile.Dataset;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogInformation($"Skipping non-DICOM file: {entry.FullName}, Error: {ex.Message}");
                                // Skip non-DICOM files
                                continue;
                            }

                            // Get key DICOM identifiers
                            var patientId = dataset.GetString(DicomTag.PatientID) ?? "unknown_patient";
                            var studyInstanceUid = dataset.GetString(DicomTag.StudyInstanceUID) ?? Guid.NewGuid().ToString();
                            var seriesInstanceUid = dataset.GetString(DicomTag.SeriesInstanceUID) ?? Guid.NewGuid().ToString();
                            var sopInstanceUid = dataset.GetString(DicomTag.SOPInstanceUID) ?? Guid.NewGuid().ToString();

                            // Get additional metadata for more user-friendly paths
                            var patientName = dataset.GetString(DicomTag.PatientName) ?? "Unknown";
                            var studyDescription = dataset.GetString(DicomTag.StudyDescription) ?? "No_Description";
                            var seriesDescription = dataset.GetString(DicomTag.SeriesDescription) ?? "No_Description";
                            var modality = dataset.GetString(DicomTag.Modality) ?? "Unknown";
                            var studyDate = dataset.GetString(DicomTag.StudyDate) ?? "00000000";

                            // Create DICOM date in friendly format (YYYYMMDD to YYYY-MM-DD)
                            var friendlyDate = studyDate.Length == 8 ?
                                $"{studyDate.Substring(0, 4)}-{studyDate.Substring(4, 2)}-{studyDate.Substring(6, 2)}" :
                                "Unknown-Date";

                            // Format patient name for path (Last^First to Last_First)
                            var formattedPatientName = patientName.Replace('^', '_').Replace(' ', '_');

                            // Get original filename without extension
                            var originalFilename = Path.GetFileNameWithoutExtension(entry.Name);

                            // Create a structured path for the blob that's human-readable
                            var blobPath = $"Patient_{SafePath(formattedPatientName)}_{SafePath(patientId)}/" +
                                           $"{friendlyDate}_{SafePath(studyDescription)}/" +
                                           $"{modality}_{SafePath(seriesDescription)}/" +
                                           $"{originalFilename ?? sopInstanceUid}.dcm";

                            //// Get a reference to the blob
                            //var blob = blobClient.GetBlobClient(blobPath);

                            //// Reopen the entry stream for upload
                            //using var uploadStream = entry.Open();

                            //// Upload the DICOM file to Azure - streaming directly from zip entry to blob storage
                            //await blob.UploadAsync(uploadStream, new BlobHttpHeaders { ContentType = "application/dicom" });

                            // Add to results
                            dicomResults.Add(new DicomFileResult
                            {
                                Filename = entry.FullName,
                                Success = true,
                                //BlobUrl = blob.Uri.ToString(),
                                Metadata = new DicomMetadata
                                {
                                    PatientId = patientId,
                                    PatientName = patientName,
                                    StudyInstanceUid = studyInstanceUid,
                                    SeriesInstanceUid = seriesInstanceUid,
                                    SopInstanceUid = sopInstanceUid,
                                    Modality = modality,
                                    StudyDate = studyDate
                                }
                            });

                            _logger.LogDebug($"Successfully processed and uploaded: {entry.FullName}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error processing ZIP entry: {entry.FullName}");
                            dicomResults.Add(new DicomFileResult
                            {
                                Filename = entry.FullName,
                                Success = false,
                                Error = ex.Message
                            });
                        }
                    }
                }

                _logger.LogInformation($"ZIP processing complete. Processed {dicomResults.Count}/{totalEntries} entries.");

                return Ok(new
                {
                    ZipFileName = file.FileName,
                    TotalEntries = totalEntries,
                    ProcessedDicomFiles = dicomResults.Count,
                    SuccessfulFiles = dicomResults.Count(r => r.Success),
                    FailedFiles = dicomResults.Count(r => !r.Success),
                    Results = dicomResults
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing ZIP file");
                return StatusCode(500, $"Error processing zip file: {ex.Message}");
            }
        }

        // Helper method to ensure path components are safe for blob storage
        private string SafePath(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "Unknown";

            // Remove invalid characters
            var sanitized = string.Join("_", input.Split(Path.GetInvalidFileNameChars()));

            // Limit length for readability
            if (sanitized.Length > 50)
                sanitized = sanitized.Substring(0, 47) + "...";

            // Replace consecutive underscores with single one
            while (sanitized.Contains("__"))
                sanitized = sanitized.Replace("__", "_");

            // Trim leading/trailing underscores
            return sanitized.Trim('_');
        }
    }

    public class DicomFileResult
    {
        public string Filename { get; set; }
        public bool Success { get; set; }
        public string BlobUrl { get; set; }
        public string Error { get; set; }
        public DicomMetadata Metadata { get; set; }
    }

    public class DicomMetadata
    {
        public string PatientId { get; set; }
        public string PatientName { get; set; }
        public string StudyInstanceUid { get; set; }
        public string SeriesInstanceUid { get; set; }
        public string SopInstanceUid { get; set; }
        public string Modality { get; set; }
        public string StudyDate { get; set; }
    }
}
