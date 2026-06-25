namespace Bookify.Application.Abstractions.Messaging;

public interface IEventBusConsumer
{
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}
