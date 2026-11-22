using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TaskPilot.Domain.Entities;

namespace TaskPilot.Domain.Interfaces
{
    // Contrato Específico para Usuários, estendendo o repositório genérico (opcional, mas comum)
    public interface IUserRepository : IGenericRepository<User>
    {
        Task<bool> ExistsAsync(int id);
        Task<User> GetByEmailAsync(string email);
        // Métodos específicos de consulta de Usuários, ex: Task<IEnumerable<User>> GetActiveUsersAsync();
    }
}
