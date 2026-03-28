namespace FaceAuth.API.Application.DTOs
{
    /// <summary>
    /// DTO com o resultado da autenticação facial.
    /// </summary>
    public class AuthenticationResult
    {
        /// <summary>
        /// Indica se a autenticação foi bem-sucedida.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Nível de confiança da autenticação (0-100%).
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Nome do usuário identificado (null se não reconhecido).
        /// </summary>
        public string? UserName { get; set; }
    }
}
