using Etc.Shared.DTOs;

namespace Etc.Shared.Interfaces
{
    public interface ICustomerInquiryServiceGW
    {
        Task<AccountCheckResponseDto> CheckAccountByMobileAsync(string mobileNo);
        Task<List<WalletBalanceResultDto>> GetWalletBalanceAsync(string searchKey);
    }
}