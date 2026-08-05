using Etc.Shared.DTOs;
using Etc.Shared.Models;

namespace Etc.Shared.Interfaces
{
    public interface IVehicleService
    {
        Task<IEnumerable<Vehicle>> GetAllAsync();

        // Guid used instead of int
        Task<Vehicle?> GetByIdAsync(Guid id);
        Task<Vehicle?> GetByVehicleIdAsync(string vehicleRegistrationNumber);

        // Returns generated Guid ID
        Task<Guid> AddAsync(Vehicle vehicle);

        Task UpdateAsync(Vehicle vehicle);

        // Guid used instead of int
        Task DeleteAsync(Guid id);
    }
}