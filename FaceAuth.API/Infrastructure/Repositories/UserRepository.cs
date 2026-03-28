using FaceAuth.API.Domain.Entities;
using FaceAuth.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FaceAuth.API.Infrastructure.Repositories
{
    /// <summary>
    /// Repositório para operações de banco de dados relacionadas a usuários.
    /// </summary>
    public class UserRepository
    {
        private readonly AppDbContext _context;

        public UserRepository(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Adiciona um novo usuário ao banco de dados.
        /// </summary>
        /// <param name="user">Entidade User a ser persistida.</param>
        /// <returns>Usuário criado com Id gerado.</returns>
        public async Task<User> AddAsync(User user)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        /// <summary>
        /// Retorna todos os usuários cadastrados.
        /// </summary>
        /// <returns>Lista de todos os usuários.</returns>
        public async Task<List<User>> GetAllAsync()
        {
            return await _context.Users.ToListAsync();
        }

        /// <summary>
        /// Busca um usuário pelo Id.
        /// </summary>
        /// <param name="id">Id do usuário.</param>
        /// <returns>Usuário encontrado ou null.</returns>
        public async Task<User?> GetByIdAsync(int id)
        {
            return await _context.Users.FindAsync(id);
        }

        /// <summary>
        /// Adiciona um log de acesso ao banco de dados.
        /// </summary>
        /// <param name="accessLog">Entidade AccessLog a ser persistida.</param>
        public async Task AddAccessLogAsync(AccessLog accessLog)
        {
            _context.AccessLogs.Add(accessLog);
            await _context.SaveChangesAsync();
        }
    }
}
