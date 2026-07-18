using System.Data;
using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Abstractions.Data;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;
using Dapper;

namespace Bookify.Application.Bookings.GetBooking;

internal sealed class GetBookingQueryHandler : IQueryHandler<GetBookingQuery, BookingResponse>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;
    private readonly IUserContext _userContext;

    public GetBookingQueryHandler(ISqlConnectionFactory sqlConnectionFactory, IUserContext userContext)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
        _userContext = userContext;
    }

    public async Task<Result<BookingResponse>> Handle(GetBookingQuery request, CancellationToken cancellationToken)
    {
        using IDbConnection connection = _sqlConnectionFactory.CreateConnection();

        const string sql = """
            SELECT
                id AS Id,
                apartment_id AS ApartmentId,
                user_id AS UserId,
                status AS Status,
                payment_status AS PaymentStatus,
                price_for_period_amount AS PriceAmount,
                price_for_period_currency AS PriceCurrency,
                cleaning_fee_amount AS CleaningFeeAmount,
                cleaning_fee_currency AS CleaningFeeCurrency,
                amenities_up_charge_amount AS AmenitiesUpChargeAmount,
                amenities_up_charge_currency AS AmenitiesUpChargeCurrency,
                extra_guest_charge_amount AS ExtraGuestChargeAmount,
                extra_guest_charge_currency AS ExtraGuestChargeCurrency,
                total_price_amount AS TotalPriceAmount,
                total_price_currency AS TotalPriceCurrency,
                duration_start AS DurationStart,
                duration_end AS DurationEnd,
                created_on_utc AS CreatedOnUtc,
                guest_count AS GuestCount
            FROM bookings
            WHERE id = @BookingId;

            SELECT
                tax_rule_name AS TaxRuleName,
                calculated_amount_amount AS CalculatedAmount,
                calculated_amount_currency AS Currency
            FROM booking_taxes
            WHERE booking_id = @BookingId;
            """;

        using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(
            sql,
            new
            {
                request.BookingId
            });

        BookingResponse? booking = await multi.ReadFirstOrDefaultAsync<BookingResponse>();

        // Resource-based authorization check, maybe move to a generic pipeline/solution later in the future
        if (booking is null || booking.UserId != _userContext.UserId)
        {
            return Result.Failure<BookingResponse>(BookingErrors.NotFound);
        }

        IEnumerable<BookingTaxResponse> taxes = await multi.ReadAsync<BookingTaxResponse>();

        return booking with { Taxes = [.. taxes] };
    }
}
