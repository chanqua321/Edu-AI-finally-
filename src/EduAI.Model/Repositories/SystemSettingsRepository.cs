using EduAI.Model.Entities;
using EduAI.Model.IRepository;
using Microsoft.EntityFrameworkCore;

namespace EduAI.Model.Repositories;

public class SystemSettingsRepository : ISystemSettingsRepository
{
    private readonly AppDbContext _context;

    public SystemSettingsRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<SystemSettings?> GetAsync() =>
        await _context.SystemSettings
            .Include(s => s.UpdatedBy)
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync();

    public async Task AddAsync(SystemSettings settings) =>
        await _context.SystemSettings.AddAsync(settings);

    public void Update(SystemSettings settings) =>
        _context.SystemSettings.Update(settings);
}
