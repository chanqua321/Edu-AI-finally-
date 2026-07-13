using System.Threading.Channels;
using EduAI.BusinessLogic.IService;
using EduAI.Model.IRepository;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EduAI.Web.Services;

public sealed class DocumentIndexingQueue : IDocumentIndexingQueue
{
    private readonly Channel<int> _channel = Channel.CreateUnbounded<int>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    public ValueTask EnqueueAsync(int documentId, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(documentId, cancellationToken);

    internal IAsyncEnumerable<int> DequeueAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}

public sealed class DocumentIndexingWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly DocumentIndexingQueue _queue;
    private readonly ILogger<DocumentIndexingWorker> _logger;

    public DocumentIndexingWorker(
        IServiceProvider serviceProvider,
        DocumentIndexingQueue queue,
        ILogger<DocumentIndexingWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RequeuePendingAsync(stoppingToken);

        await foreach (var documentId in _queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var indexing = scope.ServiceProvider.GetRequiredService<IDocumentIndexingService>();
                await indexing.IndexAsync(documentId, ipAddress: null, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Document indexing failed for DocumentId={DocumentId}", documentId);
            }
        }
    }

    private async Task RequeuePendingAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var pending = await unitOfWork.Documents.GetPendingIndexingAsync();
            foreach (var document in pending)
                await _queue.EnqueueAsync(document.Id, stoppingToken);

            if (pending.Count > 0)
                _logger.LogInformation("Re-queued {Count} document(s) pending indexing after startup.", pending.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to re-queue documents pending indexing on startup.");
        }
    }
}

