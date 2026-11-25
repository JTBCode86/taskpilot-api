using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using TaskPilot.Domain.Entities;
using TaskPilot.Domain.Exceptions;
using TaskPilot.Domain.Interfaces;

namespace TaskPilot.API.Auth
{
    public interface IAccountService
    {
        Task<int> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
        Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
        Task<bool> VerifyEmailAsync(string token, CancellationToken cancellationToken);
        System.Threading.Tasks.Task ResendVerificationAsync(string email, CancellationToken cancellationToken);
        Task<bool> ResetPasswordAsync(int userId, string password, CancellationToken cancellationToken);
    }

    public sealed class EmailNotVerifiedException : Exception
    {
    }

    public sealed class AccountService : IAccountService
    {
        private static readonly TimeSpan VerificationTokenLifetime = TimeSpan.FromHours(24);
        private static readonly TimeSpan VerificationResendInterval = TimeSpan.FromMinutes(1);
        private static readonly string DummyHash = new PasswordHasher<User>()
            .HashPassword(new User(), Guid.NewGuid().ToString());
        private readonly IUserRepository _users;
        private readonly IPasswordHasher<User> _hasher;
        private readonly TokenService _tokens;
        private readonly IEmailSender _emailSender;

        public AccountService(
            IUserRepository users,
            IPasswordHasher<User> hasher,
            TokenService tokens,
            IEmailSender emailSender)
        {
            _users = users;
            _hasher = hasher;
            _tokens = tokens;
            _emailSender = emailSender;
        }

        public async Task<int> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
        {
            var email = NormalizeEmail(request.Email);
            if (await _users.GetByEmailAsync(email, cancellationToken) != null)
                throw new DuplicateUserException();
            var now = DateTimeOffset.UtcNow;
            var token = GenerateVerificationToken();
            var user = new User
            {
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                Email = email,
                IsActive = true,
                EmailVerificationTokenHash = HashVerificationToken(token),
                EmailVerificationExpiresAtUtc = now.Add(VerificationTokenLifetime),
                EmailVerificationLastSentAtUtc = now,
                CreatedAt = now.UtcDateTime
            };
            user.PasswordHash = _hasher.HashPassword(user, request.Password);
            var userId = await _users.AddForAuthenticationAsync(user, cancellationToken);
            await _emailSender.SendVerificationEmailAsync(email, token, cancellationToken);
            return userId;
        }

        public async Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
        {
            var user = await _users.GetByEmailAsync(NormalizeEmail(request.Email), cancellationToken);
            var hash = user?.PasswordHash;
            if (string.IsNullOrEmpty(hash) || !Convert.TryFromBase64String(hash, new byte[hash.Length], out _))
                hash = DummyHash;
            var result = _hasher.VerifyHashedPassword(user, hash, request.Password);
            if (user == null || !user.IsActive || hash == DummyHash || result == PasswordVerificationResult.Failed)
                return null;
            if (!user.EmailVerified)
                throw new EmailNotVerifiedException();
            return _tokens.Create(user);
        }

        public async Task<bool> VerifyEmailAsync(string token, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(token))
                return false;

            return await _users.VerifyEmailTokenAsync(
                HashVerificationToken(token),
                DateTimeOffset.UtcNow,
                cancellationToken);
        }

        public async System.Threading.Tasks.Task ResendVerificationAsync(
            string email,
            CancellationToken cancellationToken)
        {
            var user = await _users.GetByEmailAsync(NormalizeEmail(email), cancellationToken);
            if (user == null || !user.IsActive || user.EmailVerified)
                return;

            var now = DateTimeOffset.UtcNow;
            var token = GenerateVerificationToken();
            var issued = await _users.TryIssueEmailVerificationTokenAsync(
                user.Id,
                HashVerificationToken(token),
                now.Add(VerificationTokenLifetime),
                now,
                now.Subtract(VerificationResendInterval),
                cancellationToken);
            if (!issued)
                return;

            await _emailSender.SendVerificationEmailAsync(user.Email, token, cancellationToken);
        }

        public async Task<bool> ResetPasswordAsync(int userId, string password, CancellationToken cancellationToken)
        {
            var user = await _users.GetForAuthenticationByIdAsync(userId, cancellationToken);
            if (user == null || !user.IsActive)
                return false;
            user.PasswordHash = _hasher.HashPassword(user, password);
            await _users.UpdateAsync(user);
            return true;
        }

        private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

        private static string GenerateVerificationToken()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static string HashVerificationToken(string token)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        }
    }
}
