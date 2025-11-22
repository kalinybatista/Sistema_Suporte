using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sistema_Suporte.Models
{
    [Table("chamados")]
    public class Chamado
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id_chamado")]
        public int IdChamado { get; set; }

        [Required]
        [Column("titulo")]
        public string Titulo { get; set; } = "Suporte Técnico";

        [Column("descricao")]
        public string? Descricao { get; set; }

        [Required]
        [Column("usuario_id")]
        public int UsuarioId { get; set; }

        [Column("tecnico_id")]
        public int? TecnicoId { get; set; }

        [Required]
        [Column("status")]
        public string Status { get; set; } = "Aberto";

        [Required]
        [Column("tipo_chat")]
        public string TipoChat { get; set; } = "IA";

        [Column("data_abertura")]
        public DateTime DataAbertura { get; set; } = DateTime.Now;

        [Column("data_atualizacao")]
        public DateTime? DataAtualizacao { get; set; }

        // Relacionamentos
        [ForeignKey("UsuarioId")]
        public virtual RegistroUsuarios? Usuario { get; set; }

        [ForeignKey("TecnicoId")]
        public virtual RegistroTecnico? Tecnico { get; set; }

        // Coleção de mensagens
        public virtual ICollection<ChatMessage> Mensagens { get; set; } = new List<ChatMessage>();
    }
}