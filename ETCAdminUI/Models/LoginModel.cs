using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace ETCAdminUI.Models
{
    public class LoginModel
    {
        [Required]
        [DisplayName("User Name")]
        public string UserId { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [DisplayName("Password")]
        public string Password { get; set; }
    }
}
