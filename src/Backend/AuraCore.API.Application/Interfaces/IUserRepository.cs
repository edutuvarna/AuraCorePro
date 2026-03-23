using AuraCore.API.Domain.Entities;

namespace AuraCore.API.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User> CreateAsync(User user, CancellationToken ct = default);
    Task<List<User>> GetAllPagedAsync(int page, int pageSize, CancellationToken ct = default);
}
