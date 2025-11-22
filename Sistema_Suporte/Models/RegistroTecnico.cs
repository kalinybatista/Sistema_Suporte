using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sistema_Suporte.Models
{
    [Table("tecnicos")]
    public class RegistroTecnico
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id_tecnico")]
        public int IdTecnico { get; set; }

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
        public string Tipo { get; set; } = "Tecnico";

        [Column("data_cadastro")] // ✅ CORRIGIDO: data_cadastro no banco
        public DateTime DataCadastro { get; set; } = DateTime.Now;

        // Campos que NÃO existem no banco - marcar como [NotMapped]
        [NotMapped]
        public DateTime? UltimoAcesso { get; set; }

        [NotMapped]
        public bool Disponivel { get; set; } = true;
    }
}
