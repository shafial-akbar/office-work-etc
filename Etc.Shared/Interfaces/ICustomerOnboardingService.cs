using Etc.Shared.DTOs;
using Etc.Shared.Models;

namespace Etc.Shared.Interfaces
{
    public interface ICustomerOnboardingService
    {
        Task<Vehicle> AddVehicleToWalletAsync(AddVehicleToWalletDto dto);
        Task<Wallet> CreateNewWalletAsync(CreateNewWalletDto dto);
        Task<Customer> RegisterFullCustomerAsync(RegisterFullCustomerDto dto);
        Task<ApiResponse<Wallet>> UnregisterVehicleAsync(VehicleUnregisterRequest request);
    }
}