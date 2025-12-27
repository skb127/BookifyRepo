using Bookify.Domain.Abstractions;

namespace Bookify.Domain.UnitTests.Infrastructure;

public abstract class BaseTest
{
    public static T AssertDomainEventWasPublished<T>(Entity entity)
        where T : IDomainEvent
    {
        ArgumentNullException.ThrowIfNull(entity);

        var domainEvent = entity.GetDomainEvents().OfType<T>().SingleOrDefault();

        return domainEvent is null
            ? throw new InvalidOperationException($"Expected domain event of type {typeof(T).Name} was not published.")
            : domainEvent;
    }
}
