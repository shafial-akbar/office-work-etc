using Etc.Shared.DTOs;
using Etc.Shared.Models;

namespace Etc.Shared.Interfaces
{
    public interface IRhdApiService
    {
        Task<ApiResponse<Vehicle>> GetVehicleInformation(string registrationNumber, int companyOid);
        Task<ApiResponse<VehicleRegiInformation>> RegisterVehicle(VehicleRegistrationRequest request);
        Task<VehicleUnregisterResponse> UnregisterVehicle(VehicleUnregisterRequest request);
    }
}
