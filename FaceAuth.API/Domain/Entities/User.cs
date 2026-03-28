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
        /// </summary>
        public string Embedding { get; set; } = string.Empty;

        /// <summary>
        /// Logs de acesso associados ao usuário.
        /// </summary>
        public ICollection<AccessLog> AccessLogs { get; set; } = new List<AccessLog>();
    }
}
