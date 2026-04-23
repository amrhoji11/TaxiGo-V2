using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.Interfaces;

namespace TaxiApp.Backend.Infrastructure.Repositories
{
    public class SmsService : ISmsService
    {
        private readonly IConfiguration _config;

        public SmsService(IConfiguration config)
        {
            _config = config;
        }

        public async Task<bool> SendSms(string to, string message)
        {
            try
            {
                var sid = _config["Twilio:AccountSid"];
                var token = _config["Twilio:AuthToken"];
                var from = _config["Twilio:FromPhone"];

                TwilioClient.Init(sid, token);

           var result = await  MessageResource.CreateAsync(
                    body: message,
                    from: new Twilio.Types.PhoneNumber(from),
                    to: new Twilio.Types.PhoneNumber(to)
                );

                return result.ErrorCode == null;
            }
            catch
            {
                return false;
            }
        }
    }
}