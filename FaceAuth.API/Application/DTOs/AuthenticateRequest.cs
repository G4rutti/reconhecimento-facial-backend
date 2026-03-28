using System.ComponentModel.DataAnnotations;

namespace FaceAuth.API.Application.DTOs
{
    /// <summary>
    /// DTO para requisição de autenticação facial.
    /// </summary>
    public class AuthenticateRequest
    {
        /// <summary>
        /// Imagem facial codificada em base64.
        /// </summary>
        [Required(ErrorMessage = "A imagem em base64 é obrigatória.")]
        public string ImageBase64 { get; set; } = string.Empty;
    }
}
