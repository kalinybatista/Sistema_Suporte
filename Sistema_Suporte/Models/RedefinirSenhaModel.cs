using System.ComponentModel.DataAnnotations;

namespace Sistema_Suporte.Models
{
    public class RedefinirSenhaModel
    {
        [Required]
        public string Token { get; set; }

        [Required]
        [MinLength(6)]
        public string NovaSenha { get; set; }

        [Required]
        [Compare("NovaSenha")]
        public string ConfirmarSenha { get; set; }
    }

}
