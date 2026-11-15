using Mango.Services.ShoppingCartAPI.Models.Dto;
using Mango.Services.ShoppingCartAPI.Service.IService;
using Newtonsoft.Json;

namespace Mango.Services.ShoppingCartAPI.Service
{
    public class ProductService : IProductService
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public ProductService(IHttpClientFactory clientFactory)
        {
            _httpClientFactory = clientFactory;
        }

        public async Task<IEnumerable<ProductDto>> GetProducts()
        {
            var client = _httpClientFactory.CreateClient("Product");
            using var response = await client.GetAsync($"/api/product");
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException("Product service request failed.", null, response.StatusCode);
            var apiContet = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(apiContet))
                throw new InvalidDataException("Product service returned an empty response.");
            var resp = JsonConvert.DeserializeObject<ResponseDto>(apiContet)
                ?? throw new InvalidDataException("Product service returned an invalid response.");
            if (resp.IsSuccess)
            {
                return JsonConvert.DeserializeObject<IEnumerable<ProductDto>>(Convert.ToString(resp.Result));
            }
            return new List<ProductDto>();
        }
    }
}
