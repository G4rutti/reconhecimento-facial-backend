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
        private readonly IFaceService _faceService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IUserService userService, IFaceService faceService, ILogger<AuthController> logger)
        {
            _userService = userService;
            _faceService = faceService;
            _logger = logger;
        }

        /// <summary>
        /// Cadastra um novo usuário com reconhecimento facial.
        /// Aceita múltiplas imagens faciais para melhor precisão (multi-embedding).
        /// </summary>
        /// <param name="request">Dados do registro (nome e imagens base64).</param>
        /// <returns>Dados do usuário cadastrado.</returns>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                _logger.LogInformation("Requisição de registro recebida para: {Name}", request.Name);

                // Usar múltiplas imagens se disponíveis, senão fallback para imagem única
                var images = request.ImagesBase64;
                if ((images == null || images.Count == 0) && !string.IsNullOrEmpty(request.ImageBase64))
                {
                    images = new List<string> { request.ImageBase64 };
                }

                if (images == null || images.Count == 0)
                {
                    return BadRequest(new { error = "Envie pelo menos uma imagem facial." });
                }

                var user = await _userService.RegisterAsync(request.Name, images);

                return Ok(new
                {
                    message = "Usuário cadastrado com sucesso!",
                    userId = user.Id,
                    name = user.Name,
                    embeddingsCount = images.Count
                });
            }
            catch (ArgumentException ex)
            {
                // Nenhum rosto detectado, mais de um rosto, ou qualidade insuficiente
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
        /// Inclui verificação de anti-spoofing e rate limiting.
        /// </summary>
        /// <param name="request">Dados da autenticação (imagem base64).</param>
        /// <returns>Resultado da autenticação com confiança, liveness e nome do usuário.</returns>
        [HttpPost("authenticate")]
        public async Task<IActionResult> Authenticate([FromBody] AuthenticateRequest request)
        {
            try
            {
                _logger.LogInformation("Requisição de autenticação recebida.");

                var result = await _userService.AuthenticateAsync(request.ImageBase64);

                // Rate limiting: bloqueado
                if (result.IsBlocked)
                {
                    return StatusCode(429, new
                    {
                        success = false,
                        isBlocked = true,
                        blockedSecondsRemaining = result.BlockedSecondsRemaining,
                        remainingAttempts = 0,
                        message = $"Muitas tentativas falhas. Tente novamente em {result.BlockedSecondsRemaining}s."
                    });
                }

                if (!result.Success)
                {
                    _logger.LogWarning("Autenticação falhou. Confiança: {Confidence}%, Liveness: {Liveness}%",
                        result.Confidence, result.LivenessScore);

                    return Unauthorized(new
                    {
                        success = false,
                        confidence = result.Confidence,
                        userName = (string?)null,
                        livenessScore = result.LivenessScore,
                        remainingAttempts = result.RemainingAttempts,
                        message = result.LivenessScore < 35
                            ? "Possível fraude detectada. Use seu rosto real."
                            : "Usuário não reconhecido."
                    });
                }

                return Ok(new
                {
                    success = true,
                    confidence = result.Confidence,
                    userName = result.UserName,
                    livenessScore = result.LivenessScore,
                    remainingAttempts = result.RemainingAttempts,
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

        /// <summary>
        /// Valida a qualidade de uma imagem facial sem processar o registro/autenticação.
        /// Útil para feedback em tempo real no frontend.
        /// </summary>
        /// <param name="request">Imagem em base64.</param>
        /// <returns>Resultado da validação de qualidade.</returns>
        [HttpPost("validate-image")]
        public IActionResult ValidateImage([FromBody] AuthenticateRequest request)
        {
            try
            {
                _logger.LogInformation("Validação de qualidade de imagem solicitada.");

                var result = _faceService.ValidateImageQuality(request.ImageBase64);

                return Ok(new
                {
                    isAcceptable = result.IsAcceptable,
                    blurScore = result.BlurScore,
                    brightnessScore = result.BrightnessScore,
                    faceSizePercent = result.FaceSizePercent,
                    warnings = result.Warnings
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro na validação de imagem.");
                return StatusCode(500, new { error = "Erro ao validar imagem.", details = ex.Message });
            }
        }

        /// <summary>
        /// Retorna os logs de acesso com paginação e filtro opcional.
        /// Para auditoria de segurança.
        /// </summary>
        /// <param name="page">Página (padrão: 1).</param>
        /// <param name="pageSize">Itens por página (padrão: 20).</param>
        /// <param name="success">Filtro por sucesso (opcional).</param>
        /// <returns>Lista paginada de logs de acesso.</returns>
        [HttpGet("logs")]
        public async Task<IActionResult> GetLogs(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] bool? success = null)
        {
            try
            {
                var (logs, totalCount) = await _userService.GetAccessLogsAsync(page, pageSize, success);

                return Ok(new
                {
                    logs = logs.Select(l => new
                    {
                        id = l.Id,
                        userId = l.UserId,
                        userName = l.User?.Name,
                        timestamp = l.Timestamp,
                        success = l.Success,
                        confidence = l.Confidence
                    }),
                    totalCount,
                    page,
                    pageSize,
                    totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar logs de acesso.");
                return StatusCode(500, new { error = "Erro ao buscar logs.", details = ex.Message });
            }
        }
    }
}
