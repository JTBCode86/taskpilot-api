using System;
using System.ComponentModel.DataAnnotations;

namespace TaskPilot.API.Auth
{
    /// <summary>Dados para criar uma conta.</summary>
    public sealed record RegisterRequest
    {
        [Required, StringLength(100)]
        public string FirstName { get; init; }

        [Required, StringLength(100)]
        public string LastName { get; init; }

        [Required, EmailAddress, StringLength(256)]
        public string Email { get; init; }

        [Required, StringLength(128, MinimumLength = 12)]
        public string Password { get; init; }
    }

    /// <summary>Credenciais de acesso.</summary>
    public sealed record LoginRequest
    {
        [Required, EmailAddress, StringLength(256)]
        public string Email { get; init; }

        [Required, StringLength(128, MinimumLength = 12)]
        public string Password { get; init; }
    }

    /// <summary>Identificador da conta.</summary>
    public sealed record UserResponse(int UserId);

    /// <summary>Token de acesso e seu prazo de validade.</summary>
    public sealed record TokenResponse(string AccessToken, int UserId, DateTimeOffset ExpiresAt);

    /// <summary>Informa que a conta foi criada e que uma confirmação será enviada por e-mail.</summary>
    public sealed record RegistrationAcceptedResponse(string Message);

    /// <summary>Token recebido no link de confirmação de e-mail.</summary>
    public sealed record VerifyEmailRequest
    {
        [Required, StringLength(128)]
        public string Token { get; init; }
    }

    /// <summary>Endereço que poderá receber um novo link de confirmação.</summary>
    public sealed record ResendVerificationRequest
    {
        [Required, EmailAddress, StringLength(256)]
        public string Email { get; init; }
    }
}
