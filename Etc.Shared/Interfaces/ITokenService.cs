using Etc.Shared.DTOs;
using Etc.Shared.Models;

namespace Etc.Shared.Interfaces
{
    public interface ITokenService
    {
        Task<string> GetToken();
    }

}
