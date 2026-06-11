using Mango.Services.EMailAPI.Models.Dto;

namespace Mango.Services.EMailAPI.Service.IService
{
    public interface IEmailService
    {
        Task EmailCartAndLog(CartDto cartDto);
        Task RegisterUserEmailAndLog(string email);
    }
}
