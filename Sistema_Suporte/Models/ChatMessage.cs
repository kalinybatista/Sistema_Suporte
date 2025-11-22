using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sistema_Suporte.Models
{
    [Table("mensagens")]
    public class ChatMessage
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id_mensagem")]
        public int IdMensagem { get; set; }

        [Required]
        [Column("conteudo")]
        public string Conteudo { get; set; } = string.Empty;

        [Required]
        [Column("tipo_remetente")]
        public string TipoRemetente { get; set; } = string.Empty;

        [Required]
        [Column("chamado_id")]
        public int ChamadoId { get; set; }

        [Column("remetente_id")]
        public int? RemetenteId { get; set; }

        [Column("data_envio")]
        public DateTime DataEnvio { get; set; } = DateTime.Now;

        // Propriedade de navegação
        [ForeignKey("ChamadoId")]
        public virtual Chamado Chamado { get; set; } = null!;
    }
}