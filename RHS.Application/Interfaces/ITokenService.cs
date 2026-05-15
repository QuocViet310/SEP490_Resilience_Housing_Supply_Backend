using RHS.Domain.Entities;
using System.Security.Claims;

namespace RHS.Application.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    ClaimsPrincipal? ValidateToken(string token);
}
