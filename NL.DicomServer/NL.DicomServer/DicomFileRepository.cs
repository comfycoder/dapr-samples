using FellowOakDicom;

public class DicomFileRepository
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DicomFileRepository> _logger;
    private readonly BlobStorageService _blobStorageService;
    private readonly string _customerId;

    public DicomFileRepository(
        IConfiguration configuration,
        ILogger<DicomFileRepository> logger,
        BlobStorageService blobStorageService)
    {
        _configuration = configuration;
        _logger = logger;
        _blobStorageService = blobStorageService;
        _customerId = configuration["Customer:Id"];
    }

    public async Task<string> StoreFileAsync(DicomFile dicomFile)
    {
        try
        {
            // Get relevant DICOM tags for path construction
            var dataset = dicomFile.Dataset;
            string patientId = dataset.GetSingleValueOrDefault(DicomTag.PatientID, "unknown");
            string studyInstanceUid = dataset.GetSingleValueOrDefault(DicomTag.StudyInstanceUID, "unknown");
            string seriesInstanceUid = dataset.GetSingleValueOrDefault(DicomTag.SeriesInstanceUID, "unknown");
            string sopInstanceUid = dataset.GetSingleValueOrDefault(DicomTag.SOPInstanceUID, "unknown");
            string modality = dataset.GetSingleValueOrDefault(DicomTag.Modality, "unknown");

            // Create logical folder structure: customer/patientId/studyInstanceUid/modality_seriesInstanceUid/
            string blobPath = $"{_customerId}/{patientId}/{studyInstanceUid}/{modality}_{seriesInstanceUid}/{sopInstanceUid}.dcm";

            // Save DICOM file to memory stream
            using var memoryStream = new MemoryStream();
            await dicomFile.SaveAsync(memoryStream);
            memoryStream.Position = 0;

            // Upload to blob storage using the constructed path
            await _blobStorageService.UploadBlobAsync(blobPath, memoryStream);

            _logger.LogInformation($"DICOM file stored at {blobPath}");
            return blobPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing DICOM file");
            throw;
        }
    }

    public string GetStoragePath(DicomFile dicomFile)
    {
        var dataset = dicomFile.Dataset;
        string patientId = dataset.GetSingleValueOrDefault(DicomTag.PatientID, "unknown");
        string studyInstanceUid = dataset.GetSingleValueOrDefault(DicomTag.StudyInstanceUID, "unknown");
        string seriesInstanceUid = dataset.GetSingleValueOrDefault(DicomTag.SeriesInstanceUID, "unknown");
        string sopInstanceUid = dataset.GetSingleValueOrDefault(DicomTag.SOPInstanceUID, "unknown");
        string modality = dataset.GetSingleValueOrDefault(DicomTag.Modality, "unknown");

        return $"{_customerId}/{patientId}/{studyInstanceUid}/{modality}_{seriesInstanceUid}/{sopInstanceUid}.dcm";
    }
}
