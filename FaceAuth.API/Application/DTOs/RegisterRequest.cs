using System.ComponentModel.DataAnnotations;

namespace FaceAuth.API.Application.DTOs
{
    /// <summary>
    /// DTO para requisição de cadastro de usuário com múltiplas imagens faciais.
    /// </summary>
    public class RegisterRequest
    {
        /// <summary>
        /// Nome do usuário a ser cadastrado.
        /// </summary>
        [Required(ErrorMessage = "O nome é obrigatório.")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Lista de imagens faciais codificadas em base64 (mínimo 3: frontal, esquerda, direita).
        /// </summary>
        [Required(ErrorMessage = "As imagens em base64 são obrigatórias.")]
        [MinLength(1, ErrorMessage = "Envie pelo menos 1 imagem facial.")]
        public List<string> ImagesBase64 { get; set; } = new();

        /// <summary>
        /// (Obsoleto) Imagem facial única — mantido para compatibilidade.
        /// Se preenchido e ImagesBase64 estiver vazio, será usado como fallback.
        /// </summary>
        public string? ImageBase64 { get; set; }
    }
}
