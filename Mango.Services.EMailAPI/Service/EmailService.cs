using Mango.Services.EMailAPI.Data;
using Mango.Services.EMailAPI.Message;
using Mango.Services.EMailAPI.Models;
using Mango.Services.EMailAPI.Models.Dto;
using Mango.Services.EMailAPI.Service.IService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Mango.Services.EMailAPI.Service
{
    public class EmailService : IEmailService
    {
        private readonly DbContextOptions<AppDbContext> _dbOptions;
        private readonly ILogger<EmailService> _logger;

        public EmailService(DbContextOptions<AppDbContext> dbOptions, ILogger<EmailService> logger)
        {
            _dbOptions = dbOptions;
            _logger = logger;
        }

        public async Task EmailCartAndLog(CartDto cartDto)
        {
            StringBuilder message = new();

            message.AppendLine("<br/>Cart Email Requested ");
            message.AppendLine("<br/>Total " + cartDto.CartHeader.CartTotal);
            message.Append("<br/>");
            message.Append("<ul>");
            
            int skippedItemsCount = 0;
            foreach (var item in cartDto.CartDetails)
            {
                if (item.Product != null)
                {
                    message.Append("<li>");
                    message.Append(item.Product.Name + " x " + item.Count);
                    message.Append("</li>");
                }
                else
                {
                    skippedItemsCount++;
                }
            }
            
            message.Append("</ul>");
            
            if (skippedItemsCount > 0)
            {
                message.Append("<br/>[" + skippedItemsCount + " product(s) could not be included]");
            }

            await LogAndEmail(message.ToString(), cartDto.CartHeader.Email);
        }

        public async Task LogOrderPlaced(RewardsMessage rewardsDto)
        {
            string message = "New Order Placed. <br/> Order ID : " + rewardsDto.OrderId;
            await LogAndEmail(message, "dotnetmastery@gmail.com");
        }

        public async Task RegisterUserEmailAndLog(string email)
        {
            string message = "User Registeration Successful. <br/> Email : " + email;
            await LogAndEmail(message, "<<uncorreo>>@<<dominio>>.com");
        }

        private async Task LogAndEmail(string message, string email)
        {
            try
            {
                EmailLogger emailLog = new()
                {
                    Email = email,
                    EmailSent = DateTime.Now,
                    Message = message
                };
                await using var _db = new AppDbContext(_dbOptions);
                await _db.EmailLoggers.AddAsync(emailLog);
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DB persistence failed for email: {Email}", email);
                throw;
            }
        }
    }
}
