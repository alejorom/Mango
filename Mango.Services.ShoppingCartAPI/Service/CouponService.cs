using Mango.Services.ShoppingCartAPI.Models.Dto;
using Mango.Services.ShoppingCartAPI.Service.IService;
using Newtonsoft.Json;
using System.Net;

namespace Mango.Services.ShoppingCartAPI.Service
{
    public class CouponService : ICouponService
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public CouponService(IHttpClientFactory clientFactory)
        {
            _httpClientFactory = clientFactory;
        }

        public async Task<CouponDto?> GetCoupon(string couponCode)
        {
            var client = _httpClientFactory.CreateClient("Coupon");
            HttpResponseMessage response;
            try
            {
                response = await client.GetAsync($"/api/coupon/GetByCode/{couponCode}");
            }
            catch (HttpRequestException ex)
            {
                throw new HttpRequestException("Coupon service request failed.", null, ex.StatusCode);
            }
            catch (TaskCanceledException)
            {
                throw new HttpRequestException("Coupon service request timed out.");
            }
            using (response)
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                    return null;
                if (!response.IsSuccessStatusCode)
                    throw new HttpRequestException("Coupon service request failed.", null, response.StatusCode);
                var apiContet = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(apiContet))
                    throw new InvalidDataException("Coupon service returned an invalid response.");
                try
                {
                    var resp = JsonConvert.DeserializeObject<ResponseDto>(apiContet);
                    if (resp is not { IsSuccess: true, Result: not null })
                        throw new InvalidDataException("Coupon service returned an invalid response.");
                    return JsonConvert.DeserializeObject<CouponDto>(Convert.ToString(resp.Result)!)
                        ?? throw new InvalidDataException("Coupon service returned an invalid response.");
                }
                catch (JsonException)
                {
                    throw new InvalidDataException("Coupon service returned an invalid response.");
                }
            }
        }
    }
}
