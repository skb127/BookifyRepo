using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Exceptions;
using Bookify.Domain.Abstractions;
using Bookify.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Bookify.Application.Abstractions.Serialization;

namespace Bookify.Infrastructure;

public sealed class ApplicationDbContext : DbContext, IUnitOfWork
{
    private readonly IDateTimeProvider _dateTimeProvider;

    public ApplicationDbContext(DbContextOptions options, IDateTimeProvider dateTimeProvider)
        : base(options) =>
        _dateTimeProvider = dateTimeProvider;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly); // Automatically apply all configurations from the current assembly

        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        try
        {
            AddDomainEventsAsOutboxMessage(); // Make sure to add/load domain events as outbox messages and add then to the change tracker before saving changes

            int result = await base.SaveChangesAsync(cancellationToken); // When we call SaveChangesAsync, EF Core is going to go through the change tracker and persist all the changes (including the outbox messages) in a single transaction
                                                                         // with give us atomic guarantee that either all changes are persisted or none of them are.

            return result;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException("Concurrency exception occured.", ex);
        }
    }

    /// <summary>
    /// This method is going to the ChangeTracker on EF Core to grab the entity entries which implement Entity class.
    /// Select que the actual entity entry instance, then we are calling SelectMany to map the list of domain events from each entity,
    /// calling the GetDomainEvents(), then clearing the domain events from the entities and returning the domain events.
    /// The ClearDomainEvents() is important because when we publish the domain events, we don't know what could be happening in the handlers.
    /// e.g: There could be another database context created that could use the same entry and add another domain event and that is going to cause strange behavior.
    /// Finally, we just iterate over them one by one and call Publish to publish the domain event, triggering the respective domain event handlers defined in the application layer
    /// </summary>
    /// <returns></returns>
    private void AddDomainEventsAsOutboxMessage()
    {
        var outboxMessages = ChangeTracker
            .Entries<Entity>()
            .Select(entry => entry.Entity)
            .SelectMany(entity =>
            {
                IReadOnlyList<IDomainEvent> domainEvents = entity.GetDomainEvents();

                entity.ClearDomainEvents();

                return domainEvents;
            })
            .Select(domainEvent => new OutboxMessage(
                Guid.CreateVersion7(),
                _dateTimeProvider.UtcNow,
                domainEvent.GetType().Name,
                DomainEventSerializer.Serialize(domainEvent)))
            .ToList();

        AddRange(outboxMessages);
    }
}
