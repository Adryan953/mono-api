using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MercadoPago.Client.Preference;
using MercadoPago.Config;
using MercadoPago.Resource.Preference;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mono.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public PaymentController(IConfiguration configuration)
        {
            _configuration = configuration;
            MercadoPagoConfig.AccessToken = _configuration["MercadoPago:AccessToken"];
        }

        [HttpPost("create_preference")]
        public async Task<IActionResult> CreatePreference()
        {
            try
            {
                var request = new PreferenceRequest
                {
                    Items = new List<PreferenceItemRequest>
                    {
                        new PreferenceItemRequest
                        {
                            Title = "Assinatura Mono Premium",
                            Quantity = 1,
                            CurrencyId = "BRL",
                            UnitPrice = 19.90m,
                        }
                    },
                    BackUrls = new PreferenceBackUrlsRequest
                    {
                        Success = "https://mono-api-j17s.onrender.com/success.html",
                        Failure = "https://mono-api-j17s.onrender.com/failure.html",
                        Pending = "https://mono-api-j17s.onrender.com/pending.html"
                    },
                    AutoReturn = "approved",
                };

                var client = new PreferenceClient();
                Preference preference = await client.CreateAsync(request);

                return Ok(new { PreferenceId = preference.Id, InitPoint = preference.InitPoint });
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
