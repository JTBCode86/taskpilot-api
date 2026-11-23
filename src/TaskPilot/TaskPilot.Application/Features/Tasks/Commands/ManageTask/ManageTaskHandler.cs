using System;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.Domain.Enums;
using TaskPilot.Domain.Interfaces;

namespace TaskPilot.Application.Features.Tasks.Commands.ManageTask
{
    public sealed class ManageTaskHandler
    {
        private readonly ITaskRepository _tasks;

        public ManageTaskHandler(ITaskRepository tasks) => _tasks = tasks;

        public Task<bool> Update(int id, int userId, string title, string description, PriorityLevel priority, bool updateDueDate, DateTime? dueDate, CancellationToken cancellationToken) =>
            _tasks.UpdateDetailsAsync(id, userId, title, description, priority, updateDueDate, dueDate, cancellationToken);

        public Task<bool> Delete(int id, int userId, CancellationToken cancellationToken) =>
            _tasks.DeleteOwnedAsync(id, userId, cancellationToken);
    }
}
