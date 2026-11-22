// TaskPilot.Application/Features/Tasks/Commands/CreateTask/CreateTaskCommand.cs
using System;
using System.ComponentModel.DataAnnotations;
using TaskPilot.Domain.Enums;

namespace TaskPilot.Application.Features.Tasks.Commands.CreateTask
{
    public class CreateTaskCommand
    {
        public int UserId { get; set; }

        [Required(ErrorMessage = "O título da tarefa é obrigatório.")]
        [StringLength(256, ErrorMessage = "O título deve ter no máximo 256 caracteres.")]
        public string Title { get; set; }
        public string Description { get; set; }
        // Recebe o enum diretamente para tipagem forte
        public PriorityLevel PriorityLevel { get; set; } = PriorityLevel.Low;
        public int? EstimatedDuration { get; set; }
        public DateTime? DueDate { get; set; }
    }
}