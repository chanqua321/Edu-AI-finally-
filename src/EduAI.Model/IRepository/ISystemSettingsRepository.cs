using EduAI.Model.Entities;
using Microsoft.EntityFrameworkCore;

namespace EduAI.Model.IRepository;

public interface ISystemSettingsRepository
{
    Task<SystemSettings?> GetAsync();
    Task AddAsync(SystemSettings settings);
    void Update(SystemSettings settings);
}
