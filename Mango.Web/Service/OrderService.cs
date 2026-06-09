using Mango.Web.Models;
using Mango.Web.Service.IService;

namespace Mango.Web.Service
{
    public class OrderService : IOrderService
    {
        public Task<ResponseDto?> CreateOrder(CartDto cartDto)
        {
            throw new NotImplementedException();
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
