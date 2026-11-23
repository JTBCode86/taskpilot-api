using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using TaskPilot.Domain.Interfaces;
using TaskPilot.Domain.Entities;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;
using TaskPilot.Domain.Enums;

namespace TaskPilot.Infrastructure.Data.Repositories
{
    public class TaskRepository : ITaskRepository
    {
        private readonly ApplicationDbContext _context;

        public TaskRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        // Exemplo: Implementação AddAsync usando a Stored Procedure
        public async Task<int> AddAsync(Domain.Entities.Task task)
        {
            // O ID retornado da Stored Procedure
            int newTaskId = 0;

            // Obtém a conexão de banco de dados do DbContext
            var connection = _context.Database.GetDbConnection();

            try
            {
                await connection.OpenAsync();

                // Cria o comando
                using (var command = connection.CreateCommand())
                {
                    // O nome da sua Stored Procedure
                    command.CommandText = "SpCreateNewTask";
                    command.CommandType = System.Data.CommandType.StoredProcedure;

                    // 1. Adicionar Parâmetros de Entrada
                    // NOTE: Use a sintaxe correta para o tipo de provedor (SqlParameter para SQL Server)
                    command.Parameters.Add(new SqlParameter("@UserId", task.UserId));
                    command.Parameters.Add(new SqlParameter("@Title", task.Title));
                    command.Parameters.Add(new SqlParameter("@Description", (object)task.Description ?? DBNull.Value));
                    command.Parameters.Add(new SqlParameter("@PriorityLevel", (int)task.PriorityLevel));
                    command.Parameters.Add(new SqlParameter("@EstimatedDuration", (object)task.EstimatedDuration ?? DBNull.Value));
                    command.Parameters.Add(new SqlParameter("@DueDate", (object)task.DueDate ?? DBNull.Value));

                    // 2. Executar e Ler o Resultado
                    // Assumimos que a SP usa SELECT SCOPE_IDENTITY() AS NewTaskId; para retornar o ID.
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            // Lendo o resultado que foi mapeado para 'NewTaskId' na SP
                            // Deve ser o índice 0, que é a primeira coluna do SELECT da SP.
                            //newTaskId = reader.GetInt32(0);
                            newTaskId = (int)reader.GetDecimal(0);
                        }
                        else
                        {
                            throw new InvalidOperationException("Stored Procedure 'SpCreateNewTask' não retornou um ID de tarefa.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Logar o erro aqui
                Console.WriteLine($"Erro ao executar Stored Procedure: {ex.Message}");
                throw;
            }
            finally
            {
                // Garante que a conexão seja fechada se o DbContext não for o proprietário dela
                if (connection.State == System.Data.ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }

            return newTaskId;
        }

        public async Task<Domain.Entities.Task> GetByIdAsync(int id)
        {
            return await _context.Tasks
                .Include(t => t.Status)
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        // Exemplo de uso da Stored Procedure para obter tarefas atrasadas
        public async Task<IEnumerable<Domain.Entities.Task>> GetOverdueTasksByUserIdAsync(int userId)
        {
            var userIdParam = new SqlParameter("@UserId", userId);

            // Mapeia o resultado da SP para a Entidade Task
            return await _context.Tasks
                .FromSqlRaw("EXEC dbo.SpGetOverdueTasksByUserId @UserId", userIdParam)
                .ToListAsync();
        }

        // Implementações omitidas para brevidade: UpdateAsync e DeleteAsync
        public System.Threading.Tasks.Task UpdateAsync(Domain.Entities.Task entity)
        {
            if (_context.Entry(entity).State == EntityState.Detached)
                _context.Tasks.Update(entity);
            return _context.SaveChangesAsync();
        }

        public System.Threading.Tasks.Task DeleteAsync(int id)
        {
            // ... busca e remoção
            throw new NotImplementedException();
        }

        public async Task<bool> UpdateDetailsAsync(int id, int userId, string title, string description, PriorityLevel priority, CancellationToken cancellationToken)
        {
            var affected = await _context.Tasks
                .Where(task => task.Id == id && task.UserId == userId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(task => task.Title, title)
                    .SetProperty(task => task.Description, description)
                    .SetProperty(task => task.PriorityLevel, priority), cancellationToken);
            return affected == 1;
        }

        public async Task<bool> DeleteOwnedAsync(int id, int userId, CancellationToken cancellationToken)
        {
            var affected = await _context.Tasks
                .Where(task => task.Id == id && task.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);
            return affected == 1;
        }

        public async Task<IEnumerable<Domain.Entities.Task>> GetSummaryTasksByUserIdAsync(int userId)
        {
            // Esta implementação é complexa, pois deve carregar dados da View (vw_UserTaskSummary) 
            // ou fazer um JOIN complexo com todas as navegações (Status, Categories)

            // Vamos usar a abordagem de mapeamento do EF Core (FromSqlRaw seria mais limpo, 
            // mas requer a configuração de um tipo Keyless na Infrastructure)

            // Exemplo de implementação que carrega a entidade completa com navegação:
            return await _context.Tasks
              .Include(t => t.User)
              .Include(t => t.Status)
              .Include(t => t.TaskCategoryMaps) // Inclui o mapeamento M:N
              .ThenInclude(tcm => tcm.Category) // Inclui a Categoria dentro do mapeamento
              .Where(t => t.UserId == userId)
              .ToListAsync();

            /*
                NOTA: Se você deseja usar a VIEW 'vw_UserTaskSummary' do T-SQL, o EF Core 
                requer que você mapeie um tipo de retorno *Keyless* (TaskSummaryDto) no DbContext 
                da Infrastructure e chame `_context.Set<TaskSummaryDto>().FromSqlRaw(...)`. 
                A implementação acima é a forma padrão do EF Core.
        */
        }

        public Task<List<Domain.Entities.Task>> GetSummaryAsync(int userId)
        {
            throw new NotImplementedException();
        }
    }
}