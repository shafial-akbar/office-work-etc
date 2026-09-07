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
                    .ThenInclude(c => c.Wallets.Where(w => w.Status == WalletStatus.Active))
                        .ThenInclude(w => w.Vehicles.Where(v => v.Status == VehicleStatus.Active))
                .Include(w => w.Vehicles.Where(v => v.Status == VehicleStatus.Active))
                .FirstOrDefaultAsync(w => w.MobileNo == mobileNo && w.Status == WalletStatus.Active);

            // ১. অ্যাকাউন্ট না পাওয়া গেলে ৪০৪ রেসপন্স
            if (matchedWallet == null || matchedWallet.Customer == null)
            {
                return new AccountCheckResponseDto
                {
                    HttpCode = 404, // 👈 Status এর পরিবর্তে HttpCode ও HttpStatus সেট করা হয়েছে
                    HttpStatus = "NOT_FOUND",
                    Message = "No active record found with this mobile number. Please register a new Customer, Wallet, and Vehicle.",
                    AllowedActions = new List<string> { "REGISTER_NEW_CUSTOMER" }
                };
            }

            var customer = matchedWallet.Customer;

            // ২. অ্যাকাউন্ট পাওয়া গেলে ২০০ রেসপন্স
            return new AccountCheckResponseDto
            {
                HttpCode = 200, // 👈 HttpCode ও HttpStatus যুক্ত করা হয়েছে
                HttpStatus = "OK",
                Message = "Customer record found.",
                CustomerInfo = new CustomerSummaryDto
                {
                    CustomerGuid = customer.Id,
                    CustomerId = customer.CustomerId,
                    Name = customer.Name
                },
                RequestedWallet = MapToWalletDto(matchedWallet),
                OtherWallets = customer.Wallets
                    .Where(w => w.Id != matchedWallet.Id && w.Status == WalletStatus.Active)
                    .Select(MapToWalletDto)
                    .ToList(),
                AllowedActions = new List<string>
        {
            "ADD_VEHICLE_TO_EXISTING_WALLET",
            "CREATE_NEW_WALLET_FOR_CUSTOMER"
        }
            };
        }

        public async Task<List<WalletBalanceResultDto>> GetWalletBalanceAsync(string searchKey)
        {
            var requestTime = DateTime.Now; // 👈 Local Time ব্যবহারের জন্য DateTime.Now

            // ১. ইনভ্যালিড সার্চ কি (400 Bad Request)
            if (string.IsNullOrWhiteSpace(searchKey))
            {
                var emptyResponse = new List<WalletBalanceResultDto>
        {
            new WalletBalanceResultDto
            {
                HttpCode = 400,
                HttpStatus = "Bad Request",
                Message = "Search key cannot be null or empty."
            }
        };

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
                // ২. ডাটাবেজ থেকে ২০০ OK রেসপন্স সহ সিলেক্ট
                var result = await _context.Wallets
                    .AsNoTracking()
                    .Where(w => w.Status == WalletStatus.Active &&
                               (w.MobileNo == searchKey || w.WalletNo == searchKey))
                    .Select(w => new WalletBalanceResultDto
                    {
                        HttpCode = 200,
                        HttpStatus = "OK",
                        Message = "Wallet balance fetched successfully.",
                        WalletNo = w.WalletNo,
                        Balance = w.Balance,
                        Currency = w.Currency
                    })
                    .ToListAsync();

                // যদি ডাটা না পাওয়া যায় (404 Not Found)
                if (!result.Any())
                {
                    result.Add(new WalletBalanceResultDto
                    {
                        HttpCode = 404,
                        HttpStatus = "Not Found",
                        Message = "No active wallet found for the provided search key."
                    });
                }

                // ৩. ইনকোয়ারি ট্রানজেকশন লগ সেভ
                await SaveInquiryTransactionLogAsync(
                    searchKey: searchKey,
                    response: result,
                    requestTime: requestTime,
                    status: result.Any(r => r.HttpCode == 200) ? "Success" : "Failed",
                    accountNo: result.FirstOrDefault(r => r.HttpCode == 200)?.WalletNo
                );

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during GetWalletBalanceAsync for searchKey: {SearchKey}", searchKey);

                // ৪. ইন্টারনাল এক্সেপশন (500 Internal Server Error)
                var errorResponse = new List<WalletBalanceResultDto>
        {
            new WalletBalanceResultDto
            {
                HttpCode = 500,
                HttpStatus = "Internal Server Error",
                Message = "An error occurred while fetching wallet balance."
            }
        };

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