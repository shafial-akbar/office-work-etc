using Etc.Shared.Constants;
using Etc.Shared.DTOs;
using Etc.Shared.Interfaces;
using Etc.Shared.Models;
using EtcMwApi.Data;
using Microsoft.EntityFrameworkCore;

namespace EtcMwApi.Services
{
    public class CustomerInquiryService : ICustomerInquiryService
    {
        private readonly DatabaseContext _context;

        public CustomerInquiryService(DatabaseContext context)
        {
            _context = context;
        }

        public async Task<AccountCheckResponseDto> CheckAccountByMobileAsync(string mobileNo)
        {
            var matchedWallet = await _context.Wallets
                .Include(w => w.Customer)
                    .ThenInclude(c => c.Wallets.Where(w => w.Status == WalletStatus.Active)) // Active wallets filter
                        .ThenInclude(w => w.Vehicles.Where(v => v.Status == VehicleStatus.Active)) // Active vehicles filter
                .Include(w => w.Vehicles.Where(v => v.Status == VehicleStatus.Active)) // Matched wallet active vehicles
                .FirstOrDefaultAsync(w => w.MobileNo == mobileNo && w.Status == WalletStatus.Active); // Replaced hardcoded "Active"

            if (matchedWallet == null || matchedWallet.Customer == null)
            {
                return new AccountCheckResponseDto
                {
                    Status = "NOT_FOUND",
                    Message = "No active record found with this mobile number. Please register a new Customer, Wallet, and Vehicle.",
                    AllowedActions = new List<string> { "REGISTER_NEW_CUSTOMER" }
                };
            }

            var customer = matchedWallet.Customer;

            return new AccountCheckResponseDto
            {
                Status = "EXISTS",
                Message = "Customer record found.",
                CustomerInfo = new CustomerSummaryDto
                {
                    CustomerGuid = customer.Id,
                    CustomerId = customer.CustomerId,
                    Name = customer.Name
                },
                RequestedWallet = MapToWalletDto(matchedWallet),
                OtherWallets = customer.Wallets
                    .Where(w => w.Id != matchedWallet.Id && w.Status == WalletStatus.Active) // Replaced hardcoded "Active"
                    .Select(MapToWalletDto)
                    .ToList(),
                AllowedActions = new List<string>
                {
                    "ADD_VEHICLE_TO_EXISTING_WALLET",
                    "CREATE_NEW_WALLET_FOR_CUSTOMER"
                }
            };
        }

        // নতুন মেথড: মোবাইল নম্বর বা ওয়ালেট নম্বর দিয়ে ব্যালেন্স চেক
        public async Task<List<WalletBalanceResultDto>> GetWalletBalanceAsync(string searchKey)
        {
            if (string.IsNullOrWhiteSpace(searchKey))
            {
                return new List<WalletBalanceResultDto>();
            }

            return await _context.Wallets
                .AsNoTracking()
                .Where(w => w.Status == WalletStatus.Active &&
                           (w.MobileNo == searchKey || w.WalletNo == searchKey))
                .Select(w => new WalletBalanceResultDto
                {
                    WalletNo = w.WalletNo,
                    Balance = w.Balance,
                    Currency = w.Currency
                })
                .ToListAsync();
        }

        private static WalletSummaryDto MapToWalletDto(Wallet wallet)
        {
            return new WalletSummaryDto
            {
                WalletGuid = wallet.Id,
                WalletNo = wallet.WalletNo,
                MobileNo = wallet.MobileNo,
                Balance = wallet.Balance,
                Currency = wallet.Currency,
                Vehicles = wallet.Vehicles != null
                    ? wallet.Vehicles
                        .Where(v => v.Status == VehicleStatus.Active) // Replaced hardcoded "Active"
                        .Select(v => new VehicleSummaryDto
                        {
                            VehicleGuid = v.Id,
                            VehicleRegistrationNumber = v.VehicleRegistrationNumber,
                            ChassisNo = v.ChassisNo,
                            BrtaClass = v.BrtaClass,
                            RhdClass = v.RhdClass,
                            VehicleCC = v.VehicleCC,
                            VehicleColour = v.VehicleColour
                        }).ToList()
                    : new List<VehicleSummaryDto>()
            };
        }
    }
}