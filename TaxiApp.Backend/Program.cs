using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TaxiApp.Backend.Core.Interfaces;
using TaxiApp.Backend.Core.Models;
using TaxiApp.Backend.Infrastructure.Data;
using TaxiApp.Backend.Infrastructure.Repositories;
using TaxiApp.Backend.Infrastructure;
using TaxiApp.Backend.Core;
using TaxiApp.Backend.Infrastructure.Helper;
using TaxiApp.Backend.Core.Settings;
using TaxiApp.Backend.Core.Settings;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace TaxiApp.Backend
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 1. الأساسيات والـ Controllers
            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
                });

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddOpenApi();
            builder.Services.AddHttpContextAccessor();

            // 2. إعداد قاعدة البيانات
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            // 3. إعداد الـ Identity
            builder.Services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequireDigit = false;
                options.Password.RequiredLength = 1;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;

                options.Tokens.AuthenticatorTokenProvider = TokenOptions.DefaultPhoneProvider;
                options.Tokens.PasswordResetTokenProvider = TokenOptions.DefaultEmailProvider;
                options.Tokens.ChangePhoneNumberTokenProvider = TokenOptions.DefaultPhoneProvider;
            })
            .AddRoles<IdentityRole>()
            .AddSignInManager<SignInManager<ApplicationUser>>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

            builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
            {
                options.TokenLifespan = TimeSpan.FromMinutes(2);
            });

            // 4. إعدادات الـ Authentication والـ JWT
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    ValidAudience = builder.Configuration["JWT:ValidAudience"],
                    ValidIssuer = builder.Configuration["JWT:ValidIssuer"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JWT:Secret"]))
                };


                // 👇 أضف هذا الجزء (مهم جداً)
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];

                        var path = context.HttpContext.Request.Path;

                        if (!string.IsNullOrEmpty(accessToken) &&
                            path.StartsWithSegments("/notificationHub"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

            builder.Services.AddAuthorization();

            // 5. تسجيل الخدمات (تم التأكد من IDriverAssignmentRepository)
            builder.Services.AddScoped<IOrderRepository, OrderRepository>();
            builder.Services.AddScoped<IAuthRepository, AuthRepository>();
            builder.Services.AddScoped<IVehicleRepository, VehicleRepository>();
            builder.Services.AddScoped<IUserBlockRepository, UserBlockRepository>();
            builder.Services.AddScoped<IUserRepository, UserRepository>();
            builder.Services.AddScoped<IDriverRepository, DriverRepository>();
            builder.Services.AddScoped<IDriverAssignmentRepository, DriverAssignmentRepository>();
            builder.Services.AddScoped<IPassengerRepository, PassengerRepository>();
            builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
            builder.Services.AddScoped<IDriverApprovalRepository, DriverApprovalRepository>();
            builder.Services.AddScoped<JwtService>();
            builder.Services.AddScoped<IDriverTrackingRepository, DriverTrackingRepository>();
            builder.Services.AddScoped<OrderService>();
            builder.Services.AddScoped<IComplaintRepository, ComplaintRepository>();
            builder.Services.AddScoped<ISettingsRepository, SettingsRepository>();
            builder.Services.AddScoped<IFavoriteLocationsRepository, FavoriteLocationsRepository>();
            builder.Services.AddScoped<IMessageRepository, MessageRepository>();
            builder.Services.AddScoped<IAdminRepository, AdminRepository>();
            builder.Services.AddScoped<IAdminAssignmentRepository, AdminAssignmentRepository>();
            builder.Services.AddScoped<TripRoutingService>();
            builder.Services.AddScoped<ISmsService, SmsService>();


            builder.Services.AddSingleton<ActiveTripStore>();
            builder.Services.AddSingleton<IEtaCacheService, EtaCacheService>();
            builder.Services.AddMemoryCache();
            builder.Services.AddSignalR(options =>
            {
                options.EnableDetailedErrors = true; // 👈 أضف هذا السطر لرؤية الخطأ الحقيقي في الـ Console الخاص بالمتصفح
                options.ClientTimeoutInterval = TimeSpan.FromMinutes(2);  // وقت السيرفر يعتبر فيه الاتصال ميت
                options.KeepAliveInterval = TimeSpan.FromSeconds(15);    // ping كل 15 ثانية
            });

            // Hosted Services
            builder.Services.AddHostedService<RefreshTokenCleanupService>();
            builder.Services.AddHostedService<DatabaseCleanupService>();
            builder.Services.AddHostedService<TripOfferBackgroundService>();
           builder.Services.AddHttpClient<IMapService, GoogleMapService>();
            builder.Services.Configure<TaxiSettings>(builder.Configuration.GetSection("TaxiSettings"));

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy
                        .SetIsOriginAllowed(_ => true) // مهم جداً
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });


            builder.Services.AddRateLimiter(options =>
            {
                // 🔒 Login (حسب IP)
                options.AddPolicy("LoginPolicy", context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 5,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0
                        }));


                // 🔐 Verify OTP (مهم جداً)
                options.AddPolicy("VerifyOtpPolicy", context =>
                {
                    var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                    var partitionKey = userId
                        ?? context.Connection.RemoteIpAddress?.ToString()
                        ?? "unknown";

                    return RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey,
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 5,
                            Window = TimeSpan.FromMinutes(5),
                            QueueLimit = 0
                        });
                });


                options.AddPolicy("ChangePhonePolicy", context =>
                {
                    var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? context.Connection.RemoteIpAddress?.ToString()
                        ?? "unknown";

                    return RateLimitPartition.GetFixedWindowLimiter(
                        userId,
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 3,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0
                        });
                });


                // 🚕 Driver actions (Accept + Reject مع بعض)
                options.AddPolicy("DriverActionsPolicy", context =>
                {
                    var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                    if (string.IsNullOrEmpty(userId))
                        userId = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                    return RateLimitPartition.GetFixedWindowLimiter(
                        userId,
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 4, // مجموع العمليات
                            Window = TimeSpan.FromSeconds(20),
                            QueueLimit = 0
                        });
                });

                // رسالة عند الرفض
                options.OnRejected = async (context, token) =>
                {
                    context.HttpContext.Response.StatusCode = 429;
                    await context.HttpContext.Response.WriteAsync("Too many requests 🚫");
                };
            });

            var app = builder.Build();

            // 6. تشغيل الـ Seeding (تم إصلاح الأقواس هنا بدقة)
            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
                string[] roleNames = { "SuperAdmin", "Admin", "Driver", "Passenger" };
                foreach (var roleName in roleNames)
                {
                    if (!await roleManager.RoleExistsAsync(roleName))
                    {
                        await roleManager.CreateAsync(new IdentityRole(roleName));
                    }
                }
                await DbSeeder.SeedAdminAsync(services);
            } // إغلاق الـ using

            // 7. إعدادات الـ Middleware
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseHttpsRedirection();
            app.UseCors("AllowAll");

            // Serves uploaded profile/vehicle/document photos (saved by FileService to
            // the content-root "Images" folder) at /images/{fileName}. There was no
            // static-file middleware registered at all before this, so every uploaded
            // image was unreachable over HTTP.
            var imagesPath = Path.Combine(builder.Environment.ContentRootPath, "Images");
            Directory.CreateDirectory(imagesPath);
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(imagesPath),
                RequestPath = "/images"
            });

            app.UseAuthentication();
            app.UseRateLimiter();
            app.UseAuthorization();

            app.MapHub<NotificationHub>("/notificationHub").RequireCors("AllowAll"); ;
            app.MapControllers();

            app.Run();
        }
    }
}