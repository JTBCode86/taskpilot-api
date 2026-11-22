// TaskPilot.API/Controllers/TasksController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using TaskPilot.Application.Features.Tasks.Commands.CreateTask;
using TaskPilot.Application.Features.Tasks.Commands.CompleteTask;
using TaskPilot.Application.Features.Tasks.Queries.GetTaskSummary;
using TaskPilot.Domain.Exceptions;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;

namespace TaskPilot.API.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/v1/[controller]")]
    public class TasksController : ControllerBase
    {
        // Injeção de dependência dos Handlers da camada Application
        private readonly CreateTaskCommandHandler _createTaskHandler;
        private readonly CompleteTaskCommandHandler _completeTaskHandler;
        private readonly GetTaskSummaryQueryHandler _getSummaryHandler;

        public TasksController(
            CreateTaskCommandHandler createTaskHandler,
            CompleteTaskCommandHandler completeTaskHandler,
            GetTaskSummaryQueryHandler getSummaryHandler)
        {
            _createTaskHandler = createTaskHandler;
            _completeTaskHandler = completeTaskHandler;
            _getSummaryHandler = getSummaryHandler;
        }

        // POST api/v1/tasks
        [HttpPost]
        [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CreateTask([FromBody] CreateTaskCommand command)
        {
            var currentUserId = CurrentUserId();
            if (command.UserId != 0 && command.UserId != currentUserId)
                return Forbid();
            command.UserId = currentUserId;
            int newTaskId;
            try
            {
                newTaskId = await _createTaskHandler.Handle(command);
            }
            catch (NotFoundException ex)
            {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Usuário não encontrado",
                    detail: ex.Message);
            }

            // Retorna 201 Created com o link para o recurso
            return CreatedAtAction(nameof(GetTaskSummary), new { userId = command.UserId }, newTaskId);
        }

        // GET api/v1/tasks/summary/{userId}
        [HttpGet("summary/{userId}")]
        public async Task<ActionResult<IEnumerable<TaskSummaryDto>>> GetTaskSummary(int userId)
        {
            if (userId != CurrentUserId())
                return Forbid();
            var query = new GetTaskSummaryQuery { UserId = userId };

            var summary = await _getSummaryHandler.Handle(query);

            return Ok(summary); // Retorna 200 OK
        }

        // POST api/v1/tasks/{taskId}/complete
        [HttpPost("{taskId}/complete")]
        public async Task<IActionResult> CompleteTask(int taskId)
        {
            int currentUserId = CurrentUserId();

            var command = new CompleteTaskCommand
            {
                TaskId = taskId,
                UserId = currentUserId
            };

            // Tratamento de exceções específicas (DomainException)
            try
            {
                await _completeTaskHandler.Handle(command);
                return NoContent(); // Retorna 204 No Content (sucesso sem corpo de retorno)
            }
            catch (KeyNotFoundException)
            {
                return NotFound(); // 404 Not Found
            }
            catch (DomainException ex)
            {
                // Erro de regra de negócio (ex: "Tarefa bloqueada")
                return BadRequest(new { Message = ex.Message }); // 400 Bad Request
            }

        }

        private int CurrentUserId() => int.Parse(User.FindFirst("sub")!.Value);
    }
}