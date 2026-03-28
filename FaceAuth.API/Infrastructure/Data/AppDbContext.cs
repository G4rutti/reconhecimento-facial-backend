using FaceAuth.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FaceAuth.API.Infrastructure.Data
{
    /// <summary>
    /// Contexto do Entity Framework Core para o banco de dados faceauth.
    /// </summary>
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        /// <summary>
        /// Tabela de usuários cadastrados.
        /// </summary>
        public DbSet<User> Users { get; set; } = null!;

        /// <summary>
        /// Tabela de logs de acesso.
        /// </summary>
        public DbSet<AccessLog> AccessLogs { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configuração da entidade User
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.Id);
                entity.Property(u => u.Name)
                    .IsRequired()
                    .HasMaxLength(200);
                entity.Property(u => u.Embedding)
                    .IsRequired();
            });

            // Configuração da entidade AccessLog
            modelBuilder.Entity<AccessLog>(entity =>
            {
                entity.HasKey(a => a.Id);
                entity.Property(a => a.Timestamp)
                    .IsRequired();
                entity.Property(a => a.Confidence);

                // Relacionamento: AccessLog -> User (opcional)
                entity.HasOne(a => a.User)
                    .WithMany(u => u.AccessLogs)
                    .HasForeignKey(a => a.UserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });
        }
    }
}
