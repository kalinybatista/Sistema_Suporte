using System.ComponentModel.DataAnnotations;
namespace Sistema_Suporte.Models
{

    public class AlterarSenhaModel
    {
        [Required(ErrorMessage = "Informe sua senha atual")]
        public string SenhaAtual { get; set; }

        [Required(ErrorMessage = "Informe a nova senha")]
        [MinLength(6, ErrorMessage = "A senha deve ter pelo menos 6 caracteres")]
        public string NovaSenha { get; set; }

        [Required(ErrorMessage = "Confirme a nova senha")]
        [Compare("NovaSenha", ErrorMessage = "As senhas não conferem")]
        public string ConfirmarSenha { get; set; }
    }

}
