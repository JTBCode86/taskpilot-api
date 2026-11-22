using Microsoft.EntityFrameworkCore;
using TaskPilot.Domain.Entities;
using TaskPilot.Domain.Interfaces;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace TaskPilot.Infrastructure.Data.Repositories
{
    // Implementa a interface IUserRepository definida no Domain
    public class UserRepository : IUserRepository
    {
        private readonly ApplicationDbContext _context;

        public UserRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        // -------------------------------------------------------------------
        // MÉTODOS ESPECÍFICOS DA IUserRepository
        // -------------------------------------------------------------------

        public Task<bool> ExistsAsync(int id)
        {
            return _context.Users.AnyAsync(user => user.Id == id);
        }

        public async Task<User> GetByEmailAsync(string email)
        {
            // Busca o usuário pelo e-mail (crucial para autenticação)
            return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        }

        // -------------------------------------------------------------------
        // MÉTODOS CRUD HERDADOS DO IGenericRepository<User>
        // -------------------------------------------------------------------

        public async Task<User> GetByIdAsync(int id)
        {
            // O Include(u => u.Tasks) é opcional, dependendo de quantos dados você precisa
            return await _context.Users
                .Include(u => u.Tasks)
                .FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<IEnumerable<User>> GetAllAsync()
        {
            return await _context.Users.ToListAsync();
        }

        public async Task<int> AddAsync(User entity)
        {
            _context.Users.Add(entity);
            await _context.SaveChangesAsync();
            return entity.Id;
        }

        public System.Threading.Tasks.Task UpdateAsync(User entity)
        {
            // O EF Core rastreia as mudanças automaticamente se a entidade for carregada,
            // mas o Update garante que ele marque a entidade como modificada.
            _context.Users.Update(entity);
            return _context.SaveChangesAsync();
        }

        public async System.Threading.Tasks.Task DeleteAsync(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
            }
            // Se o usuário não for encontrado, simplesmente retornamos sem erro ( idempotent )
        }
    }
}