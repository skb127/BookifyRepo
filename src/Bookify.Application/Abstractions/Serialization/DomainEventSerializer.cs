using System.Text.Json;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments.Events;
using Bookify.Domain.Bookings.Events;
using Bookify.Domain.Reviews.Events;
using Bookify.Domain.Users.Events;

namespace Bookify.Application.Abstractions.Serialization;

public static class DomainEventSerializer
{
    private static readonly Dictionary<string, Type> EventTypes = new(StringComparer.Ordinal)
    {
        { nameof(UserCreatedDomainEvent), typeof(UserCreatedDomainEvent) },
        { nameof(UserDeletedDomainEvent), typeof(UserDeletedDomainEvent) },
        { nameof(UserChangedPasswordDomainEvent), typeof(UserChangedPasswordDomainEvent) },
        { nameof(UserPasswordResetDomainEvent), typeof(UserPasswordResetDomainEvent) },
        { nameof(UserProfileUpdatedDomainEvent), typeof(UserProfileUpdatedDomainEvent) },
        { nameof(UserPasswordRecoveryDomainEvent), typeof(UserPasswordRecoveryDomainEvent) },
        { nameof(UserEmailChangedDomainEvent), typeof(UserEmailChangedDomainEvent) },
        { nameof(UserEmailChangeInitiatedDomainEvent), typeof(UserEmailChangeInitiatedDomainEvent) },
        { nameof(ApartmentUpdatedDomainEvent), typeof(ApartmentUpdatedDomainEvent) },
        { nameof(ApartmentDeletedDomainEvent), typeof(ApartmentDeletedDomainEvent) },
        { nameof(BookingConfirmedDomainEvent), typeof(BookingConfirmedDomainEvent) },
        { nameof(BookingReservedDomainEvent), typeof(BookingReservedDomainEvent) },
        { nameof(BookingCompletedDomainEvent), typeof(BookingCompletedDomainEvent) },
        { nameof(BookingCheckedInDomainEvent), typeof(BookingCheckedInDomainEvent) },
        { nameof(BookingCheckedOutDomainEvent), typeof(BookingCheckedOutDomainEvent) },
        { nameof(BookingExpiredDomainEvent), typeof(BookingExpiredDomainEvent) },
        { nameof(BookingCancelledDomainEvent), typeof(BookingCancelledDomainEvent) },
        { nameof(BookingRejectedDomainEvent), typeof(BookingRejectedDomainEvent) },
        { nameof(BookingNoShowDomainEvent), typeof(BookingNoShowDomainEvent) },
        { nameof(BookingPaymentAuthorizedDomainEvent), typeof(BookingPaymentAuthorizedDomainEvent) },
        { nameof(BookingPaymentCompletedDomainEvent), typeof(BookingPaymentCompletedDomainEvent) },
        { nameof(BookingRefundInitiatedDomainEvent), typeof(BookingRefundInitiatedDomainEvent) },
        { nameof(BookingClosedStayDomainEvent), typeof(BookingClosedStayDomainEvent) },
        { nameof(ReviewCreatedDomainEvent), typeof(ReviewCreatedDomainEvent) },
        { nameof(ReviewUpdatedDomainEvent), typeof(ReviewUpdatedDomainEvent) }
    };

    public static string Serialize<T>(T domainEvent) where T : IDomainEvent =>
        JsonSerializer.Serialize(domainEvent, typeof(T), DomainEventsSerializerContext.Default);

    public static string Serialize(IDomainEvent domainEvent) =>
        JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), DomainEventsSerializerContext.Default);

    public static IDomainEvent? Deserialize(string typeName, string content)
    {
        if (!EventTypes.TryGetValue(typeName, out Type? type))
        {
            throw new InvalidOperationException($"Unknown domain event type: {typeName}");
        }

        return JsonSerializer.Deserialize(content, type, DomainEventsSerializerContext.Default) as IDomainEvent;
    }
}
