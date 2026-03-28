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
        /// Registra um novo usuário com base na imagem facial.
        /// </summary>
        /// <param name="name">Nome do usuário.</param>
        /// <param name="base64Image">Imagem facial codificada em base64.</param>
        /// <returns>Usuário cadastrado.</returns>
        Task<User> RegisterAsync(string name, string base64Image);

        /// <summary>
        /// Autentica um usuário com base na imagem facial.
        /// </summary>
        /// <param name="base64Image">Imagem facial codificada em base64.</param>
        /// <returns>Resultado da autenticação.</returns>
        Task<AuthenticationResult> AuthenticateAsync(string base64Image);
    }
}
