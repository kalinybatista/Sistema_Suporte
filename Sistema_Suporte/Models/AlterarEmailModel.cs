using System.ComponentModel.DataAnnotations;

namespace Sistema_Suporte.Models
{
    public class AlterarEmailModel
    {
        public string EmailAtual { get; set; }

        [Required(ErrorMessage = "Informe seu novo email")]
        [EmailAddress(ErrorMessage = "Email inválido")]
        public string NovoEmail { get; set; }

        [Required(ErrorMessage = "Confirme seu novo email")]
        [EmailAddress(ErrorMessage = "Email inválido")]
        [Compare("NovoEmail", ErrorMessage = "Os emails não conferem")]
        public string ConfirmarEmail { get; set; }
    }

}
