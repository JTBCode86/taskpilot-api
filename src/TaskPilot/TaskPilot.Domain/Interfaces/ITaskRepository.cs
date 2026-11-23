using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using TaskPilot.Domain.Entities;
using TaskPilot.Domain.Enums;

namespace TaskPilot.Domain.Interfaces
{
    public interface ITaskRepository
    {
        Task<Entities.Task> GetByIdAsync(int id);
        Task<int> AddAsync(Entities.Task task);
        System.Threading.Tasks.Task UpdateAsync(Entities.Task task);
        System.Threading.Tasks.Task DeleteAsync(int id);
        Task<bool> UpdateDetailsAsync(int id, int userId, string title, string description, PriorityLevel priority, bool updateDueDate, DateTime? dueDate, CancellationToken cancellationToken);
        Task<bool> DeleteOwnedAsync(int id, int userId, CancellationToken cancellationToken);

        // Consultas complexas usando Stored Procedures/Views
        Task<IEnumerable<Entities.Task>> GetOverdueTasksByUserIdAsync(int userId);

        Task<IEnumerable<Entities.Task>> GetSummaryTasksByUserIdAsync(int userId);

       // Task<List<System.Threading.Tasks.Task>> GetSummaryAsync(int userId);

        System.Threading.Tasks.Task<List<TaskPilot.Domain.Entities.Task>> GetSummaryAsync(int userId);
    }
}
