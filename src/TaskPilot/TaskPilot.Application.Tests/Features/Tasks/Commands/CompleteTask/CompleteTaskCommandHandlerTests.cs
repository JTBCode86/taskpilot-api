// Localização: TaskPilot.Application.Tests/Features/Tasks/Commands/CompleteTask/CompleteTaskCommandHandlerTests.cs

using Moq;
using Xunit;
using TaskPilot.Domain.Interfaces;
using TaskPilot.Domain.Exceptions;
using TaskPilot.Application.Features.Tasks.Commands.CompleteTask;
using System;
using DomainTask = TaskPilot.Domain.Entities.Task;
using System.Threading.Tasks;
using System.Threading;

public class CompleteTaskCommandHandlerTests
{
    // A constante de status é crucial para o teste de sucesso.
    private const int CompletedStatusId = 3;

    [Fact]
    public async Task Handle_ShouldUpdateTaskStatusToCompleted_WhenTaskExists()
    {
        // ARRANGE
        var taskIdToComplete = 5;
        var mockRepo = new Mock<ITaskRepository>();

        // 1. Simular a tarefa ANTES da conclusão (StatusId = 1 para Pendente)
        var taskToUpdate = new DomainTask
        {
            Id = taskIdToComplete,
            StatusId = 1, // Pendente
            CompletedAt = null // Deve ser nulo antes
        };

        // Setup 1: Configurar GetByIdAsync para retornar a tarefa simulada
        mockRepo.Setup(r => r.GetByIdAsync(taskIdToComplete))
                .ReturnsAsync(taskToUpdate);

        // Setup 2: Configurar UpdateAsync para apenas retornar Task.CompletedTask 
        // (Garantir que a chamada de UpdateAsync não cause erros)
        mockRepo.Setup(r => r.UpdateAsync(It.IsAny<DomainTask>()))
                .Returns(System.Threading.Tasks.Task.CompletedTask);

        var handler = new CompleteTaskCommandHandler(mockRepo.Object);
        var command = new CompleteTaskCommand { TaskId = taskIdToComplete };

        // ACT
        var resultId = await handler.Handle(command, CancellationToken.None);

        // ASSERT

        // 1. Verifica se o ID retornado é o ID da tarefa
        Assert.Equal(taskIdToComplete, resultId);

        // 2. Verifica se o status e a data foram atualizados no objeto de domínio
        Assert.Equal(CompletedStatusId, taskToUpdate.StatusId);
        Assert.NotNull(taskToUpdate.CompletedAt);
        
        // Garante que a data de conclusão está próxima do tempo de execução do teste
        Assert.True(taskToUpdate.CompletedAt.Value > DateTime.Now.AddMinutes(-1));

        // 3. Verifica se o método de atualização do repositório foi CHAMADO EXATAMENTE UMA VEZ
        mockRepo.Verify(r => r.UpdateAsync(taskToUpdate), Times.Once);
    }

    // Seu método de teste de falha existente:
    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenTaskDoesNotExist()
    {
        // ARRANGE
        var mockRepo = new Mock<ITaskRepository>();

        // Setup: GetByIdAsync deve retornar NULL
        mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((DomainTask)null);

        var handler = new CompleteTaskCommandHandler(mockRepo.Object);
        var command = new CompleteTaskCommand { TaskId = 999 };

        // ACT & ASSERT
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldNotCompleteAnotherUsersTask()
    {
        var repository = new Mock<ITaskRepository>();
        repository.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(new DomainTask { Id = 5, UserId = 1 });
        var handler = new CompleteTaskCommandHandler(repository.Object);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new CompleteTaskCommand { TaskId = 5, UserId = 2 }, CancellationToken.None));
        repository.Verify(r => r.UpdateAsync(It.IsAny<DomainTask>()), Times.Never);
    }
}