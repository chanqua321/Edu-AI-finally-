namespace EduAI.BusinessLogic.IService;

public interface IDocumentIndexingQueue
{
    ValueTask EnqueueAsync(int documentId, CancellationToken cancellationToken = default);
}

