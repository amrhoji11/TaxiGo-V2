using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.DTO_S.AuthDto;
using TaxiApp.Backend.Core.DTO_S.AuthDto.Requests;
using TaxiApp.Backend.Core.DTO_S.AuthDto.Responses;

namespace TaxiApp.Backend.Core.Interfaces
{
    public interface IAuthRepository
    {
        Task<string> LoginAsync(LoginRequest request);
        Task<LoginResponse> VerifyOtpAndLoginAsync(VerifyOtpRequest request);
        Task<RegisterPassengerResponse> RegisterPassengerAsync(RegisterPassengerRequest request);
        Task<RegisterDriverResponse> RegisterDriverAsync(RegisterDriverRequest request);


        Task<string> RequestChangePhoneNumberAsync(string userId, string countryCode, string phoneNumber);
        Task<bool> ConfirmChangePhoneNumberAsync(string userId, string countryCode, string phoneNumber, string otp);

        Task<LoginResponse?> RefreshTokenAsync(string refreshToken);
        Task<bool> RevokeRefreshTokenAsync(string userId, string refreshToken);
    }
}
