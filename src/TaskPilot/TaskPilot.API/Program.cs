// TaskPilot.API/Program.cs
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.API.Auth;

namespace TaskPilot.API
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "reset-password")
                return await ResetPasswordAsync(args);
            CreateHostBuilder(args).Build().Run();
            return 0;
        }

        private static async Task<int> ResetPasswordAsync(string[] args)
        {
            if (args.Length != 2 || !int.TryParse(args[1], out var userId) || userId <= 0)
            {
                Console.Error.WriteLine("Uso: reset-password <id-do-usuario>");
                return 1;
            }
            Console.Write("Nova senha: ");
            var password = ReadSecret();
            Console.Write("Confirme a senha: ");
            var confirmation = ReadSecret();
            if (string.IsNullOrWhiteSpace(password) || password.Length < 12 || password.Length > 128 || password != confirmation)
            {
                Console.Error.WriteLine("As senhas devem coincidir e conter de 12 a 128 caracteres.");
                return 1;
            }
            using var host = CreateHostBuilder(Array.Empty<string>()).Build();
            using var scope = host.Services.CreateScope();
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountService>();
            if (!await accounts.ResetPasswordAsync(userId, password, CancellationToken.None))
            {
                Console.Error.WriteLine("Usuário não encontrado ou inativo.");
                return 1;
            }
            Console.WriteLine("Senha atualizada. Tokens anteriores foram invalidados.");
            return 0;
        }

        private static string ReadSecret()
        {
            if (Console.IsInputRedirected)
                return Console.ReadLine();
            var value = new StringBuilder();
            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter)
                    break;
                if (key.Key == ConsoleKey.Backspace && value.Length > 0)
                    value.Length--;
                else if (!char.IsControl(key.KeyChar))
                    value.Append(key.KeyChar);
            }
            Console.WriteLine();
            return value.ToString();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}