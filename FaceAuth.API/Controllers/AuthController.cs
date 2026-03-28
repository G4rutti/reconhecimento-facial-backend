using FaceAuth.API.Application.DTOs;
using FaceAuth.API.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FaceAuth.API.Controllers
{
    /// <summary>
    /// Controller responsável pelos endpoints de autenticação facial.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IUserService userService, ILogger<AuthController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        /// <summary>
        /// Cadastra um novo usuário com reconhecimento facial.
        /// Recebe o nome e uma imagem facial em base64.
        /// </summary>
        /// <param name="request">Dados do registro (nome e imagem base64).</param>
        /// <returns>Dados do usuário cadastrado.</returns>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                _logger.LogInformation("Requisição de registro recebida para: {Name}", request.Name);

                var user = await _userService.RegisterAsync(request.Name, request.ImageBase64);

                return Ok(new
                {
                    message = "Usuário cadastrado com sucesso!",
                    userId = user.Id,
                    name = user.Name
                });
            }
            catch (ArgumentException ex)
            {
                // Nenhum rosto detectado ou mais de um rosto
                _logger.LogWarning("Erro de validação no registro: {Message}", ex.Message);
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro interno no registro.");
                return StatusCode(500, new { error = "Erro interno no servidor.", details = ex.Message });
            }
        }

        /// <summary>
        /// Autentica um usuário por reconhecimento facial.
        /// Recebe uma imagem facial em base64 e compara com os embeddings cadastrados.
        /// </summary>
        /// <param name="request">Dados da autenticação (imagem base64).</param>
        /// <returns>Resultado da autenticação com confiança e nome do usuário.</returns>
        [HttpPost("authenticate")]
        public async Task<IActionResult> Authenticate([FromBody] AuthenticateRequest request)
        {
            try
            {
                _logger.LogInformation("Requisição de autenticação recebida.");

                var result = await _userService.AuthenticateAsync(request.ImageBase64);

                if (!result.Success)
                {
                    _logger.LogWarning("Autenticação falhou. Confiança: {Confidence}%", result.Confidence);
                    return Unauthorized(new
                    {
                        success = false,
                        confidence = result.Confidence,
                        userName = (string?)null,
                        message = "Usuário não reconhecido."
                    });
                }

                return Ok(new
                {
                    success = true,
                    confidence = result.Confidence,
                    userName = result.UserName,
                    message = $"Bem-vindo, {result.UserName}!"
                });
            }
            catch (ArgumentException ex)
            {
                // Nenhum rosto detectado ou mais de um rosto
                _logger.LogWarning("Erro de validação na autenticação: {Message}", ex.Message);
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro interno na autenticação.");
                return StatusCode(500, new { error = "Erro interno no servidor.", details = ex.Message });
            }
        }
    }
}
