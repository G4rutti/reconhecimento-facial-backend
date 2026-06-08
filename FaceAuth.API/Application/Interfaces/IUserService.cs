using FaceAuth.API.Application.DTOs;
using FaceAuth.API.Domain.Entities;

namespace FaceAuth.API.Application.Interfaces
{
    /// <summary>
    /// Interface para o serviço de gerenciamento de usuários.
    /// </summary>
    public interface IUserService
    {
        /// <summary>
        /// Registra um novo usuário com base em múltiplas imagens faciais.
        /// </summary>
        /// <param name="name">Nome do usuário.</param>
        /// <param name="base64Images">Lista de imagens faciais codificadas em base64.</param>
        /// <returns>Usuário cadastrado.</returns>
        Task<User> RegisterAsync(string name, List<string> base64Images);

        /// <summary>
        /// Registra um novo usuário com base em uma única imagem facial (compatibilidade).
        /// </summary>
        /// <param name="name">Nome do usuário.</param>
        /// <param name="base64Image">Imagem facial codificada em base64.</param>
        /// <returns>Usuário cadastrado.</returns>
        Task<User> RegisterAsync(string name, string base64Image);

        /// <summary>
        /// Autentica um usuário com base na imagem facial.
        /// Inclui verificação de anti-spoofing e rate limiting.
        /// </summary>
        /// <param name="base64Image">Imagem facial codificada em base64.</param>
        /// <returns>Resultado da autenticação.</returns>
        Task<AuthenticationResult> AuthenticateAsync(string base64Image);

        /// <summary>
        /// Retorna os logs de acesso com paginação e filtro opcional.
        /// </summary>
        /// <param name="page">Número da página (1-based).</param>
        /// <param name="pageSize">Tamanho da página.</param>
        /// <param name="successFilter">Filtro por sucesso (null = todos).</param>
        /// <returns>Lista de logs de acesso.</returns>
        Task<(List<AccessLog> Logs, int TotalCount)> GetAccessLogsAsync(int page, int pageSize, bool? successFilter);
    }
}
