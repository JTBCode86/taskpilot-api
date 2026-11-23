using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TaskPilot.API.Controllers
{
    /// <summary>Dados editáveis de uma tarefa.</summary>
    public sealed record UpdateTaskRequest
    {
        private DateTime? _dueDate;

        [Required(ErrorMessage = "O título da tarefa é obrigatório.")]
        [StringLength(256, ErrorMessage = "O título deve ter no máximo 256 caracteres.")]
        public string Title { get; init; }

        public string Description { get; init; }

        [Range(1, 3)]
        public int PriorityLevel { get; init; }

        public DateTime? DueDate
        {
            get => _dueDate;
            init { _dueDate = value; HasDueDate = true; }
        }

        [JsonIgnore]
        public bool HasDueDate { get; private set; }
    }

    /// <summary>Identificador da tarefa atualizada.</summary>
    public sealed record UpdatedTaskResponse(int Id);
}
