using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Etc.Shared.Models
{
    public class Vehicle
    {
        [Key]
        public Guid Id { get; set; }
        public Guid WalletId { get; set; }
        public string VehicleRegistrationNumber { get; set; } = string.Empty;
        public string ChassisNo { get; set; } = string.Empty;
        public string BrtaClass { get; set; } = string.Empty;
        public string RhdClass { get; set; } = string.Empty;
        public string VehicleCC { get; set; } = string.Empty;
        public string VehicleColour { get; set; } = string.Empty;
        public string Status { get; set; } = "Active";
        public DateTime RegisterDate { get; set; } = DateTime.UtcNow;

        // Fixed: Made Nullable
        public DateTime? UnregisterDate { get; set; }

        [JsonIgnore]
        public Wallet Wallet { get; set; } = null!;
    }
}
