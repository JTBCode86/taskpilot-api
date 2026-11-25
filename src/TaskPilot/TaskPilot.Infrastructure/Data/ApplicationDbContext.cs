using Microsoft.EntityFrameworkCore;
using TaskPilot.Domain.Entities;
using TaskPilot.Infrastructure.Data.Configurations;
using System.Reflection;

namespace TaskPilot.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext
    {
        // Define as coleções de entidades do Domain
        public DbSet<Domain.Entities.Task> Tasks { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<TaskStatus> TaskStatuses { get; set; }
        public DbSet<TaskCategory> TaskCategories { get; set; }
        public DbSet<TaskCategoryMap> TaskCategoryMaps { get; set; }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Aplica as configurações específicas de mapeamento (Fluido API)
            // Isso garante que o EF Core saiba sobre chaves compostas, stored procedures, etc.

            // Alternativa 1: Aplica todas as classes IEntityTypeConfiguration no assembly (Mais limpo)
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

            // Alternativa 2: Aplica manualmente as configurações
            // modelBuilder.ApplyConfiguration(new TaskConfiguration());
            // modelBuilder.ApplyConfiguration(new TaskCategoryMapConfiguration());

            modelBuilder.Entity<User>()
                .Property(user => user.EmailVerificationTokenHash)
                .HasMaxLength(64);
            modelBuilder.Entity<User>()
                .HasIndex(user => user.EmailVerificationTokenHash)
                .HasDatabaseName("UX_Users_EmailVerificationTokenHash")
                .IsUnique()
                .HasFilter("[EmailVerificationTokenHash] IS NOT NULL");

            // Garante a inserção de dados iniciais para a tabela de Status (Seed Data)
            modelBuilder.Entity<TaskStatus>().HasData(
                new TaskStatus { Id = 1, Name = "Pendente" },
                new TaskStatus { Id = 2, Name = "Em Andamento" },
                new TaskStatus { Id = 3, Name = "Concluída" },
                new TaskStatus { Id = 4, Name = "Bloqueada" }
            );

            base.OnModelCreating(modelBuilder);
        }
    }
}
