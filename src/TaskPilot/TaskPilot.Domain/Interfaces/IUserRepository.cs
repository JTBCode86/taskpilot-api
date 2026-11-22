using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using TaskPilot.Domain.Entities;

namespace TaskPilot.Domain.Interfaces
{
    // Contrato Específico para Usuários, estendendo o repositório genérico (opcional, mas comum)
    public interface IUserRepository : IGenericRepository<User>
    {
        Task<bool> ExistsAsync(int id);
        Task<User> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
        Task<User> GetForAuthenticationByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<int> AddForAuthenticationAsync(User user, CancellationToken cancellationToken = default);
        // Métodos específicos de consulta de Usuários, ex: Task<IEnumerable<User>> GetActiveUsersAsync();
    }
}
