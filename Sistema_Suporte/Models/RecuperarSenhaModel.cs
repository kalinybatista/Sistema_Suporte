using System.ComponentModel.DataAnnotations;

namespace Sistema_Suporte.Models
{
    public class RecuperarSenhaModel
    {
        [Required(ErrorMessage = "Email é obrigatório")]
        [EmailAddress(ErrorMessage = "Email inválido")]
        public required string Email { get; set; }
    }
}
