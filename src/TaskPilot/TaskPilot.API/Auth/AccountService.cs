using System;
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
        Task<bool> ResetPasswordAsync(int userId, string password, CancellationToken cancellationToken);
    }

    public sealed class AccountService : IAccountService
    {
        private static readonly string DummyHash = new PasswordHasher<User>()
            .HashPassword(new User(), Guid.NewGuid().ToString());
        private readonly IUserRepository _users;
        private readonly IPasswordHasher<User> _hasher;
        private readonly TokenService _tokens;

        public AccountService(IUserRepository users, IPasswordHasher<User> hasher, TokenService tokens)
        {
            _users = users;
            _hasher = hasher;
            _tokens = tokens;
        }

        public async Task<int> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
        {
            var email = NormalizeEmail(request.Email);
            if (await _users.GetByEmailAsync(email, cancellationToken) != null)
                throw new DuplicateUserException();
            var user = new User
            {
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                Email = email,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            user.PasswordHash = _hasher.HashPassword(user, request.Password);
            return await _users.AddForAuthenticationAsync(user, cancellationToken);
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
            return _tokens.Create(user);
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
    }
}
