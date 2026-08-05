using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Etc.Shared.Models
{
    public class ApiToken
    {
        [Key]
        [Column("Id")]
        public Guid Id { get; set; }
        public string Token { get; set; }
        public DateTime Expiry { get; set; }
        public DateTime CreatedAt { get; set; }
    }    
}
