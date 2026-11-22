// TaskPilot.API/Startup.cs
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TaskPilot.Application;      // Para AddApplicationServices
using TaskPilot.Infrastructure;   // Para AddInfrastructureServices
using TaskPilot.API.Extensions;   // Para ExceptionMiddleware
using TaskPilot.API.Health;

namespace TaskPilot.API
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        // Configura os serviços (DI Container)
        public void ConfigureServices(IServiceCollection services)
        {
            // Adiciona serviços das camadas Application e Infrastructure
            services.AddApplicationServices();
            services.AddInfrastructureServices(Configuration); // Passa a configuração do BD/APIs

            services.AddControllers();
            services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

            // Adicionar configuração do Swagger/OpenAPI (muito útil)
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "TaskPilot API", Version = "v1" });
            });
        }

        // Configura o pipeline de requisições HTTP
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();

                // Habilita o Swagger (Documentação da API)
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "TaskPilot API v1"));
            }

            // MIDDLEWARE PARA TRATAMENTO GLOBAL DE EXCEÇÕES
            // Deve vir antes do UseRouting
            app.UseMiddleware<ExceptionMiddleware>();

            app.UseHttpsRedirection();
            app.UseRouting();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHealthChecks("/health");
            });
        }
    }
}