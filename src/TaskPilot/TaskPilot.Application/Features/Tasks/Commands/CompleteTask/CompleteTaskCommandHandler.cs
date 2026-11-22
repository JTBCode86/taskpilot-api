// TaskPilot.Application/Features/Tasks/Commands/CompleteTask/CompleteTaskCommandHandler.cs
using TaskPilot.Domain.Exceptions;
using TaskPilot.Domain.Interfaces;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;

namespace TaskPilot.Application.Features.Tasks.Commands.CompleteTask
{
    public class CompleteTaskCommandHandler
    {
        private readonly ITaskRepository _taskRepository;

        public CompleteTaskCommandHandler(ITaskRepository taskRepository)
        {
            _taskRepository = taskRepository;
        }

        public async Task Handle(CompleteTaskCommand command)
        {
            var task = await _taskRepository.GetByIdAsync(command.TaskId);

            if (task == null || task.UserId != command.UserId)
            {
                throw new KeyNotFoundException($"Tarefa com ID {command.TaskId} não encontrada.");
            }

            // Regra de Negócio 2: Não pode completar se já estiver concluída ou bloqueada (StatusId = 4)
            if (task.StatusId == 3) // 3 = Concluída
            {
                return; // Já concluída, não faz nada.
            }
            if (task.StatusId == 4) // 4 = Bloqueada
            {
                throw new DomainException("Não é possível completar uma tarefa que está bloqueada.");
            }

            // 1. Atualizar a Entidade
            task.StatusId = 3; // Mudar para Concluída
            task.CompletedAt = DateTime.Now;

            // 2. Persistência (O TaskRepository usa a SP MarkTaskAsCompleted ou faz o UPDATE)
            await _taskRepository.UpdateAsync(task);
        }

        public async Task<int> Handle(CompleteTaskCommand command, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await Handle(command);
            }
            catch (KeyNotFoundException)
            {
                throw new NotFoundException($"Tarefa com ID {command.TaskId} não encontrada.");
            }
            return command.TaskId;
        }
    }
}