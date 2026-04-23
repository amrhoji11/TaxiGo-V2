using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.Models;

namespace TaxiApp.Backend.Core
{
    public class JwtService
    {
        private readonly IConfiguration _configuration;

        public JwtService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        //  قمنا بتعديل الدالة لتستقبل الـ role بشكل ديناميكي
        public string GenerateToken(ApplicationUser user, string role)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.UserName ?? ""),
                new Claim(ClaimTypes.MobilePhone, user.PhoneNumber ?? ""),
                new Claim(ClaimTypes.Role, role), //  الآن نضع الدور الحقيقي هنا
               new Claim(ClaimTypes.NameIdentifier, user.Id),
            };

            // جلب المفتاح السري من الإعدادات
            var secretKey = _configuration["JWT:Secret"];
            if (string.IsNullOrEmpty(secretKey))
                throw new Exception("JWT Secret is missing in configuration!");

            var symmetricSecurityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var signingCredentials = new SigningCredentials(symmetricSecurityKey, SecurityAlgorithms.HmacSha256);

            var jwtToken = new JwtSecurityToken(
                issuer: _configuration["JWT:ValidIssuer"], // يفضل إضافة المصدر
                audience: _configuration["JWT:ValidAudience"], // والمستقبل
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: signingCredentials
            );

            return new JwtSecurityTokenHandler().WriteToken(jwtToken);
        }
    }
}