using System.ComponentModel.DataAnnotations;

namespace Sistema_Suporte.Models
{
    public class PasswordResetToken
    {
        [Key]
        public int Id { get; set; }

        public string Email { get; set; }
        public string Token { get; set; }
        public DateTime Expiration { get; set; }
    }

}
