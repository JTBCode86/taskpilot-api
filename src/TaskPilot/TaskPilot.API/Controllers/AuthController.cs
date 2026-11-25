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
        [ProducesResponseType(typeof(RegistrationAcceptedResponse), StatusCodes.Status202Accepted)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
        {
            try
            {
                await _accounts.RegisterAsync(request, cancellationToken);
                return Accepted(new RegistrationAcceptedResponse(
                    "Conta criada. Confira seu e-mail para confirmar o cadastro."));
            }
            catch (DuplicateUserException)
            {
                return Problem(statusCode: StatusCodes.Status409Conflict, title: "E-mail já cadastrado");
            }
            catch (EmailDeliveryException)
            {
                return Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Não foi possível enviar o e-mail de confirmação",
                    detail: "A conta foi criada. Aguarde um minuto e tente reenviar o e-mail.");
            }
        }

        [EnableRateLimiting(AuthRateLimitPolicies.Login)]
        [HttpPost("login")]
        [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var token = await _accounts.LoginAsync(request, cancellationToken);
                if (token == null)
                    return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Credenciais inválidas");
                Response.Headers.CacheControl = "no-store";
                return Ok(token);
            }
            catch (EmailNotVerifiedException)
            {
                return Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "E-mail ainda não confirmado",
                    detail: "Confirme seu endereço de e-mail antes de entrar.");
            }
        }

        [AllowAnonymous]
        [EnableRateLimiting(AuthRateLimitPolicies.VerifyEmail)]
        [HttpPost("verify-email")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> VerifyEmail(
            VerifyEmailRequest request,
            CancellationToken cancellationToken)
        {
            if (!await _accounts.VerifyEmailAsync(request.Token, cancellationToken))
            {
                return Problem(
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    title: "Link inválido ou expirado",
                    detail: "Solicite um novo link de confirmação e tente novamente.");
            }

            return NoContent();
        }

        [AllowAnonymous]
        [EnableRateLimiting(AuthRateLimitPolicies.ResendVerification)]
        [HttpPost("resend-verification")]
        [ProducesResponseType(typeof(RegistrationAcceptedResponse), StatusCodes.Status202Accepted)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> ResendVerification(
            ResendVerificationRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                await _accounts.ResendVerificationAsync(request.Email, cancellationToken);
                return Accepted(new RegistrationAcceptedResponse(
                    "Se a conta puder receber uma confirmação, enviaremos um novo link."));
            }
            catch (EmailDeliveryException)
            {
                return Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Não foi possível enviar o e-mail de confirmação",
                    detail: "Tente novamente mais tarde.");
            }
        }

        [Authorize]
        [HttpGet("me")]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
        public IActionResult Me() => Ok(new UserResponse(int.Parse(User.FindFirst("sub")!.Value)));
    }
}
