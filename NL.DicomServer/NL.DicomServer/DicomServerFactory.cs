using FellowOakDicom.Network;

public class DicomServerFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DicomServerFactory> _logger;
    private DicomServer _dicomServer;

    public DicomServerFactory(IServiceProvider serviceProvider, ILogger<DicomServerFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public DicomServer CreateServer(int port, string aet)
    {
        if (_dicomServer != null)
        {
            return _dicomServer;
        }

        _dicomServer = DicomServer.Create<DicomServiceProvider>(port, options =>
        {
            options.LogDimseDatasets = false;
            options.AETitle = aet;
        });

        return _dicomServer;
    }
}
