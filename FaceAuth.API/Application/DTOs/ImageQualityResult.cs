namespace FaceAuth.API.Application.DTOs
{
    /// <summary>
    /// DTO com o resultado da validação de qualidade da imagem facial.
    /// </summary>
    public class ImageQualityResult
    {
        /// <summary>
        /// Score de nitidez da imagem (variância do Laplaciano). Quanto maior, mais nítida.
        /// </summary>
        public double BlurScore { get; set; }

        /// <summary>
        /// Score de brilho da imagem (0-255). Ideal entre 80-200.
        /// </summary>
        public double BrightnessScore { get; set; }

        /// <summary>
        /// Percentual da imagem ocupado pelo rosto detectado (0-100).
        /// </summary>
        public double FaceSizePercent { get; set; }

        /// <summary>
        /// Indica se a imagem passou em todos os critérios de qualidade.
        /// </summary>
        public bool IsAcceptable { get; set; }

        /// <summary>
        /// Lista de avisos sobre problemas detectados na imagem.
        /// </summary>
        public List<string> Warnings { get; set; } = new();
    }
}
