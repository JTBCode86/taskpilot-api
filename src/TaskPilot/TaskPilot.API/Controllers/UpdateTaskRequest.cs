using System.ComponentModel.DataAnnotations;

namespace TaskPilot.API.Controllers
{
    /// <summary>Dados editáveis de uma tarefa.</summary>
    public sealed record UpdateTaskRequest
    {
        [Required(ErrorMessage = "O título da tarefa é obrigatório.")]
        [StringLength(256, ErrorMessage = "O título deve ter no máximo 256 caracteres.")]
        public string Title { get; init; }

        public string Description { get; init; }

        [Range(1, 3)]
        public int PriorityLevel { get; init; }
    }

    /// <summary>Identificador da tarefa atualizada.</summary>
    public sealed record UpdatedTaskResponse(int Id);
}
