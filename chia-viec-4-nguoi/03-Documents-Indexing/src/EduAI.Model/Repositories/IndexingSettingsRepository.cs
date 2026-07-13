using EduAI.Model.Entities;
using EduAI.Model.IRepository;
using Microsoft.EntityFrameworkCore;

namespace EduAI.Model.Repositories;

public class IndexingSettingsRepository : IIndexingSettingsRepository
{
    private readonly AppDbContext _context;

    public IndexingSettingsRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IndexingSettings?> GetAsync() =>
        await _context.IndexingSettings
            .Include(s => s.UpdatedBy)
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync();

    public async Task AddAsync(IndexingSettings settings) =>
        await _context.IndexingSettings.AddAsync(settings);

    public void Update(IndexingSettings settings) =>
        _context.IndexingSettings.Update(settings);
}
