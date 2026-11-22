using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskPilot.Domain.Interfaces;
using TaskPilot.Infrastructure.Data;
using TaskPilot.Infrastructure.Data.Repositories;
using TaskPilot.Infrastructure.ExternalServices;
using TaskPilot.Application.Interfaces; // Interface do serviço externo

namespace TaskPilot.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
        {
            // 1. Configuração do DbContext
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("TaskPilotDBConnection"))
            );

            // 2. Registro dos Repositórios (Implementação de Interfaces do Domain)
            services.AddScoped<ITaskRepository, TaskRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            // services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>)); // Se implementado

            // 3. Registro dos Serviços Externos (Implementação de Interfaces do Application)
            // Lendo a chave da API do appsettings.json
            var externalApiKey = configuration["ExternalServices:NotificationApiKey"];
            services.AddSingleton<IExternalApiService>(sp => new NotificationService(externalApiKey));

            return services;
        }
    }
}