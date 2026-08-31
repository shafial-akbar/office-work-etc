using Etc.Shared.DTOs;
using Etc.Shared.Models;

namespace Etc.Shared.Interfaces
{
    public interface ICustomerOnboardingService
    {
        Task<Vehicle> AddVehicleToWalletAsync(AddVehicleToWalletDto dto);
        Task<Wallet> CreateNewWalletAsync(CreateNewWalletDto dto);
        Task<CustomerOnboardingResponseDto> RegisterFullCustomerAsync(RegisterFullCustomerDto dto);
        Task<ApiResponse<Wallet>> UnregisterVehicleAsync(VehicleUnregisterRequest request);
    }
}