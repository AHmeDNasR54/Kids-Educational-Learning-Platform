using Ecommerce.DataAccess.ApplicationContext;
using Ecommerce.DataAccess.Services.Email;
//using Ecommerce.DataAccess.Services.ImageUploading;
using Ecommerce.DataAccess.Services.OTP;
using Ecommerce.DataAccess.Services.Token;
using Ecommerce.Entities.DTO.Account.Auth;
using Ecommerce.Entities.DTO.Account.Auth.Login;
using Ecommerce.Entities.DTO.Account.Auth.Register;
using Ecommerce.Entities.DTO.Account.Auth.ResetPassword;
using Ecommerce.Entities.Models.Auth.Identity;
using Ecommerce.Entities.Models.Auth.Users;
using Ecommerce.Entities.Shared.Bases;
using Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Security.Claims;

using LoginRequest = Ecommerce.Entities.DTO.Account.Auth.Login.LoginRequest;
using ResetPasswordRequest = Ecommerce.Entities.DTO.Account.Auth.ResetPassword.ResetPasswordRequest;

namespace Ecommerce.DataAccess.Services.Auth
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IOTPService _otpService;
        private readonly ITokenStoreService _tokenStoreService;
        //private readonly IImageUploadService _imageUploading;
        private readonly ILogger<AuthService> _logger;
        private readonly ResponseHandler _responseHandler;

        public AuthService(UserManager<User> userManager,
                           ApplicationDbContext context,
                           IEmailService emailService,
                           IOTPService otpService,
                           ResponseHandler responseHandler,
                           ITokenStoreService tokenStoreService,
                           ILogger<AuthService> logger
                           /*IImageUploadService imageUploading*/)
        {
            _userManager = userManager;
            _context = context;
            _emailService = emailService;
            _otpService = otpService;
            _responseHandler = responseHandler;
            _tokenStoreService = tokenStoreService;
            _logger = logger;
            //_imageUploading = imageUploading;
        }

        public async Task<Response<LoginResponse>> LoginAsync(LoginRequest loginRequest)
        {
            try
            {
                User? user = await FindUserByEmailOrPhoneAsync(loginRequest.Email);

                if (user == null)
                    return _responseHandler.NotFound<LoginResponse>("User not found.");

                if (!await _userManager.CheckPasswordAsync(user, loginRequest.Password))
                    return _responseHandler.BadRequest<LoginResponse>("Invalid password.");

                if (!user.EmailConfirmed)
                    return _responseHandler.BadRequest<LoginResponse>("Email is not verified. Please verify your email first.");

                var roles = await _userManager.GetRolesAsync(user);

                var tokens = await _tokenStoreService.GenerateAndStoreTokensAsync(user.Id, user);

                var response = new LoginResponse
                {
                    Id = user.Id,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    Role = roles.FirstOrDefault(),
                    IsEmailConfirmed = user.EmailConfirmed,
                    AccessToken = tokens.AccessToken,
                    RefreshToken = tokens.RefreshToken,
                };

                return _responseHandler.Success(response, "Login successful.");
            }
            catch (RedisConnectionException)
            {
                _logger.LogError("Check Your Internet Connection");
                return _responseHandler.BadRequest<LoginResponse>("Check Your Internet Connection");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error {ex}", ex);
                return _responseHandler.BadRequest<LoginResponse>("An error occurred during Login.");
            }
        }

        public async Task<Response<RegisterResponse>> RegisterAsync(RegisterRequest registerRequest)
        {
            _logger.LogInformation("RegisterAsync started for Email: {Email}", registerRequest.Email);

            var emailPhoneCheck = await CheckIfEmailOrPhoneExists(registerRequest.Email, registerRequest.PhoneNumber);
            if (emailPhoneCheck != null)
            {
                _logger.LogWarning("Registration failed: {Reason}", emailPhoneCheck);
                return _responseHandler.BadRequest<RegisterResponse>(emailPhoneCheck);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var user = new User
                {
                    UserName = registerRequest.Email,
                    Email = registerRequest.Email,
                    PhoneNumber = registerRequest.PhoneNumber,
                };

                var createUserResult = await _userManager.CreateAsync(user, registerRequest.Password);
                if (!createUserResult.Succeeded)
                {
                    var errors = createUserResult.Errors.Select(e => e.Description).ToList();
                    return _responseHandler.BadRequest<RegisterResponse>(string.Join(", ", errors));
                }

                await _userManager.AddToRoleAsync(user, "USER");

                var tokens = await _tokenStoreService.GenerateAndStoreTokensAsync(user.Id, user);

                var otp = await _otpService.GenerateAndStoreOtpAsync(user.Id);

                await _emailService.SendOtpEmailAsync(user, otp);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var response = new RegisterResponse
                {
                    Email = registerRequest.Email,
                    Id = user.Id,
                    IsEmailConfirmed = false,
                    PhoneNumber = registerRequest.PhoneNumber,
                    Role = "USER",
                    AccessToken = tokens.AccessToken,
                    RefreshToken = tokens.RefreshToken
                };

                return _responseHandler.Created(response, "User registered successfully. Please check your email to receive the OTP.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error occurred during RegisterAsync for Email: {Email}", registerRequest.Email);
                return _responseHandler.BadRequest<RegisterResponse>("An error occurred during registration.");
            }
        }

        // ==========================
        // Parent Register
        // ==========================
        public async Task<Response<ParentRegisterResponse>> ParentRegisterAsync(ParentRegisterRequest registerRequest)
        {
            _logger.LogInformation("ParentRegisterAsync started for Email: {Email}", registerRequest.Email);

            var emailPhoneCheck = await CheckIfEmailOrPhoneExists(registerRequest.Email, registerRequest.PhoneNumber);
            if (emailPhoneCheck != null)
            {
                _logger.LogWarning("Registration failed: {Reason}", emailPhoneCheck);
                return _responseHandler.BadRequest<ParentRegisterResponse>(emailPhoneCheck);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var user = new User
                {
                    Id = Guid.NewGuid().ToString(),

                    UserName = registerRequest.Email,
                    Email = registerRequest.Email,
                    PhoneNumber = registerRequest.PhoneNumber,
                    EmailConfirmed = true,
                    PhoneNumberConfirmed = true
                };

                var createUserResult = await _userManager.CreateAsync(user, registerRequest.Password);
                if (!createUserResult.Succeeded)
                {
                    var errors = createUserResult.Errors.Select(e => e.Description).ToList();
                    return _responseHandler.BadRequest<ParentRegisterResponse>(string.Join(", ", errors));
                }

                await _userManager.AddToRoleAsync(user, "parent");

                //string? imageUploaded = registerRequest.ProfileImageUrl != null
                //    ? await _imageUploading.UploadAsync(registerRequest.ProfileImageUrl)
                //    : null;

                var parent = new Parent
                {

                    User = user,
                    FullName = registerRequest.FullName,
                    //ProfileImageUrl = imageUploaded,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                    Id = user.Id,
                    UserId = user.Id,
                    
                };

                await _context.Parent.AddAsync(parent);
                var tokens = await _tokenStoreService.GenerateAndStoreTokensAsync(user.Id, user);

                //var otp = await _otpService.GenerateAndStoreOtpAsync(user.Id);

                //await _emailService.SendOtpEmailAsync(user, otp);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var response = new ParentRegisterResponse
                {
                    Id = user.Id,
                    Email = registerRequest.Email,
                    IsEmailConfirmed = false,
                    PhoneNumber = registerRequest.PhoneNumber,
                    Role = "PARENT",
                    FullName = registerRequest.FullName,
                    //ProfileImageUrl = imageUploaded,
                    AccessToken = tokens.AccessToken,
                    RefreshToken = tokens.RefreshToken
                };

                return _responseHandler.Created(response, "Parent registered successfully. Please check your email to receive the OTP.");
            }
            catch (RedisConnectionException)
            {
                _logger.LogError("Check Your Internet Connection");
                return _responseHandler.BadRequest<ParentRegisterResponse>("Check Your Internet Connection");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error occurred during ParentRegisterAsync for Email: {Email}", registerRequest.Email);
                return _responseHandler.BadRequest<ParentRegisterResponse>("An error occurred during registration.");
            }
        }

        // ==========================
        // Teacher Register
        // ==========================
        public async Task<Response<TeacherRegisterResponse>> TeacherRegisterAsync(TeacherRegisterRequest registerRequest)
        {
            _logger.LogInformation("TeacherRegisterAsync started for Email: {Email}", registerRequest.Email);

            var emailPhoneCheck = await CheckIfEmailOrPhoneExists(registerRequest.Email, registerRequest.PhoneNumber);
            if (emailPhoneCheck != null)
            {
                _logger.LogWarning("Registration failed: {Reason}", emailPhoneCheck);
                return _responseHandler.BadRequest<TeacherRegisterResponse>(emailPhoneCheck);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var user = new User
                {
                    Id = Guid.NewGuid().ToString(),
                    UserName = registerRequest.Email,
                    Email = registerRequest.Email,
                    PhoneNumber = registerRequest.PhoneNumber,
                    EmailConfirmed = true,
                    PhoneNumberConfirmed = true
                };

                var createUserResult = await _userManager.CreateAsync(user, registerRequest.Password);
                if (!createUserResult.Succeeded)
                {
                    var errors = createUserResult.Errors.Select(e => e.Description).ToList();
                    return _responseHandler.BadRequest<TeacherRegisterResponse>(string.Join(", ", errors));
                }

                await _userManager.AddToRoleAsync(user, "teacher");

                var teacher = new Teacher
                {
                    User = user,
                    FullName = registerRequest.FullName,
                    Bio = registerRequest.Bio,
                    Country = registerRequest.Country,
                    HourlyRate = registerRequest.HourlyRate,
                    IsVerified = true,
                    JoinDate = DateTime.Now,
                    Id = user.Id,
                    UserId = user.Id,
                };

                await _context.Teacher.AddAsync(teacher);

                var tokens = await _tokenStoreService.GenerateAndStoreTokensAsync(user.Id, user);

                //var otp = await _otpService.GenerateAndStoreOtpAsync(user.Id);

                //await _emailService.SendOtpEmailAsync(user, otp);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var response = new TeacherRegisterResponse
                {
                    Id = user.Id,
                    Email = registerRequest.Email,
                    IsEmailConfirmed = false,
                    PhoneNumber = registerRequest.PhoneNumber,
                    Role = "TEACHER",
                    FullName = registerRequest.FullName,
                    Bio = registerRequest.Bio,
                    Country = registerRequest.Country,
                    HourlyRate = registerRequest.HourlyRate,
                    AccessToken = tokens.AccessToken,
                    RefreshToken = tokens.RefreshToken
                };

                return _responseHandler.Created(response, "Teacher registered successfully. Please check your email to receive the OTP.");
            }
            catch (RedisConnectionException)
            {
                _logger.LogError("Check Your Internet Connection");
                return _responseHandler.BadRequest<TeacherRegisterResponse>("Check Your Internet Connection");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error occurred during TeacherRegisterAsync for Email: {Email}", registerRequest.Email);
                return _responseHandler.BadRequest<TeacherRegisterResponse>("An error occurred during registration.");
            }
        }

        public async Task<Response<ForgetPasswordResponse>> ForgotPasswordAsync(ForgetPasswordRequest model)
        {
            _logger.LogInformation("Starting ForgotPasswordAsync for Email: {Email}", model.Email);

            User? user = await FindUserByEmailOrPhoneAsync(model.Email);

            if (user == null)
                return _responseHandler.NotFound<ForgetPasswordResponse>("User not found.");

            try
            {
                var otp = await _otpService.GenerateAndStoreOtpAsync(user.Id);
                await _emailService.SendOtpEmailAsync(user, otp);
            }
            catch (RedisConnectionException)
            {
                _logger.LogError("Check Your Internet Connection");
                return _responseHandler.BadRequest<ForgetPasswordResponse>("Check Your Internet Connection");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send OTP email to user ID: {UserId}", user.Id);
                return _responseHandler.BadRequest<ForgetPasswordResponse>("Failed to send OTP.");
            }

            var response = new ForgetPasswordResponse
            {
                UserId = user.Id
            };

            return _responseHandler.Success(response, "OTP sent to your email. Please use it to reset your password.");
        }

        public async Task<Response<ResetPasswordResponse>> ResetPasswordAsync(ResetPasswordRequest model)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(model.UserId);
                if (user == null)
                    return _responseHandler.NotFound<ResetPasswordResponse>("User not found.");

                var result = await _userManager.ResetPasswordAsync(user, model.token, model.NewPassword);
                if (!result.Succeeded)
                {
                    var errors = result.Errors.Select(e => e.Description).ToList();
                    return _responseHandler.BadRequest<ResetPasswordResponse>(string.Join(", ", errors));
                }

                await _tokenStoreService.InvalidateOldTokensAsync(user.Id);

                var roles = await _userManager.GetRolesAsync(user);

                var response = new ResetPasswordResponse
                {
                    UserId = user.Id,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    Role = roles.FirstOrDefault() ?? "USER"
                };

                return _responseHandler.Success(response, "Password reset successfully. Please log in with your new password.");
            }
            catch (RedisConnectionException)
            {
                _logger.LogError("Check Your Internet Connection");
                return _responseHandler.BadRequest<ResetPasswordResponse>("Check Your Internet Connection");
            }
            catch
            {
                return _responseHandler.BadRequest<ResetPasswordResponse>("An error occurred during reset password.");
            }
        }

        public async Task<Response<bool>> VerifyEmailAsync(VerifyOtpRequest verifyOtpRequest)
        {
            var user = await _userManager.FindByIdAsync(verifyOtpRequest.UserId);
            if (user == null)
                return _responseHandler.NotFound<bool>("User not found.");

            if (user.EmailConfirmed)
                return _responseHandler.Success(true, "Email is already verified.");

            var isOtpValid = await _otpService.ValidateOtpAsync(verifyOtpRequest.UserId, verifyOtpRequest.Otp);
            if (!isOtpValid)
                return _responseHandler.BadRequest<bool>("Invalid or expired OTP.");

            user.EmailConfirmed = true;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return _responseHandler.BadRequest<bool>("Failed to update user confirmation status.");

            return _responseHandler.Success(true, "Email verified successfully.");
        }

        public async Task<Response<VerifyResetPasswordResponse>> VerifyResetPasswordAsync(VerifyOtpRequest verifyOtpRequest)
        {
            var user = await _userManager.FindByIdAsync(verifyOtpRequest.UserId);
            if (user == null)
                return _responseHandler.NotFound<VerifyResetPasswordResponse>("User not found.");

            var isOtpValid = await _otpService.ValidateOtpAsync(verifyOtpRequest.UserId, verifyOtpRequest.Otp);
            if (!isOtpValid)
                return _responseHandler.BadRequest<VerifyResetPasswordResponse>("Invalid or expired OTP.");

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            var response = new VerifyResetPasswordResponse
            {
                token = token
            };

            return _responseHandler.Success(response, "Otp Is Verified");
        }

        public async Task<Response<string>> ResendOtpAsync(ResendOtpRequest resendOtpRequest)
        {
            var user = await _userManager.FindByIdAsync(resendOtpRequest.UserId);
            if (user == null)
                return _responseHandler.NotFound<string>("User not found.");

            var otp = await _otpService.GenerateAndStoreOtpAsync(user.Id);

            await _emailService.SendOtpEmailAsync(user, otp);

            return _responseHandler.Success<string>(null, "OTP resent successfully. Please check your email.");
        }

        public async Task<RefreshTokenResponse> RefreshTokenAsync(string refreshToken)
        {
            _logger.LogInformation("Starting RefreshTokenAsync");

            var isValid = await _tokenStoreService.IsValidAsync(refreshToken);
            if (!isValid)
                throw new SecurityTokenException("Invalid refresh token");

            var tokenEntry = await _context.UserRefreshTokens
                .FirstOrDefaultAsync(r => r.Token == refreshToken);

            if (tokenEntry == null)
                throw new SecurityTokenException("Invalid refresh token");

            var user = await _userManager.FindByIdAsync(tokenEntry.UserId.ToString());
            if (user == null)
                throw new SecurityTokenException("Invalid user");

            await _tokenStoreService.InvalidateOldTokensAsync(user.Id);

            var userTokens = await _tokenStoreService.GenerateAndStoreTokensAsync(user.Id, user);

            return new RefreshTokenResponse
            {
                AccessToken = userTokens.AccessToken,
                RefreshToken = userTokens.RefreshToken
            };
        }

        public async Task<Response<string>> LogoutAsync(ClaimsPrincipal userClaims)
        {
            try
            {
                var userId = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return _responseHandler.Unauthorized<string>("User not authenticated");

                await _tokenStoreService.InvalidateOldTokensAsync(userId);

                return _responseHandler.Success<string>(null, "Logged out successfully");
            }
            catch (Exception ex)
            {
                return _responseHandler.ServerError<string>($"An error occurred during logout: {ex.Message}");
            }
        }

        public async Task<Response<string>> ChangePasswordAsync(ClaimsPrincipal userClaims, ChangePasswordRequest request)
        {
            try
            {
                var userId = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return _responseHandler.Unauthorized<string>("User not authenticated");

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                    return _responseHandler.NotFound<string>("User not found");

                var isCurrentPasswordValid = await _userManager.CheckPasswordAsync(user, request.CurrentPassword);
                if (!isCurrentPasswordValid)
                    return _responseHandler.BadRequest<string>("Current password is incorrect");

                var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return _responseHandler.BadRequest<string>(errors);
                }

                await _tokenStoreService.InvalidateOldTokensAsync(userId);

                return _responseHandler.Success<string>(null, "Password changed successfully. Please login again.");
            }
            catch (Exception ex)
            {
                return _responseHandler.ServerError<string>($"An error occurred while changing password: {ex.Message}");
            }
        }

        // Helpers
        private async Task<string?> CheckIfEmailOrPhoneExists(string email, string? phoneNumber)
        {
            if (await _userManager.FindByEmailAsync(email) != null)
                return "Email is already registered.";

            if (!string.IsNullOrEmpty(phoneNumber) && await _userManager.Users.AnyAsync(u => u.PhoneNumber == phoneNumber))
                return "Phone number is already registered.";

            return null;
        }

        private async Task<User?> FindUserByEmailOrPhoneAsync(string email)
        {
            if (!string.IsNullOrEmpty(email))
                return await _userManager.FindByEmailAsync(email);

            return null;
        }

        public async Task<Response<bool>> VerifyOtpAsync(VerifyOtpRequest verifyOtpRequest)
        {
            return await VerifyEmailAsync(verifyOtpRequest);
        }
    }
}