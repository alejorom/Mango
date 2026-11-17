using System.Collections.Concurrent;
using Mango.MessageBus.Service.IService;

namespace Mango.Services.AuthAPI.Tests.TestSupport;

internal sealed record Publication(object Payload, string Destination);

internal sealed class RecordingBus : IMessageBus
{
    public ConcurrentQueue<Publication> Publications { get; } = new();
    public Exception? Failure { get; set; }

    public Task PublishMessage(object message, string topic_queue_Name)
    {
        Publications.Enqueue(new Publication(message, topic_queue_Name));
        return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
    }
}
