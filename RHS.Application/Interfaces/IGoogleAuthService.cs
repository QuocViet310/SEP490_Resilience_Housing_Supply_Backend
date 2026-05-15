using Google.Apis.Auth;

namespace RHS.Application.Interfaces;

public interface IGoogleAuthService
{
    Task<GoogleJsonWebSignature.Payload?> VerifyGoogleTokenAsync(string idToken);
}
