namespace FaceAuth.API.Domain.Entities
{
    /// <summary>
    /// Entidade que representa um usuário cadastrado no sistema de reconhecimento facial.
    /// </summary>
    public class User
    {
        /// <summary>
        /// Identificador único do usuário.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Nome do usuário.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Embedding facial serializado em JSON (vetor de 128 floats).
        /// Mantido para compatibilidade com dados existentes.
        /// </summary>
        public string Embedding { get; set; } = string.Empty;

        /// <summary>
        /// Múltiplos embeddings faciais serializados em JSON (array de vetores de 128 floats).
        /// Permite cadastro com múltiplas fotos para maior precisão.
        /// </summary>
        public string? Embeddings { get; set; }

        /// <summary>
        /// Data e hora do cadastro do usuário.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Logs de acesso associados ao usuário.
        /// </summary>
        public ICollection<AccessLog> AccessLogs { get; set; } = new List<AccessLog>();
    }
}
