using Etc.Shared.Constants;

using Etc.Shared.Interfaces;
using Etc.Shared.Models;
using Etc.Shared.DTOs;
using ETCGatewayAPI.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Etc.Shared.Helpers;


namespace ETCGatewayAPI.Services
{
    public class CustomerInquiryServiceGW : ICustomerInquiryServiceGW
    {
        private readonly DatabaseContext _context;
        private readonly ILogger<CustomerInquiryServiceGW> _logger;

        public CustomerInquiryServiceGW(DatabaseContext context, ILogger<CustomerInquiryServiceGW> logger)
        {
            _context = context;
            _logger = logger;
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
            var requestTime = DateTime.UtcNow;

            // ১. খালি/ইনভ্যালিড সার্চ কি-এর ক্ষেত্রে অডিট লগ ও রেসপন্স
            if (string.IsNullOrWhiteSpace(searchKey))
            {
                var emptyResponse = new List<WalletBalanceResultDto>();

                await SaveInquiryTransactionLogAsync(
                    searchKey: searchKey,
                    response: emptyResponse,
                    requestTime: requestTime,
                    status: "Failed",
                    errorMessage: "Search key cannot be null or empty."
                );

                return emptyResponse;
            }

            try
            {
                // ২. রিড-ওনলি ডাটাবেজ কোয়েরি
                var result = await _context.Wallets
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

                // ৩. ইনকোয়ারি সফল হলে TransactionLog সেভ
                await SaveInquiryTransactionLogAsync(
                    searchKey: searchKey,
                    response: result,
                    requestTime: requestTime,
                    status: "Success",
                    accountNo: result.FirstOrDefault()?.WalletNo
                );

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during GetWalletBalanceAsync for searchKey: {SearchKey}", searchKey);

                var errorResponse = new List<WalletBalanceResultDto>();

                await SaveInquiryTransactionLogAsync(
                    searchKey: searchKey,
                    response: errorResponse,
                    requestTime: requestTime,
                    status: "Failed",
                    errorMessage: ex.Message
                );

                return errorResponse;
            }
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

        // AccountInquiry-এর জন্য কাস্টমাইজড হেলপার মেথড
        private async Task SaveInquiryTransactionLogAsync(
            string searchKey,
            object response,
            DateTime requestTime,
            string status,
            string accountNo = null,
            string errorMessage = null)
        {
            try
            {
                var log = new TransactionLog
                {
                    Id = Guid.NewGuid(),
                    PartnerId = null,
                    PartnerTxnId = null,
                    RequestType = TranLogRequestType.AccountInquiry,
                    RequestData = JsonSerializer.Serialize(new { SearchKey = searchKey }),
                    ResponseData = response != null ? JsonSerializer.Serialize(response) : null,
                    ResponseCode = status == "Success" ? "200" : "400",
                    ResponseMessage = errorMessage ?? (status == "Success" ? "Inquiry Successful" : "Inquiry Failed"),
                    RequestTimestamp = requestTime,
                    ResponseTimestamp = DateTime.UtcNow,
                    Status = status,
                    SblTxnId = null,
                    AccountNo = accountNo ?? searchKey,
                    TransactionAmount = null,
                    BalanceBefore = null,
                    BalanceAfter = null,
                    TranMode = null
                };

                await _context.TransactionLogs.AddAsync(log);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write Inquiry TransactionLog for searchKey: {SearchKey}", searchKey);
            }
        }
    }
}