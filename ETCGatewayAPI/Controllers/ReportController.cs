using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETCGatewayAPI.Controllers
{
    [ApiController]
    //[Route("api/[controller]")]
    [Route("api")]
    [Authorize]
    public class ReportController : Controller
    {
        
    }
}
