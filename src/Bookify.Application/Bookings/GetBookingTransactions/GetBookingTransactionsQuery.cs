using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.Bookings.GetBookingTransactions;

public sealed record GetBookingTransactionsQuery(Guid BookingId) : IQuery<IReadOnlyList<BookingTransactionResponse>>;
