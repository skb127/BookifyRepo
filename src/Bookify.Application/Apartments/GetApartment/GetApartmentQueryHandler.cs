using System.Data;
using Bookify.Application.Abstractions.Data;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Application.Common;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using Dapper;

namespace Bookify.Application.Apartments.GetApartment;

internal sealed class GetApartmentQueryHandler : IQueryHandler<GetApartmentQuery, ApartmentResponse>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public GetApartmentQueryHandler(ISqlConnectionFactory sqlConnectionFactory) =>
        _sqlConnectionFactory = sqlConnectionFactory;

    public async Task<Result<ApartmentResponse>> Handle(GetApartmentQuery request, CancellationToken cancellationToken)
    {
        using IDbConnection connection = _sqlConnectionFactory.CreateConnection();

        const string sql = """
            SELECT
                a.id AS Id,
                a.owner_id AS OwnerId,
                a.name AS Name,
                a.description AS Description,
                a.amenities AS Amenities,
                a.last_booked_on_utc AS LastBookedOnUtc,
                a.instant_booking AS InstantBooking,
                a.minimum_nights AS MinimumNights,
                a.check_in_cut_off_hours AS CheckInCutOffHours,
                a.base_guests AS BaseGuests,
                a.max_guests AS MaxGuests,
                a.price_amount AS Amount,
                a.price_currency AS Currency,
                a.cleaning_fee_amount AS Amount,
                a.cleaning_fee_currency AS Currency,
                a.extra_guest_fee_amount AS Amount,
                a.extra_guest_fee_currency AS Currency,
                a.address_country AS Country,
                a.address_state AS State,
                a.address_zip_code AS ZipCode,
                a.address_city AS City,
                a.address_street AS Street
            FROM apartments AS a
            WHERE a.id = @ApartmentId AND a.deleted_at IS NULL
            """;

        IEnumerable<ApartmentResponse> apartments = await connection
            .QueryAsync<ApartmentResponse, MoneyResponse, MoneyResponse, MoneyResponse, AddressResponse, ApartmentResponse>(
                sql,
                (apartment, price, cleaningFee, extraGuestFee, address) =>
                {
                    apartment.Price = price;
                    apartment.CleaningFee = cleaningFee;
                    apartment.ExtraGuestFee = extraGuestFee;
                    apartment.Address = address;
                    return apartment;
                },
                new
                {
                    request.ApartmentId
                },
                splitOn: "Amount,Amount,Amount,Country");

        ApartmentResponse? apartmentResponse = apartments.FirstOrDefault();

        return apartmentResponse ?? Result.Failure<ApartmentResponse>(ApartmentErrors.NotFound);
    }
}
