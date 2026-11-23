// TaskPilot.Application/DependencyInjection.cs
using Microsoft.Extensions.DependencyInjection;
using TaskPilot.Application.Features.Tasks.Commands.CreateTask;
using TaskPilot.Application.Features.Tasks.Commands.CompleteTask;
using TaskPilot.Application.Features.Tasks.Commands.ManageTask;
using TaskPilot.Application.Features.Tasks.Queries.GetTaskSummary;
using System.Reflection;
using MediatR;
using TaskPilot.Application.Features.Users.Queries.GetUserById;
using TaskPilot.Application.Interfaces;

namespace TaskPilot.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            // Registra todos os Command Handlers e Query Handlers
            services.AddScoped<CreateTaskCommandHandler>();
            services.AddScoped<CompleteTaskCommandHandler>();
            services.AddScoped<ManageTaskHandler>();
            services.AddScoped<GetTaskSummaryQueryHandler>();

            //Adicionado para teste.
         //   services.AddScoped<IUserService, UserService>();

            // Opcional: Adicionar AutoMapper se estiver usando mapeamento complexo
            //services.AddAutoMapper(Assembly.GetExecutingAssembly());
            // services.AddMediatR(typeof(GetUserByIdQuery).Assembly);
            // services.AddMediatR(typeof(GetUserByIdQuery).Assembly);

            return services;
        }

        //public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        //{

        //    // Opção 2: Mais robusta, usando uma classe conhecida na Application:
        //    services.AddMediatR(typeof(GetUserByIdQuery).Assembly);
              
        //    // IMPORTANTE: Use a versão 9.0.0
        //    // Se você não conseguir instalar o MediatR, pode ser necessário 
        //    // usar o código da versão 12.x+ (que usa o novo AddMediatR)

        //    // Se o seu projeto .NET Core 3.1 só funciona com a versão 9.0.0 do MediatR,
        //    // a sintaxe é:
        //    // services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetUserByIdQuery).Assembly));
        //    // Mas para a versão 9.0.0 (antiga), o ideal é usar:
        //    services.AddMediatR(typeof(GetUserByIdQuery).Assembly);


        //    // ... Outros registros (AutoMapper, etc.) se aplicável
        //    return services;
        //}
    }
}