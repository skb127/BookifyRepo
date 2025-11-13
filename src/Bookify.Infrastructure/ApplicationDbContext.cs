using Bookify.Application.Exceptions;
using Bookify.Domain.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bookify.Infrastructure;

public sealed class ApplicationDbContext : DbContext, IUnitOfWork
{
    private readonly IPublisher _publisher;

    public ApplicationDbContext(DbContextOptions options, IPublisher publisher)
        : base(options) =>
        _publisher = publisher;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly); // Automatically apply all configurations from the current assembly

        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        try
        {
            int result = await base.SaveChangesAsync(cancellationToken);

            await PublishDomainEventsAsync();

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
    private async Task PublishDomainEventsAsync()
    {
        var domainEvents = ChangeTracker
            .Entries<Entity>()
            .Select(entry => entry.Entity)
            .SelectMany(entity =>
            {
                IReadOnlyList<IDomainEvent> domainEvents = entity.GetDomainEvents();

                entity.ClearDomainEvents();

                return domainEvents;
            })
            .ToList();

        foreach (IDomainEvent domainEvent in domainEvents)
        {
            await _publisher.Publish(domainEvent);
        }
    }
}
