using System;
using System.Collections.Generic;

namespace TaskPilot.Domain.Entities
{
    public class User
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public bool IsActive { get; set; }
        public bool EmailVerified { get; set; }
        public string EmailVerificationTokenHash { get; set; }
        public DateTimeOffset? EmailVerificationExpiresAtUtc { get; set; }
        public DateTimeOffset? EmailVerificationLastSentAtUtc { get; set; }
        public DateTime CreatedAt { get; set; }

        // Propriedade de Navegação: Um Usuário pode ter muitas Tarefas
        public ICollection<Task> Tasks { get; set; } = new List<Task>();
    }
}
