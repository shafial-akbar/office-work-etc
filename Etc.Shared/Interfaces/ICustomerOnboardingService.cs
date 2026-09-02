using Etc.Shared.DTOs;
using Etc.Shared.Models;

namespace Etc.Shared.Interfaces
{
    public interface ICustomerOnboardingService
    {
        Task<VehicleOnboardingResponseDto> AddVehicleToWalletAsync(AddVehicleToWalletDto dto);
        Task<VehicleOnboardingResponseDto> CreateNewWalletAsync(CreateNewWalletDto dto);
        Task<CustomerOnboardingResponseDto> RegisterFullCustomerAsync(RegisterFullCustomerDto dto);
        Task<VehicleUnregisterResponse> UnregisterVehicleAsync(VehicleUnregisterDto request);
    }
}