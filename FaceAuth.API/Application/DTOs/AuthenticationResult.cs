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

        /// <summary>
        /// Score de liveness/anti-spoofing (0-100%). Quanto maior, mais provável ser rosto real.
        /// </summary>
        public double LivenessScore { get; set; }

        /// <summary>
        /// Indica se a tentativa foi bloqueada por excesso de tentativas falhas.
        /// </summary>
        public bool IsBlocked { get; set; }

        /// <summary>
        /// Segundos restantes de bloqueio (0 se não bloqueado).
        /// </summary>
        public int BlockedSecondsRemaining { get; set; }

        /// <summary>
        /// Tentativas restantes antes do bloqueio.
        /// </summary>
        public int RemainingAttempts { get; set; } = 5;
    }
}
