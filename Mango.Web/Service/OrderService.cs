using Mango.Web.Models;
using Mango.Web.Service.IService;
using Mango.Web.Utility;

namespace Mango.Web.Service
{
    public class OrderService : IOrderService
    {
        private readonly IBaseService _baseService;
        public OrderService(IBaseService baseService)
        {
            _baseService = baseService;
        }

        public async Task<ResponseDto?> CreateOrder(CartDto cartDto)
        {
            return await _baseService.SendAsync(new RequestDto()
            {
                ApiType = SD.ApiType.POST,
                Data = cartDto,
                Url = SD.OrderAPIBase + "/api/order/CreateOrder"
            });
        }

        public Task<ResponseDto?> CreateStripeSession(StripeRequestDto stripeRequestDto)
        {
            throw new NotImplementedException();
        }

        public Task<ResponseDto?> GetAllOrder(string? userId)
        {
            throw new NotImplementedException();
        }

        public Task<ResponseDto?> GetOrder(int orderId)
        {
            throw new NotImplementedException();
        }

        public Task<ResponseDto?> UpdateOrderStatus(int orderId, string newStatus)
        {
            throw new NotImplementedException();
        }

        public Task<ResponseDto?> ValidateStripeSession(int orderHeaderId)
        {
            throw new NotImplementedException();
        }
    }
}
