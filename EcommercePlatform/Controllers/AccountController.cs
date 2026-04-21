using Ecommerce.DataAccess.Services.Auth;
using Ecommerce.DataAccess.Services.OAuth;
using Ecommerce.Entities.DTO.Account.Auth;
using Ecommerce.Entities.DTO.Account.Auth.Login;
using Ecommerce.Entities.DTO.Account.Auth.Register;
using Ecommerce.Entities.DTO.Account.Auth.ResetPassword;
using Ecommerce.Entities.Shared.Bases;
using Ecommerce.Utilities.Exceptions;

using FluentValidation;
using FluentValidation.Results;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

namespace Ecommerce.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ResponseHandler _responseHandler;

        private readonly IValidator<RegisterRequest> _registerValidator;
        private readonly IValidator<LoginRequest> _loginValidator;
        private readonly IValidator<ForgetPasswordRequest> _forgetPasswordValidator;
        private readonly IValidator<ResetPasswordRequest> _resetPasswordValidator;
        private readonly IValidator<ChangePasswordRequest> _changePasswordValidator;

        private readonly IAuthGoogleService _authGoogleService;

        public AccountController(
            IAuthService authService,
            ResponseHandler responseHandler,
            IValidator<RegisterRequest> registerValidator,
            IValidator<LoginRequest> loginValidator,
            IValidator<ForgetPasswordRequest> forgetPasswordValidator,
            IValidator<ResetPasswordRequest> resetPasswordValidator,
            IAuthGoogleService authGoogleService,
            IValidator<ChangePasswordRequest> changePasswordValidator)
        {
            _authService = authService;
            _responseHandler = responseHandler;
            _registerValidator = registerValidator;
            _loginValidator = loginValidator;
            _forgetPasswordValidator = forgetPasswordValidator;
            _resetPasswordValidator = resetPasswordValidator;
            _authGoogleService = authGoogleService;
            _changePasswordValidator = changePasswordValidator;
        }

        // ================= LOGIN =================
        [HttpPost("login")]
        public async Task<ActionResult<Response<LoginResponse>>> Login([FromBody] LoginRequest request)
        {
            var validation = await _loginValidator.ValidateAsync(request);
            if (!validation.IsValid)
                return BadRequest(_responseHandler.BadRequest<object>(
                    string.Join(", ", validation.Errors.Select(e => e.ErrorMessage))));

            var response = await _authService.LoginAsync(request);
            return StatusCode((int)response.StatusCode, response);
        }

        // ================= GOOGLE LOGIN =================
        //[HttpPost("login/google")]
        //public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
        //{
        //    if (!ModelState.IsValid)
        //        return _responseHandler.HandleModelStateErrors(ModelState);

        //    try
        //    {
        //        var token = await _authGoogleService.AuthenticateWithGoogleAsync(request.IdToken);
        //        return Ok(_responseHandler.Success(token, "Google login successful"));
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, _responseHandler.ServerError<string>(ex.Message));
        //    }
        //}

        // ================= REGISTER (NORMAL USER) =================
        //[HttpPost("register")]
        //public async Task<ActionResult<Response<RegisterResponse>>> Register([FromForm] RegisterRequest request)
        //{
        //    var validation = await _registerValidator.ValidateAsync(request);
        //    if (!validation.IsValid)
        //        return BadRequest(_responseHandler.BadRequest<object>(
        //            string.Join(", ", validation.Errors.Select(e => e.ErrorMessage))));

        //    var response = await _authService.RegisterAsync(request);
        //    return StatusCode((int)response.StatusCode, response);
        //}

        // ================= REGISTER PARENT =================
        [HttpPost("register/parent")]
        public async Task<IActionResult> RegisterParent([FromForm] ParentRegisterRequest request)
        {
            var response = await _authService.ParentRegisterAsync(request);
            return StatusCode((int)response.StatusCode, response);
        }

        // ================= REGISTER TEACHER =================
        [HttpPost("register/teacher")]
        public async Task<IActionResult> RegisterTeacher([FromForm] TeacherRegisterRequest request)
        {
            var response = await _authService.TeacherRegisterAsync(request);
            return StatusCode((int)response.StatusCode, response);
        }

        // ================= VERIFY OTP =================
        //[HttpPost("verify-otp")]
        //public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
        //{
        //    var response = await _authService.VerifyOtpAsync(request);
        //    return StatusCode((int)response.StatusCode, response);
        //}

        // ================= RESEND OTP =================
        //[HttpPost("resend-otp")]
        //[EnableRateLimiting("SendOtpPolicy")]
        //public async Task<IActionResult> ResendOtp([FromBody] ResendOtpRequest request)
        //{
        //    var response = await _authService.ResendOtpAsync(request);
        //    return StatusCode((int)response.StatusCode, response);
        //}

        // ================= FORGET PASSWORD =================
        //[HttpPost("forget-password")]
        //public async Task<IActionResult> ForgetPassword([FromBody] ForgetPasswordRequest request)
        //{
        //    var validation = await _forgetPasswordValidator.ValidateAsync(request);
        //    if (!validation.IsValid)
        //        return BadRequest(_responseHandler.BadRequest<object>(
        //            string.Join(", ", validation.Errors.Select(e => e.ErrorMessage))));

        //    var response = await _authService.ForgotPasswordAsync(request);
        //    return StatusCode((int)response.StatusCode, response);
        //}

        // ================= VERIFY RESET PASSWORD OTP =================
        //[HttpPost("verify-reset-password")]
        //public async Task<IActionResult> VerifyResetPassword([FromBody] VerifyOtpRequest request)
        //{
        //    var response = await _authService.VerifyResetPasswordAsync(request);
        //    return StatusCode((int)response.StatusCode, response);
        //}

        // ================= RESET PASSWORD =================
        //[HttpPost("reset-password")]
        //public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        //{
        //    var validation = await _resetPasswordValidator.ValidateAsync(request);
        //    if (!validation.IsValid)
        //        return BadRequest(_responseHandler.BadRequest<object>(
        //            string.Join(", ", validation.Errors.Select(e => e.ErrorMessage))));

        //    var response = await _authService.ResetPasswordAsync(request);
        //    return StatusCode((int)response.StatusCode, response);
        //}

        // ================= REFRESH TOKEN =================
        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                return BadRequest(_responseHandler.BadRequest<string>("Refresh token is required"));

            try
            {
                var result = await _authService.RefreshTokenAsync(refreshToken);
                return Ok(_responseHandler.Success(result, "Token refreshed successfully"));
            }
            catch (SecurityTokenException ex)
            {
                return Unauthorized(_responseHandler.Unauthorized<string>(ex.Message));
            }
        }

        // ================= CHANGE PASSWORD =================
        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var validation = await _changePasswordValidator.ValidateAsync(request);
            if (!validation.IsValid)
                return BadRequest(_responseHandler.BadRequest<object>(
                    string.Join(", ", validation.Errors.Select(e => e.ErrorMessage))));

            var response = await _authService.ChangePasswordAsync(User, request);
            return StatusCode((int)response.StatusCode, response);
        }

        // ================= LOGOUT =================
        //[Authorize]
        //[HttpPost("logout")]
        //public async Task<IActionResult> Logout()
        //{
        //    var response = await _authService.LogoutAsync(User);
        //    return StatusCode((int)response.StatusCode, response);
        //}
    }
}