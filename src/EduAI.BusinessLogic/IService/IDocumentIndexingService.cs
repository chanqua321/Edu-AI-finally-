namespace EduAI.BusinessLogic.IService;

public interface IDocumentIndexingService
{
    Task IndexAsync(int documentId, string? ipAddress, CancellationToken cancellationToken = default);
}

