using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Security.Claims;

namespace CineMatch.API.Controllers
{
    [ApiController]
    [Authorize]
    public class BaseController : ControllerBase
    {
        protected string GetCurrentUserId()
        {
            var userId = User.FindFirst("sub")?.Value ??
                        User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                        User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrEmpty(userId))
                throw new UnauthorizedAccessException("User ID not found in token");
            return userId;
        }
    }
}