using EduAI.Model.Entities;

namespace EduAI.Model.IRepository;

public interface IIndexingSettingsRepository
{
    Task<IndexingSettings?> GetAsync();
    Task AddAsync(IndexingSettings settings);
    void Update(IndexingSettings settings);
}
