using System.Text.Json.Serialization;
using Bookify.Domain.Apartments.Events;
using Bookify.Domain.Bookings.Events;
using Bookify.Domain.Reviews.Events;
using Bookify.Domain.Users.Events;

namespace Bookify.Application.Abstractions.Serialization;

[JsonSerializable(typeof(UserCreatedDomainEvent))]
[JsonSerializable(typeof(UserDeletedDomainEvent))]
[JsonSerializable(typeof(UserChangedPasswordDomainEvent))]
[JsonSerializable(typeof(UserPasswordResetDomainEvent))]
[JsonSerializable(typeof(UserProfileUpdatedDomainEvent))]
[JsonSerializable(typeof(UserPasswordRecoveryDomainEvent))]
[JsonSerializable(typeof(UserEmailChangedDomainEvent))]
[JsonSerializable(typeof(UserEmailChangeInitiatedDomainEvent))]
[JsonSerializable(typeof(UserAccountDeletionRequestedDomainEvent))]
[JsonSerializable(typeof(UserAccountDeletionCancelledDomainEvent))]
[JsonSerializable(typeof(UserBannedDomainEvent))]
[JsonSerializable(typeof(UserUnbannedDomainEvent))]
[JsonSerializable(typeof(ApartmentUpdatedDomainEvent))]
[JsonSerializable(typeof(ApartmentDeletedDomainEvent))]
[JsonSerializable(typeof(BookingConfirmedDomainEvent))]
[JsonSerializable(typeof(BookingReservedDomainEvent))]
[JsonSerializable(typeof(BookingCompletedDomainEvent))]
[JsonSerializable(typeof(BookingCheckedInDomainEvent))]
[JsonSerializable(typeof(BookingCheckedOutDomainEvent))]
[JsonSerializable(typeof(BookingExpiredDomainEvent))]
[JsonSerializable(typeof(BookingCancelledDomainEvent))]
[JsonSerializable(typeof(BookingRejectedDomainEvent))]
[JsonSerializable(typeof(BookingNoShowDomainEvent))]
[JsonSerializable(typeof(BookingPaymentAuthorizedDomainEvent))]
[JsonSerializable(typeof(BookingPaymentCompletedDomainEvent))]
[JsonSerializable(typeof(BookingRefundInitiatedDomainEvent))]
[JsonSerializable(typeof(BookingClosedStayDomainEvent))]
[JsonSerializable(typeof(ReviewCreatedDomainEvent))]
[JsonSerializable(typeof(ReviewUpdatedDomainEvent))]
public sealed partial class DomainEventsSerializerContext : JsonSerializerContext;
