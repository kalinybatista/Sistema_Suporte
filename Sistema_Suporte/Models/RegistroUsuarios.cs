using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sistema_Suporte.Models
{
    [Table("usuarios")]
    public class RegistroUsuarios
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id_usuario")]
        public int IdUsuario { get; set; }

        [Required(ErrorMessage = "Nome é obrigatório")]
        [StringLength(100, ErrorMessage = "Nome muito longo")]
        [Column("nome")]
        public string Nome { get; set; }

        [Required(ErrorMessage = "Email é obrigatório")]
        [EmailAddress(ErrorMessage = "Email inválido")]
        [Column("email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Senha é obrigatória")]
        [StringLength(255, MinimumLength = 6, ErrorMessage = "Senha deve ter no mínimo 6 caracteres")]
        [DataType(DataType.Password)]
        [Column("senha")]
        public string Senha { get; set; }

        [Column("tipo")]
        public string Tipo { get; set; } = "Usuario";

        [Column("data_cadastro")] // ✅ CORRIGIDO: data_cadastro no banco
        public DateTime DataCadastro { get; set; } = DateTime.Now;

        // Remove DataCriacao se não existe no banco
        // public DateTime DataCriacao { get; set; } // ❌ REMOVER
    }
}