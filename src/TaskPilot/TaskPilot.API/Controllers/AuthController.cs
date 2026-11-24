using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TaskPilot.API.Auth;
using TaskPilot.Domain.Exceptions;

namespace TaskPilot.API.Controllers
{
    [ApiController]
    [Route("api/v1/auth")]
    public sealed class AuthController : ControllerBase
    {
        private readonly IAccountService _accounts;

        public AuthController(IAccountService accounts) => _accounts = accounts;

        [EnableRateLimiting(AuthRateLimitPolicies.Register)]
        [HttpPost("register")]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var id = await _accounts.RegisterAsync(request, cancellationToken);
                return CreatedAtAction(nameof(Me), new UserResponse(id));
            }
            catch (DuplicateUserException)
            {
                return Problem(statusCode: StatusCodes.Status409Conflict, title: "E-mail já cadastrado");
            }
        }

        [EnableRateLimiting(AuthRateLimitPolicies.Login)]
        [HttpPost("login")]
        [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
        {
            var token = await _accounts.LoginAsync(request, cancellationToken);
            if (token == null)
                return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Credenciais inválidas");
            Response.Headers.CacheControl = "no-store";
            return Ok(token);
        }

        [Authorize]
        [HttpGet("me")]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
        public IActionResult Me() => Ok(new UserResponse(int.Parse(User.FindFirst("sub")!.Value)));
    }
}
