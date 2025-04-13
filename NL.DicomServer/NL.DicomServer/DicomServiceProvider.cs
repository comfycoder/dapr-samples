using FellowOakDicom.Imaging;
using FellowOakDicom.Network;
using FellowOakDicom;

public class DicomServiceProvider : DicomService, IDicomServiceProvider, IDicomCStoreProvider, IDicomCEchoProvider
{
    private readonly ILogger<DicomServiceProvider> _logger;
    private readonly DicomFileRepository _repository;
    private readonly PacsDbContext _dbContext;

    public DicomServiceProvider(IServiceProvider serviceProvider)
    {
        var scope = serviceProvider.CreateScope();
        _logger = scope.ServiceProvider.GetRequiredService<ILogger<DicomServiceProvider>>();
        _repository = scope.ServiceProvider.GetRequiredService<DicomFileRepository>();
        _dbContext = scope.ServiceProvider.GetRequiredService<PacsDbContext>();
    }

    public Task OnReceiveAssociationRequestAsync(DicomAssociation association)
    {
        _logger.LogInformation($"Association request received from: {association.RemoteHost}");

        // Accept any association requests
        return SendAssociationAcceptAsync(association);
    }

    public Task OnReceiveAssociationReleaseRequestAsync()
    {
        _logger.LogInformation("Association release request received");
        return Task.CompletedTask;
    }

    public Task OnReceiveAssociationAbortRequestAsync(DicomAbortSource source, DicomAbortReason reason)
    {
        _logger.LogWarning($"Association abort request received from {source}, reason: {reason}");
        return Task.CompletedTask;
    }

    public Task<DicomCStoreResponse> OnCStoreRequestAsync(DicomCStoreRequest request)
    {
        return OnCStoreRequestAsync(request, null);
    }

    public async Task<DicomCStoreResponse> OnCStoreRequestAsync(DicomCStoreRequest request, DicomNActionRequest actionRequest)
    {
        _logger.LogInformation($"C-Store request received for {request.SOPInstanceUID}");

        try
        {
            DicomFile dicomFile = await request.GetDicomFileAsync();

            // Store DICOM file in repository asynchronously
            await _repository.StoreFileAsync(dicomFile);

            // Update database with file metadata
            await SaveDicomMetadataAsync(dicomFile);

            return new DicomCStoreResponse(request, DicomStatus.Success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error processing C-Store request for {request.SOPInstanceUID}");
            return new DicomCStoreResponse(request, DicomStatus.ProcessingFailure);
        }
    }

    private async Task SaveDicomMetadataAsync(DicomFile dicomFile)
    {
        var dataset = dicomFile.Dataset;

        var dicomImage = new DicomImage
        {
            SOPInstanceUID = dataset.GetSingleValueOrDefault(DicomTag.SOPInstanceUID, string.Empty),
            SOPClassUID = dataset.GetSingleValueOrDefault(DicomTag.SOPClassUID, string.Empty),
            StudyInstanceUID = dataset.GetSingleValueOrDefault(DicomTag.StudyInstanceUID, string.Empty),
            SeriesInstanceUID = dataset.GetSingleValueOrDefault(DicomTag.SeriesInstanceUID, string.Empty),
            PatientID = dataset.GetSingleValueOrDefault(DicomTag.PatientID, string.Empty),
            PatientName = dataset.GetSingleValueOrDefault(DicomTag.PatientName, string.Empty)?.ToString(),
            StudyDate = dataset.GetSingleValueOrDefault(DicomTag.StudyDate, string.Empty),
            StudyTime = dataset.GetSingleValueOrDefault(DicomTag.StudyTime, string.Empty),
            StudyDescription = dataset.GetSingleValueOrDefault(DicomTag.StudyDescription, string.Empty),
            SeriesDescription = dataset.GetSingleValueOrDefault(DicomTag.SeriesDescription, string.Empty),
            Modality = dataset.GetSingleValueOrDefault(DicomTag.Modality, string.Empty),
            StoragePath = _repository.GetStoragePath(dicomFile)
        };

        // Check if instance already exists (idempotent operation)
        var existingImage = await _dbContext.DicomImages
            .FirstOrDefaultAsync(i => i.SOPInstanceUID == dicomImage.SOPInstanceUID);

        if (existingImage == null)
        {
            await _dbContext.DicomImages.AddAsync(dicomImage);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation($"Added metadata for {dicomImage.SOPInstanceUID} to database");
        }
        else
        {
            _logger.LogInformation($"Metadata for {dicomImage.SOPInstanceUID} already exists in database");
        }
    }

    public Task OnConnectionClosedAsync(Exception exception)
    {
        if (exception != null)
        {
            _logger.LogError(exception, "Connection closed with exception");
        }
        else
        {
            _logger.LogInformation("Connection closed");
        }
        return Task.CompletedTask;
    }

    public Task<DicomCEchoResponse> OnCEchoRequestAsync(DicomCEchoRequest request)
    {
        _logger.LogInformation($"C-Echo request received from {request.RequestingAE}");
        return Task.FromResult(new DicomCEchoResponse(request, DicomStatus.Success));
    }
}
