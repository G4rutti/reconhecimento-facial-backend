using System.ComponentModel.DataAnnotations;

namespace FaceAuth.API.Application.DTOs
{
    /// <summary>
    /// DTO para requisição de cadastro de usuário.
    /// </summary>
    public class RegisterRequest
    {
        /// <summary>
        /// Nome do usuário a ser cadastrado.
        /// </summary>
        [Required(ErrorMessage = "O nome é obrigatório.")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Imagem facial codificada em base64.
        /// </summary>
        [Required(ErrorMessage = "A imagem em base64 é obrigatória.")]
        public string ImageBase64 { get; set; } = string.Empty;
    }
}
