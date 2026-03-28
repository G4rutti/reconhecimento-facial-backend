namespace FaceAuth.API.Domain.Entities
{
    /// <summary>
    /// Entidade que representa um log de tentativa de acesso ao sistema.
    /// </summary>
    public class AccessLog
    {
        /// <summary>
        /// Identificador único do log.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Id do usuário identificado (null se não reconhecido).
        /// </summary>
        public int? UserId { get; set; }

        /// <summary>
        /// Data e hora da tentativa de acesso.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Indica se a autenticação foi bem-sucedida.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Nível de confiança da autenticação (0-100%).
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Navegação para o usuário associado.
        /// </summary>
        public User? User { get; set; }
    }
}
