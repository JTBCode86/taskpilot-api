// TaskPilot.API/Startup.cs
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TaskPilot.Application;      // Para AddApplicationServices
using TaskPilot.Infrastructure;   // Para AddInfrastructureServices
using TaskPilot.API.Extensions;   // Para ExceptionMiddleware
using TaskPilot.API.Middleware;
using TaskPilot.API.Health;
using System;
using System.Linq;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TaskPilot.API.Auth;
using TaskPilot.Domain.Entities;
using TaskPilot.Domain.Interfaces;

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

            var tokens = new TokenService(Configuration["Jwt:SigningKey"]);
            services.AddSingleton(tokens);
            services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
            services.AddScoped<IAccountService, AccountService>();
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.MapInboundClaims = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = TokenService.Issuer,
                        ValidateAudience = true,
                        ValidAudience = TokenService.Audience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = tokens.SigningKey,
                        ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
                        ValidateLifetime = true,
                        RequireExpirationTime = true,
                        ClockSkew = TimeSpan.Zero
                    };
                    options.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = async context =>
                        {
                            var subject = context.Principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                            if (!int.TryParse(subject, out var userId) || userId <= 0)
                            {
                                context.Fail("Token invÃ¡lido.");
                                return;
                            }
                            var users = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
                            var user = await users.GetForAuthenticationByIdAsync(userId, context.HttpContext.RequestAborted);
                            var stamp = context.Principal.FindFirst(TokenService.PasswordStampClaim)?.Value;
                            if (!tokens.IsCurrent(user, stamp))
                                context.Fail("Token invÃ¡lido.");
                        }
                    };
                });
            services.AddAuthorization();
            services.AddRateLimiter(options => AuthRateLimitPolicies.Configure(options, Configuration));
            services.AddControllers();
            services.AddHealthChecks()
                .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "ready" });

            // Adicionar configuração do Swagger/OpenAPI (muito útil)
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "TaskPilot API", Version = "v1" });
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT"
                });
                c.OperationFilter<SwaggerAuthorizationFilter>();
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
            app.UseMiddleware<RequestLoggingMiddleware>();
            app.UseMiddleware<ExceptionMiddleware>();

            app.UseHttpsRedirection();
            app.UseRouting();
            app.UseRateLimiter();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
                {
                    Predicate = _ => false
                });
                endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
                {
                    Predicate = check => check.Tags.Contains("ready")
                });
                endpoints.MapHealthChecks("/health", new HealthCheckOptions
                {
                    Predicate = check => check.Tags.Contains("ready")
                });
            });
        }
    }
}