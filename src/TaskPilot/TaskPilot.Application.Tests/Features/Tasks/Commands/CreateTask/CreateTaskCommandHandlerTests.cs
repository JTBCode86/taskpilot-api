// Localização: Features/Tasks/Commands/CreateTask/CreateTaskCommandHandlerTests.cs
using Moq;
using System.Threading.Tasks;
using Xunit;
using TaskPilot.Domain.Interfaces;
using TaskPilot.Application.Interfaces;
using TaskPilot.Application.Features.Tasks.Commands.CreateTask;
using TaskPilot.Domain.Exceptions;
using DomainTask = TaskPilot.Domain.Entities.Task;

namespace TaskPilot.Application.Tests.Features.Tasks.Commands.CreateTask
{
    public class CreateTaskCommandHandlerTests
    {
        [Fact]
        public async System.Threading.Tasks.Task Handle_ShouldCallRepositoryAddAsyncAndReturnNewId_WhenValidCommand()
        {
            // ARRANGE
            var mockRepo = new Mock<ITaskRepository>();
            var mockUsers = new Mock<IUserRepository>();
            var mockExternalService = new Mock<IExternalApiService>();
            mockUsers.Setup(r => r.ExistsAsync(1)).ReturnsAsync(true);

            // Setup: Simular retorno do ID 10
            mockRepo.Setup(r => r.AddAsync(It.IsAny<DomainTask>()))
                    .ReturnsAsync(10);

            var handler = new CreateTaskCommandHandler(
                mockRepo.Object,
                mockUsers.Object,
                mockExternalService.Object
            );

            var command = new CreateTaskCommand { UserId = 1, Title = "Nova Tarefa Teste" };

            // ACT
            var resultId = await handler.Handle(command);

            // ASSERT
            mockRepo.Verify(r => r.AddAsync(It.IsAny<DomainTask>()), Times.Once);
            Assert.Equal(10, resultId);
        }

        [Fact]
        public async System.Threading.Tasks.Task Handle_ShouldRejectMissingUserWithoutCreatingTask()
        {
            var taskRepository = new Mock<ITaskRepository>();
            var userRepository = new Mock<IUserRepository>();
            var notificationService = new Mock<IExternalApiService>();
            userRepository.Setup(r => r.ExistsAsync(999)).ReturnsAsync(false);
            var handler = new CreateTaskCommandHandler(
                taskRepository.Object, userRepository.Object, notificationService.Object);

            var command = new CreateTaskCommand { UserId = 999, Title = "Tarefa" };
            await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command));

            taskRepository.Verify(r => r.AddAsync(It.IsAny<DomainTask>()), Times.Never);
            notificationService.VerifyNoOtherCalls();
        }
    }
}
