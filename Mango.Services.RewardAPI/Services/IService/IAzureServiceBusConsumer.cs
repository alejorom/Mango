namespace Mango.Services.RewardAPI.Services.IService
{
    public interface IAzureServiceBusConsumer
    {
        Task Start();
        Task Stop();
    }
}
