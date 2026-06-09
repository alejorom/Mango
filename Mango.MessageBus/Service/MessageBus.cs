using Azure.Messaging.ServiceBus;
using Mango.MessageBus.Service.IService;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Text;

namespace Mango.MessageBus.Service
{
    public class MessageBus : IMessageBus
    {
        private readonly string _connectionString;

        public MessageBus(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("ServiceBusConnection") 
                ?? throw new ArgumentNullException(nameof(configuration), "ServiceBusConnection no configurada");
        }

        public async Task PublishMessage(object message, string topic_queue_Name)
        {
            await using var client = new ServiceBusClient(_connectionString);

            ServiceBusSender sender = client.CreateSender(topic_queue_Name);

            var jsonMessage = JsonConvert.SerializeObject(message);
            ServiceBusMessage finalMessage = new ServiceBusMessage(Encoding
                .UTF8.GetBytes(jsonMessage))
            {
                CorrelationId = Guid.NewGuid().ToString(),
            };

            await sender.SendMessageAsync(finalMessage);
            await client.DisposeAsync();
        }
    }
}
