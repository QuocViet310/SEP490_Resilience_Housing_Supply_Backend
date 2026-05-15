using Microsoft.Extensions.Configuration;
using RHS.Application.DTOs.Auth;
using RHS.Application.Interfaces;
using RHS.Domain.Entities;
using BCrypt.Net;

namespace RHS.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IOtpRepository _otpRepository;
    private readonly ITokenService _tokenService;
    private readonly IGoogleAuthService _googleAuthService;
    private readonly IOtpService _otpService;
    private readonly IConfiguration _configuration;

    public AuthService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IOtpRepository otpRepository,
        ITokenService tokenService,
        IGoogleAuthService googleAuthService,
        IOtpService otpService,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _otpRepository = otpRepository;
        _tokenService = tokenService;
        _googleAuthService = googleAuthService;
        _otpService = otpService;
        _configuration = configuration;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterDto registerDto)
    {
        // Check if email already exists
        if (await _userRepository.EmailExistsAsync(registerDto.Email))
        {
            return new AuthResponseDto
            {
                Success = false,
                Message = "Email đã được sử dụng"
            };
        }

        // Create new user
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = registerDto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password),
            FullName = registerDto.FullName,
            PhoneNumber = registerDto.PhoneNumber,
            Role = registerDto.Role,
            IsEmailVerified = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _userRepository.CreateAsync(user);

        // Generate and send OTP
        var otpCode = _otpService.GenerateOtp();
        var otpExpirationMinutes = int.Parse(_configuration["OtpSettings:ExpirationMinutes"]!);

        var otp = new OtpCode
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Code = otpCode,
            Purpose = "Registration",
            ExpiresAt = DateTime.UtcNow.AddMinutes(otpExpirationMinutes),
            CreatedAt = DateTime.UtcNow,
            IsUsed = false
        };

        await _otpRepository.CreateAsync(otp);
        await _otpService.SendOtpEmailAsync(user.Email, otpCode, user.FullName);

        return new AuthResponseDto
        {
            Success = true,
            Message = "Đăng ký thành công. Vui lòng kiểm tra email để xác thực tài khoản.",
            RequiresOtpVerification = true,
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role,
                IsEmailVerified = user.IsEmailVerified
            }
        };
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto loginDto)
    {
        var user = await _userRepository.GetByEmailAsync(loginDto.Email);

        if (user == null || user.PasswordHash == null || 
            !BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
        {
            return new AuthResponseDto
            {
                Success = false,
                Message = "Email hoặc mật khẩu không chính xác"
            };
        }

        if (!user.IsActive)
        {
            return new AuthResponseDto
            {
                Success = false,
                Message = "Tài khoản đã bị vô hiệu hóa"
            };
        }

        if (!user.IsEmailVerified)
        {
            return new AuthResponseDto
            {
                Success = false,
                Message = "Vui lòng xác thực email trước khi đăng nhập",
                RequiresOtpVerification = true
            };
        }

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);

        // Generate tokens
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();

        // Save refresh token
        var refreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(
                int.Parse(_configuration["JwtSettings:RefreshTokenExpirationDays"]!)),
            CreatedAt = DateTime.UtcNow,
            IsRevoked = false
        };

        await _refreshTokenRepository.CreateAsync(refreshTokenEntity);

        return new AuthResponseDto
        {
            Success = true,
            Message = "Đăng nhập thành công",
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role,
                IsEmailVerified = user.IsEmailVerified,
                ProfileImageUrl = user.ProfileImageUrl
            }
        };
    }

    public async Task<AuthResponseDto> GoogleLoginAsync(GoogleLoginDto googleLoginDto)
    {
        var payload = await _googleAuthService.VerifyGoogleTokenAsync(googleLoginDto.GoogleIdToken);

        if (payload == null)
        {
            return new AuthResponseDto
            {
                Success = false,
                Message = "Google token không hợp lệ"
            };
        }

        // Check if user exists by GoogleId first
        var user = await _userRepository.GetByGoogleIdAsync(payload.Subject);

        if (user == null)
        {
            // Check if email already exists
            var existingUser = await _userRepository.GetByEmailAsync(payload.Email);

            if (existingUser != null)
            {
                // CASE 1: Email exists - Link Google account to existing user
                // This allows users who registered with email/password to also login with Google
                
                // Check if account is active
                if (!existingUser.IsActive)
                {
                    return new AuthResponseDto
                    {
                        Success = false,
                        Message = "Tài khoản đã bị vô hiệu hóa"
                    };
                }

                // Link Google account and auto-verify email
                existingUser.GoogleId = payload.Subject;
                existingUser.IsEmailVerified = true; // Google has verified this email
                existingUser.ProfileImageUrl = payload.Picture ?? existingUser.ProfileImageUrl;
                existingUser.LastLoginAt = DateTime.UtcNow;
                await _userRepository.UpdateAsync(existingUser);

                user = existingUser;
            }
            else
            {
                // CASE 2: New user - Create account via Google
                user = new User
                {
                    Id = Guid.NewGuid(),
                    Email = payload.Email,
                    FullName = payload.Name,
                    GoogleId = payload.Subject,
                    Role = googleLoginDto.Role,
                    IsEmailVerified = true, // Google has verified this email
                    IsActive = true,
                    ProfileImageUrl = payload.Picture,
                    CreatedAt = DateTime.UtcNow,
                    LastLoginAt = DateTime.UtcNow
                };

                await _userRepository.CreateAsync(user);
            }
        }
        else
        {
            // CASE 3: User exists with GoogleId - Regular Google login
            
            // Check if account is active
            if (!user.IsActive)
            {
                return new AuthResponseDto
                {
                    Success = false,
                    Message = "Tài khoản đã bị vô hiệu hóa"
                };
            }

            // Update last login and profile image if changed
            user.LastLoginAt = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(payload.Picture))
            {
                user.ProfileImageUrl = payload.Picture;
            }
            await _userRepository.UpdateAsync(user);
        }

        // Generate tokens
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();

        // Save refresh token
        var refreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(
                int.Parse(_configuration["JwtSettings:RefreshTokenExpirationDays"]!)),
            CreatedAt = DateTime.UtcNow,
            IsRevoked = false
        };

        await _refreshTokenRepository.CreateAsync(refreshTokenEntity);

        return new AuthResponseDto
        {
            Success = true,
            Message = "Đăng nhập Google thành công",
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role,
                IsEmailVerified = user.IsEmailVerified,
                ProfileImageUrl = user.ProfileImageUrl
            }
        };
    }

    public async Task<AuthResponseDto> VerifyOtpAsync(VerifyOtpDto verifyOtpDto)
    {
        var user = await _userRepository.GetByEmailAsync(verifyOtpDto.Email);

        if (user == null)
        {
            return new AuthResponseDto
            {
                Success = false,
                Message = "Người dùng không tồn tại"
            };
        }

        var otp = await _otpRepository.GetValidOtpAsync(user.Id, verifyOtpDto.OtpCode, "Registration");

        if (otp == null)
        {
            return new AuthResponseDto
            {
                Success = false,
                Message = "Mã OTP không hợp lệ hoặc đã hết hạn"
            };
        }

        // Mark OTP as used
        otp.IsUsed = true;
        otp.UsedAt = DateTime.UtcNow;
        await _otpRepository.UpdateAsync(otp);

        // Verify user email
        user.IsEmailVerified = true;
        user.LastLoginAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);

        // Generate tokens
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();

        // Save refresh token
        var refreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(
                int.Parse(_configuration["JwtSettings:RefreshTokenExpirationDays"]!)),
            CreatedAt = DateTime.UtcNow,
            IsRevoked = false
        };

        await _refreshTokenRepository.CreateAsync(refreshTokenEntity);

        return new AuthResponseDto
        {
            Success = true,
            Message = "Xác thực email thành công",
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role,
                IsEmailVerified = user.IsEmailVerified,
                ProfileImageUrl = user.ProfileImageUrl
            }
        };
    }

    public async Task<AuthResponseDto> RefreshTokenAsync(string refreshToken)
    {
        var tokenEntity = await _refreshTokenRepository.GetByTokenAsync(refreshToken);

        if (tokenEntity == null || tokenEntity.IsRevoked || tokenEntity.ExpiresAt < DateTime.UtcNow)
        {
            return new AuthResponseDto
            {
                Success = false,
                Message = "Refresh token không hợp lệ hoặc đã hết hạn"
            };
        }

        var user = tokenEntity.User;

        // Generate new tokens
        var newAccessToken = _tokenService.GenerateAccessToken(user);
        var newRefreshToken = _tokenService.GenerateRefreshToken();

        // Revoke old token
        tokenEntity.IsRevoked = true;
        tokenEntity.RevokedAt = DateTime.UtcNow;
        tokenEntity.ReplacedByToken = newRefreshToken;
        await _refreshTokenRepository.UpdateAsync(tokenEntity);

        // Save new refresh token
        var newRefreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(
                int.Parse(_configuration["JwtSettings:RefreshTokenExpirationDays"]!)),
            CreatedAt = DateTime.UtcNow,
            IsRevoked = false
        };

        await _refreshTokenRepository.CreateAsync(newRefreshTokenEntity);

        return new AuthResponseDto
        {
            Success = true,
            Message = "Làm mới token thành công",
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role,
                IsEmailVerified = user.IsEmailVerified,
                ProfileImageUrl = user.ProfileImageUrl
            }
        };
    }

    public async Task<bool> RevokeTokenAsync(string refreshToken)
    {
        var tokenEntity = await _refreshTokenRepository.GetByTokenAsync(refreshToken);

        if (tokenEntity == null || tokenEntity.IsRevoked)
        {
            return false;
        }

        tokenEntity.IsRevoked = true;
        tokenEntity.RevokedAt = DateTime.UtcNow;
        await _refreshTokenRepository.UpdateAsync(tokenEntity);

        return true;
    }

    public async Task<bool> ResendOtpAsync(string email)
    {
        var user = await _userRepository.GetByEmailAsync(email);

        if (user == null)
        {
            return false;
        }

        // Invalidate all previous OTPs
        await _otpRepository.InvalidateAllUserOtpsAsync(user.Id, "Registration");

        // Generate and send new OTP
        var otpCode = _otpService.GenerateOtp();
        var otpExpirationMinutes = int.Parse(_configuration["OtpSettings:ExpirationMinutes"]!);

        var otp = new OtpCode
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Code = otpCode,
            Purpose = "Registration",
            ExpiresAt = DateTime.UtcNow.AddMinutes(otpExpirationMinutes),
            CreatedAt = DateTime.UtcNow,
            IsUsed = false
        };

        await _otpRepository.CreateAsync(otp);
        return await _otpService.SendOtpEmailAsync(user.Email, otpCode, user.FullName);
    }

    public async Task<bool> ForgotPasswordAsync(ForgotPasswordDto forgotPasswordDto)
    {
        var user = await _userRepository.GetByEmailAsync(forgotPasswordDto.Email);

        if (user == null)
        {
            // Không tiết lộ thông tin user có tồn tại hay không (security best practice)
            return true;
        }

        // Không cho phép reset password cho tài khoản Google-only
        if (user.GoogleId != null && user.PasswordHash == null)
        {
            return false;
        }

        // Invalidate all previous password reset OTPs
        await _otpRepository.InvalidateAllUserOtpsAsync(user.Id, "PasswordReset");

        // Generate and send new OTP
        var otpCode = _otpService.GenerateOtp();
        var otpExpirationMinutes = int.Parse(_configuration["OtpSettings:ExpirationMinutes"]!);

        var otp = new OtpCode
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Code = otpCode,
            Purpose = "PasswordReset",
            ExpiresAt = DateTime.UtcNow.AddMinutes(otpExpirationMinutes),
            CreatedAt = DateTime.UtcNow,
            IsUsed = false
        };

        await _otpRepository.CreateAsync(otp);
        return await _otpService.SendPasswordResetOtpEmailAsync(user.Email, otpCode, user.FullName);
    }

    public async Task<AuthResponseDto> ResetPasswordAsync(ResetPasswordDto resetPasswordDto)
    {
        var user = await _userRepository.GetByEmailAsync(resetPasswordDto.Email);

        if (user == null)
        {
            return new AuthResponseDto
            {
                Success = false,
                Message = "Người dùng không tồn tại"
            };
        }

        // Không cho phép reset password cho tài khoản Google-only
        if (user.GoogleId != null && user.PasswordHash == null)
        {
            return new AuthResponseDto
            {
                Success = false,
                Message = "Tài khoản này đăng nhập bằng Google, không thể đặt lại mật khẩu"
            };
        }

        var otp = await _otpRepository.GetValidOtpAsync(user.Id, resetPasswordDto.OtpCode, "PasswordReset");

        if (otp == null)
        {
            return new AuthResponseDto
            {
                Success = false,
                Message = "Mã OTP không hợp lệ hoặc đã hết hạn"
            };
        }

        // Mark OTP as used
        otp.IsUsed = true;
        otp.UsedAt = DateTime.UtcNow;
        await _otpRepository.UpdateAsync(otp);

        // Update password
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(resetPasswordDto.NewPassword);
        user.LastLoginAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);

        // Revoke all existing refresh tokens for security
        await _refreshTokenRepository.RevokeAllUserTokensAsync(user.Id);

        // Generate new tokens
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();

        // Save new refresh token
        var refreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(
                int.Parse(_configuration["JwtSettings:RefreshTokenExpirationDays"]!)),
            CreatedAt = DateTime.UtcNow,
            IsRevoked = false
        };

        await _refreshTokenRepository.CreateAsync(refreshTokenEntity);

        return new AuthResponseDto
        {
            Success = true,
            Message = "Đặt lại mật khẩu thành công",
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role,
                IsEmailVerified = user.IsEmailVerified,
                ProfileImageUrl = user.ProfileImageUrl
            }
        };
    }

    public async Task<AuthResponseDto> ChangePasswordAsync(Guid userId, ChangePasswordDto changePasswordDto)
    {
        var user = await _userRepository.GetByIdAsync(userId);

        if (user == null)
        {
            return new AuthResponseDto
            {
                Success = false,
                Message = "Người dùng không tồn tại"
            };
        }

        // Không cho phép đổi password cho tài khoản Google-only
        if (user.GoogleId != null && user.PasswordHash == null)
        {
            return new AuthResponseDto
            {
                Success = false,
                Message = "Tài khoản này đăng nhập bằng Google, không có mật khẩu để thay đổi"
            };
        }

        // Verify current password
        if (!BCrypt.Net.BCrypt.Verify(changePasswordDto.CurrentPassword, user.PasswordHash))
        {
            return new AuthResponseDto
            {
                Success = false,
                Message = "Mật khẩu hiện tại không chính xác"
            };
        }

        // Check if new password is same as current password
        if (BCrypt.Net.BCrypt.Verify(changePasswordDto.NewPassword, user.PasswordHash))
        {
            return new AuthResponseDto
            {
                Success = false,
                Message = "Mật khẩu mới không được trùng với mật khẩu hiện tại"
            };
        }

        // Update password
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(changePasswordDto.NewPassword);
        await _userRepository.UpdateAsync(user);

        // Revoke all existing refresh tokens for security (logout from all devices)
        await _refreshTokenRepository.RevokeAllUserTokensAsync(user.Id);

        // Generate new tokens
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();

        // Save new refresh token
        var refreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(
                int.Parse(_configuration["JwtSettings:RefreshTokenExpirationDays"]!)),
            CreatedAt = DateTime.UtcNow,
            IsRevoked = false
        };

        await _refreshTokenRepository.CreateAsync(refreshTokenEntity);

        return new AuthResponseDto
        {
            Success = true,
            Message = "Đổi mật khẩu thành công",
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role,
                IsEmailVerified = user.IsEmailVerified,
                ProfileImageUrl = user.ProfileImageUrl
            }
        };
    }
}
