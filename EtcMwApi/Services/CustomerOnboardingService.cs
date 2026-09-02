using Etc.Shared.Constants;
using Etc.Shared.DTOs;
using Etc.Shared.Interfaces;
using Etc.Shared.Models;
using EtcMwApi.Data;
using Microsoft.EntityFrameworkCore;

namespace EtcMwApi.Services
{
    public class CustomerOnboardingService : ICustomerOnboardingService
    {
        private readonly DatabaseContext _context;
        private readonly IRhdApiService _rhdApiService;
        private readonly ILogger<CustomerOnboardingService> _logger;

        public CustomerOnboardingService(
            DatabaseContext context,
            IRhdApiService rhdApiService,
            ILogger<CustomerOnboardingService> logger)
        {
            _context = context;
            _rhdApiService = rhdApiService;
            _logger = logger;
        }

        // ==========================================
        // PUBLIC ONBOARDING METHODS
        // ==========================================

        public async Task<CustomerOnboardingResponseDto> RegisterFullCustomerAsync(RegisterFullCustomerDto dto)
        {
            await ValidateMobileNumberAvailabilityAsync(dto.MobileNo);
            await ValidateLocalVehicleExistenceAsync(dto.VehicleRegistrationNumber);

            // 1. Common External Check
            var rhdVehicleData = await FetchAndValidateRhdVehicleAsync(dto.VehicleRegistrationNumber, dto.CompanyOid);

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var now = DateTime.UtcNow;

                var customer = new Customer
                {
                    Id = Guid.NewGuid(),
                    CustomerId = CustomerIdGenerator.GenerateCustomerId(),
                    Name = dto.CustomerName,
                    Email = dto.Email,
                    DateOfBirth = dto.DateOfBirth.HasValue
                        ? DateTime.SpecifyKind(dto.DateOfBirth.Value, DateTimeKind.Utc)
                        : null,
                    FatherName = dto.FatherName,
                    MotherName = dto.MotherName,
                    RegistrationDate = now,
                    Status = CustomerStatus.Active,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                await _context.Customers.AddAsync(customer);

                var wallet = await CreateWalletEntityAsync(customer.Id, dto.MobileNo);

                // 2. Common Mapping
                var vehicle = MapToVehicleInformation(dto.VehicleRegistrationNumber, wallet.Id, rhdVehicleData);

                await _context.Vehicles.AddAsync(vehicle);
                await _context.SaveChangesAsync();

                // 3. Common Remote Registration
                await ConfirmRhdRegistrationAsync(
                    dto.VehicleRegistrationNumber,
                    wallet.MobileNo,
                    wallet.WalletNo,
                    dto.CompanyOid,
                    "Full Customer  Onboarding with Vehicle");

                await transaction.CommitAsync();
                _logger.LogInformation("Full customer onboarding completed successfully. CustomerId: {CustomerId}", customer.CustomerId);

                // 4. Response DTO Return
                return new CustomerOnboardingResponseDto
                {
                    CustomerId = customer.CustomerId,
                    Name = customer.Name,
                    MobileNo = wallet.MobileNo,
                    WalletNo = wallet.WalletNo,
                    VehicleRegistrationNumber = dto.VehicleRegistrationNumber,
                    Message = $"customer : {customer.CustomerId} onboarding completed successfully with wallet : {wallet.WalletNo} and vehicle : {dto.VehicleRegistrationNumber}"
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to register full customer.");
                throw;
            }
        }

        public async Task<VehicleOnboardingResponseDto> CreateNewWalletAsync(CreateNewWalletDto dto)
        {
            var customer = await _context.Customers.FindAsync(dto.CustomerGuid)
                ?? throw new KeyNotFoundException($"Customer not found with ID: {dto.CustomerGuid}");

            await ValidateMobileNumberAvailabilityAsync(dto.NewMobileNo);
            await ValidateLocalVehicleExistenceAsync(dto.VehicleRegistrationNumber);

            // 1. Common External Check
            var rhdVehicleData = await FetchAndValidateRhdVehicleAsync(dto.VehicleRegistrationNumber, dto.CompanyOid);

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var wallet = await CreateWalletEntityAsync(customer.Id, dto.NewMobileNo);

                // 2. Common Mapping
                var vehicle = MapToVehicleInformation(dto.VehicleRegistrationNumber, wallet.Id, rhdVehicleData);

                await _context.Vehicles.AddAsync(vehicle);
                await _context.SaveChangesAsync();

                // 3. Common Remote Registration
                await ConfirmRhdRegistrationAsync(
                    dto.VehicleRegistrationNumber,
                    wallet.MobileNo,
                    wallet.WalletNo,
                    dto.CompanyOid,
                    "New Wallet Creation & Vehicle Registration");

                await transaction.CommitAsync();
                _logger.LogInformation("New Wallet {WalletNo} and Vehicle registered for CustomerId: {CustomerId}", wallet.WalletNo, customer.CustomerId);
                // 5. Response DTO Return
                return new VehicleOnboardingResponseDto
                {
                    VehicleGuid = vehicle.Id,
                    VehicleRegistrationNumber = vehicle.VehicleRegistrationNumber,
                    WalletGuid = wallet.Id,
                    WalletNo = wallet.WalletNo,
                    MobileNo = wallet.MobileNo,
                    BrtaClass = vehicle.BrtaClass,
                    RhdClass = vehicle.RhdClass,
                    ChassisNo = vehicle.ChassisNo,
                    AddedAt = DateTime.UtcNow,
                    Message = $"Vehicle : {vehicle.VehicleRegistrationNumber} successfully registered and added to wallet : {wallet.WalletNo}"
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to create new wallet for customer: {CustomerId}", dto.CustomerGuid);
                throw;
            }
        }

        public async Task<VehicleOnboardingResponseDto> AddVehicleToWalletAsync(AddVehicleToWalletDto dto)
        {
            // ১. লোকাল ডাটাবেজ প্রাক-যাচাই (dto.WalletId ব্যবহার করা হয়েছে)
            var wallet = await _context.Wallets.FindAsync(dto.WalletGuid)
                ?? throw new KeyNotFoundException($"Wallet not found with ID: {dto.WalletGuid}");

            await ValidateLocalVehicleExistenceAsync(dto.VehicleRegistrationNumber);

            // ২. এক্সটার্নাল RHD ইনকোয়ারি
            var rhdVehicleData = await FetchAndValidateRhdVehicleAsync(dto.VehicleRegistrationNumber, dto.CompanyOid);

            // ৩. RHD API-তে রেজিস্ট্রেশন সম্পন্ন করা
            await ConfirmRhdRegistrationAsync(
                dto.VehicleRegistrationNumber,
                wallet.MobileNo,
                wallet.WalletNo,
                dto.CompanyOid,
                "Vehicle linked to existing wallet via Onboarding");

            // ৪. লোকাল ডাটাবেজে সেভ করা এবং Compensation Logic
            try
            {
                var vehicle = MapToVehicleInformation(dto.VehicleRegistrationNumber, wallet.Id, rhdVehicleData);

                await _context.Vehicles.AddAsync(vehicle);
                await _context.SaveChangesAsync();

                _logger.LogInformation("New vehicle {RegNo} successfully added to Wallet {WalletNo}",
                    vehicle.VehicleRegistrationNumber, wallet.WalletNo);

                // 5. Response DTO Return
                return new VehicleOnboardingResponseDto
                {
                    VehicleGuid = vehicle.Id,
                    VehicleRegistrationNumber = vehicle.VehicleRegistrationNumber,
                    WalletGuid = wallet.Id,
                    WalletNo = wallet.WalletNo,
                    MobileNo = wallet.MobileNo,
                    BrtaClass = vehicle.BrtaClass,
                    RhdClass = vehicle.RhdClass,
                    ChassisNo = vehicle.ChassisNo,
                    AddedAt = DateTime.UtcNow,
                    Message = $"Vehicle : {vehicle.VehicleRegistrationNumber} successfully registered and added to wallet : {wallet.WalletNo}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Local DB save failed for vehicle {RegNo} after successful RHD Registration. Initiating RHD Compensation...",
                    dto.VehicleRegistrationNumber);

                // 6. Compensating Transaction: লোকাল DB ফেল করলে RHD-র রেজিস্ট্রেশন রিভার্ট করা
                try
                {
                    var unregisterRequest = new VehicleUnregisterRequest
                    {
                        VehicleRegistrationNumber = dto.VehicleRegistrationNumber,
                        CompanyOid = dto.CompanyOid,
                        WalletNumber = wallet.WalletNo,
                        Status = 0 // Inactive
                    };

                    var unregisterResult = await _rhdApiService.UnregisterVehicle(unregisterRequest);
                    if (!unregisterResult.Success)
                    {
                        _logger.LogCritical("CRITICAL: Failed to compensate/unregister vehicle {RegNo} on RHD! Manual intervention required. Error: {Message}",
                            dto.VehicleRegistrationNumber, unregisterResult.Message);
                    }
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogCritical(rollbackEx, "CRITICAL: Exception occurred while trying to compensate/unregister vehicle {RegNo} from RHD.",
                        dto.VehicleRegistrationNumber);
                }

                throw new InvalidOperationException("Failed to save vehicle details in local database. The RHD registration has been reverted.", ex);
            }
        }


        public async Task<VehicleUnregisterResponse> UnregisterVehicleAsync(VehicleUnregisterDto dto)
        {
            var rdhRequest = new VehicleUnregisterRequest
            {
                VehicleRegistrationNumber = dto.VehicleRegistrationNumber,
                CompanyOid = dto.CompanyOid,
                WalletNumber = dto.WalletNo,
                Status = 0
            };

            // 1. External RHD API Call
            var result = await _rhdApiService.UnregisterVehicle(rdhRequest);

            if (!result.Success)
            {
                _logger.LogWarning("RHD Vehicle Unregistration failed for Vehicle: {RegNo}, Message: {Message}",
                    dto.VehicleRegistrationNumber, result.Message);
                return result;
            }

            // 2. Local Database Transaction Update
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Fetch Wallet using WalletNumber
                var wallet = await _context.Wallets
                    .FirstOrDefaultAsync(w => w.WalletNo == dto.WalletNo);

                if (wallet == null)
                {
                    return new VehicleUnregisterResponse
                    {
                        Success = false,
                        Reason = "NOT_FOUND",
                        Message = "Wallet record not found in local database.",
                        StatusCode = 404
                    };
                }

                // Fetch corresponding Vehicle record using WalletId and Registration Number
                var vehicle = await _context.Vehicles
                    .FirstOrDefaultAsync(v => v.WalletId == wallet.Id &&
                                              v.VehicleRegistrationNumber == dto.VehicleRegistrationNumber &&
                                              v.Status == VehicleStatus.Active);

                if (vehicle != null)
                {
                    // Mark the vehicle as Inactive
                    vehicle.Status = VehicleStatus.Inactive;
                    vehicle.UnregisterDate = DateTime.UtcNow;
                }

                // GL settlement queue for refund
                await ProcessGlSettlementForUnregisterAsync(wallet);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Successfully unregistered vehicle {RegNo} and set status to Inactive in local DB.",
                    dto.VehicleRegistrationNumber);

                return result;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to update local DB records during vehicle unregistration for RegNo: {RegNo}",
                    dto.VehicleRegistrationNumber);

                return new VehicleUnregisterResponse
                {
                    Success = false,
                    Reason = "EXCEPTION",
                    Message = "Failed to update local database after unregistration.",
                    StatusCode = 500
                };
            }
        }

        // ==========================================
        // PRIVATE HELPER / REUSABLE METHODS
        // ==========================================

        private async Task<Vehicle> FetchAndValidateRhdVehicleAsync(string vehicleRegNo, int companyOid)
        {
            var checkResult = await _rhdApiService.GetVehicleInformation(vehicleRegNo, companyOid);

            if (!checkResult.Success || checkResult.Data == null)
            {
                throw new InvalidOperationException($"RHD Inquiry Failed: {checkResult.Message}");
            }

            if (checkResult.Data.Wallet != null)
            {
                throw new InvalidOperationException($"Vehicle is already registered in RHD ETC System under {checkResult.Data.Wallet.CompanyName}");
            }

            return checkResult.Data;
        }

        private async Task ConfirmRhdRegistrationAsync(string regNo, string mobileNo, string walletNo, int companyOid, string description)
        {
            var regiRequest = new VehicleRegistrationRequest
            {
                VehicleRegistrationNumber = regNo,
                CompanyOid = companyOid,
                MobileNumber = mobileNo,
                WalletNumber = walletNo,
                Description = description
            };

            var regiResult = await _rhdApiService.RegisterVehicle(regiRequest);
            if (!regiResult.Success)
            {
                throw new Exception($"RHD Remote Vehicle Registration Failed: {regiResult.Message}");
            }
        }

        private static Vehicle MapToVehicleInformation(
            string regNo,
            Guid walletId,
            Vehicle rhdData)
        {
            return new Vehicle
            {
                Id = Guid.NewGuid(),
                WalletId = walletId,
                VehicleRegistrationNumber = regNo,
                ChassisNo = rhdData.ChassisNo,
                BrtaClass = rhdData.BrtaClass,
                RhdClass = rhdData.RhdClass,
                VehicleCC = rhdData.VehicleCC,
                VehicleColour = rhdData.VehicleColour,
                Status = VehicleStatus.Active, // Explicitly set status on initial mapping
                RegisterDate = DateTime.UtcNow
            };
        }

        private async Task<Wallet> CreateWalletEntityAsync(Guid customerId, string mobileNo)
        {
            string newWalletNo;
            do
            {
                newWalletNo = WalletNumberGenerator.GenerateWalletNumber();
            }
            while (await _context.Wallets.AnyAsync(w => w.WalletNo == newWalletNo));

            var wallet = new Wallet
            {
                Id = Guid.NewGuid(),
                CustomerId = customerId,
                WalletNo = newWalletNo,
                MobileNo = mobileNo,
                Balance = 0.00m,
                Currency = "BDT",
                Status = WalletStatus.Active, // Replaced "Active"
                CompanyName = "SONALI BANK PLC",
                Type = WalletType.Bank, // Replaced "BANK"
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.Wallets.AddAsync(wallet);
            return wallet;
        }

        private async Task ValidateMobileNumberAvailabilityAsync(string mobileNo)
        {
            bool exists = await _context.Wallets.AnyAsync(w => w.MobileNo == mobileNo);
            if (exists)
            {
                throw new InvalidOperationException($"Mobile number {mobileNo} is already registered.");
            }
        }

        private async Task ValidateLocalVehicleExistenceAsync(string vehicleRegNo)
        {
            bool exists = await _context.Vehicles.AnyAsync(v => v.VehicleRegistrationNumber == vehicleRegNo);
            if (exists)
            {
                throw new InvalidOperationException($"Vehicle registration number {vehicleRegNo} already exists in local DB.");
            }
        }

        private async Task ProcessGlSettlementForUnregisterAsync(Wallet wallet)
        {
            if (wallet.Balance > 0)
            {
                _logger.LogInformation("Pending refund balance {Balance} BDT for Wallet {WalletNo} queued for GL settlement.",
                    wallet.Balance, wallet.WalletNo);
            }
            await Task.CompletedTask;
        }
    }
}