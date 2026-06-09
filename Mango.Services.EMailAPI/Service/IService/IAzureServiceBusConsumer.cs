namespace Mango.Services.EMailAPI.Service.IService
{
    public interface IAzureServiceBusConsumer
    {
        Task Start();
        Task Stop();
    }
}
