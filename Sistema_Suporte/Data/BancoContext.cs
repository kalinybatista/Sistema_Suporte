using Microsoft.EntityFrameworkCore;
using Sistema_Suporte.Models;

namespace Sistema_Suporte.Data
{
    public class BancoContext : DbContext
    {
        public BancoContext(DbContextOptions<BancoContext> options) : base(options) { }

        public DbSet<RegistroUsuarios> Usuarios { get; set; } = null!;
        public DbSet<RegistroTecnico> Tecnicos { get; set; } = null!;
        public DbSet<Chamado> Chamados { get; set; } = null!;
        public DbSet<ChatMessage> ChatMessages { get; set; } = null!;
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configuração do relacionamento Chamado -> ChatMessage
            modelBuilder.Entity<Chamado>()
                .HasMany(c => c.Mensagens)
                .WithOne(m => m.Chamado) // ✅ AGORA EXISTE!
                .HasForeignKey(m => m.ChamadoId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configurações adicionais
            modelBuilder.Entity<RegistroUsuarios>(entity =>
            {
                entity.ToTable("usuarios");
                entity.HasKey(u => u.IdUsuario);
                entity.HasIndex(u => u.Email).IsUnique();
            });

            modelBuilder.Entity<RegistroTecnico>(entity =>
            {
                entity.ToTable("tecnicos");
                entity.HasKey(t => t.IdTecnico);
                entity.HasIndex(t => t.Email).IsUnique();
            });

            modelBuilder.Entity<Chamado>(entity =>
            {
                entity.ToTable("chamados");
                entity.HasKey(c => c.IdChamado);

                // Relacionamento com Usuario
                entity.HasOne(c => c.Usuario)
                    .WithMany()
                    .HasForeignKey(c => c.UsuarioId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Relacionamento com Tecnico
                entity.HasOne(c => c.Tecnico)
                    .WithMany()
                    .HasForeignKey(c => c.TecnicoId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ChatMessage>(entity =>
            {
                entity.ToTable("mensagens");
                entity.HasKey(m => m.IdMensagem);

                // Relacionamento com Chamado
                entity.HasOne(m => m.Chamado)
                    .WithMany(c => c.Mensagens)
                    .HasForeignKey(m => m.ChamadoId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}