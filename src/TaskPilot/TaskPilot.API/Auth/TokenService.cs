using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TaskPilot.Domain.Entities;

namespace TaskPilot.API.Auth
{
    public sealed class TokenService
    {
        public const string Issuer = "TaskPilot";
        public const string Audience = "TaskPilot.Client";
        public const string PasswordStampClaim = "password_stamp";
        private readonly byte[] _key;

        public TokenService(string signingKey)
        {
            if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
                throw new InvalidOperationException("Jwt:SigningKey deve conter pelo menos 32 bytes.");
            _key = Encoding.UTF8.GetBytes(signingKey);
        }

        public SymmetricSecurityKey SigningKey => new SymmetricSecurityKey(_key);

        public TokenResponse Create(User user)
        {
            var expires = DateTimeOffset.UtcNow.AddMinutes(30);
            var token = new JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                claims: new[]
                {
                    new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                    new Claim(PasswordStampClaim, PasswordStamp(user.PasswordHash))
                },
                expires: expires.UtcDateTime,
                signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256));
            return new TokenResponse(new JwtSecurityTokenHandler().WriteToken(token), user.Id, expires);
        }

        public bool IsCurrent(User user, string stamp)
        {
            if (user == null || !user.IsActive || string.IsNullOrEmpty(stamp))
                return false;
            return string.Equals(PasswordStamp(user.PasswordHash), stamp, StringComparison.Ordinal);
        }

        private string PasswordStamp(string passwordHash)
        {
            using var hmac = new HMACSHA256(_key);
            return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes("TaskPilot.PasswordStamp:" + passwordHash)));
        }
    }
}
