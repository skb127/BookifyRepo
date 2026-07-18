using System.Data;
using System.Text;
using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Data;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Application.Common;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;
using Dapper;

namespace Bookify.Application.Apartments.SearchApartments;

internal sealed class
    SearchApartmentsQueryHandler : IQueryHandler<SearchApartmentsQuery, PagedResponse<ApartmentResponse>>
{
    private static readonly int[] HardBlockStatuses =
    [
        (int)BookingStatus.Confirmed,
        (int)BookingStatus.InProgress
    ];

    private const int ReservedStatus = (int)BookingStatus.Reserved;
    private const int PendingPaymentStatus = (int)BookingStatus.PendingPayment;

    private readonly ISqlConnectionFactory _sqlConnectionFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    public SearchApartmentsQueryHandler(
        ISqlConnectionFactory sqlConnectionFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<PagedResponse<ApartmentResponse>>> Handle(SearchApartmentsQuery request,
        CancellationToken cancellationToken)
    {
        using IDbConnection connection = _sqlConnectionFactory.CreateConnection();

        var builder = new StringBuilder();
        var parameters = new DynamicParameters();

        builder.AppendLine("WHERE a.deleted_at IS NULL");
        parameters.Add("HardBlockStatuses", HardBlockStatuses);
        parameters.Add("ReservedStatus", ReservedStatus);
        parameters.Add("PendingPaymentStatus", PendingPaymentStatus);
        parameters.Add("UtcNow", _dateTimeProvider.UtcNow);

        string isAvailableExpression = "true";

        if (request is { StartDate: not null, EndDate: not null })
        {
            builder.AppendLine(@"
                AND NOT EXISTS (
                    SELECT 1
                    FROM bookings AS b
                    WHERE b.apartment_id = a.id 
                    AND (
                        b.status = ANY(@HardBlockStatuses)
                        OR ((b.status = @ReservedStatus OR b.status = @PendingPaymentStatus) AND (b.expires_at IS NULL OR b.expires_at > @UtcNow))
                    )
                    AND b.duration_start <= @EndDate 
                    AND b.duration_end >= @StartDate
                )");
            parameters.Add("StartDate", request.StartDate.Value);
            parameters.Add("EndDate", request.EndDate.Value);

            // If they provided dates, we filter out unavailable ones, 
            // so if they pass the filter, IsAvailable is true for those dates.
            isAvailableExpression = "true";
        }

        if (!string.IsNullOrWhiteSpace(request.City))
        {
            builder.AppendLine(" AND a.address_city ILIKE @City");
            parameters.Add("City", $"%{request.City}%");
        }

        if (!string.IsNullOrWhiteSpace(request.Country))
        {
            builder.AppendLine(" AND a.address_country ILIKE @Country");
            parameters.Add("Country", $"%{request.Country}%");
        }

        if (request.MinPrice.HasValue)
        {
            builder.AppendLine(" AND a.price_amount >= @MinPrice");
            parameters.Add("MinPrice", request.MinPrice.Value);
        }

        if (request.MaxPrice.HasValue)
        {
            builder.AppendLine(" AND a.price_amount <= @MaxPrice");
            parameters.Add("MaxPrice", request.MaxPrice.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Currency))
        {
            builder.AppendLine(" AND a.price_currency = @Currency");
            parameters.Add("Currency", request.Currency);
        }

        if (request.GuestCount.HasValue)
        {
            builder.AppendLine(" AND a.max_guests >= @GuestCount");
            parameters.Add("GuestCount", request.GuestCount.Value);
        }

        if (request.Amenities != null && request.Amenities.Any())
        {
            builder.AppendLine(" AND a.amenities @> @Amenities");
            parameters.Add("Amenities", request.Amenities.ToArray());
        }

        string whereClause = builder.ToString();

        string countSql = $"SELECT COUNT(*) FROM apartments AS a {whereClause}";

        string dataSql = $@"
            SELECT 
                a.id AS Id,
                a.name AS Name,
                a.description AS Description,
                a.price_amount AS Price,
                a.price_currency AS Currency,
                a.cleaning_fee_amount AS CleaningFee,
                a.cleaning_fee_currency AS CleaningFeeCurrency,
                a.amenities AS Amenities,
                {isAvailableExpression} AS IsAvailable,
                a.address_country AS Country,
                a.address_state AS State,
                a.address_zip_code AS ZipCode,
                a.address_city AS City,
                a.address_street AS Street,
                a.minimum_nights AS MinimumNights,
                a.check_in_cut_off_hours AS CheckInCutOffHours,
                a.base_guests AS BaseGuests,
                a.max_guests AS MaxGuests,
                a.extra_guest_fee_amount AS ExtraGuestFeeAmount,
                a.extra_guest_fee_currency AS ExtraGuestFeeCurrency,
                COALESCE(r.AverageRating, 0.0) AS AverageRating
            FROM apartments AS a
            LEFT JOIN (
                SELECT 
                    apartment_id,
                    ROUND(AVG(CAST(rating AS float))::numeric, 1) AS AverageRating
                FROM reviews
                WHERE deleted_on_utc IS NULL
                GROUP BY apartment_id
            ) AS r ON r.apartment_id = a.id
            {whereClause}
            ORDER BY a.id
            LIMIT @PageSize OFFSET @Offset";

        parameters.Add("PageSize", request.PageSize);
        parameters.Add("Offset", (request.Page - 1) * request.PageSize);

        int totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);
        IEnumerable<ApartmentFlat> apartmentFlats = await connection.QueryAsync<ApartmentFlat>(dataSql, parameters);

        var apartments = apartmentFlats.Select(a => new ApartmentResponse
        {
            Id = a.Id,
            Name = a.Name,
            Description = a.Description,
            Price = new MoneyResponse(a.Price, a.Currency),
            CleaningFee = new MoneyResponse(a.CleaningFee, a.CleaningFeeCurrency),
            Amenities = a.Amenities?.ToList() ?? new List<int>(),
            Address = new AddressResponse
            {
                Country = a.Country,
                State = a.State,
                ZipCode = a.ZipCode,
                City = a.City,
                Street = a.Street
            },
            IsAvailable = a.IsAvailable,
            AverageRating = a.AverageRating,
            MinimumNights = a.MinimumNights,
            CheckInCutOffHours = a.CheckInCutOffHours,
            BaseGuests = a.BaseGuests,
            MaxGuests = a.MaxGuests,
            ExtraGuestFee = new MoneyResponse(a.ExtraGuestFeeAmount, a.ExtraGuestFeeCurrency)
        }).ToList();

        return new PagedResponse<ApartmentResponse>
        {
            Items = apartments,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount
        };
    }

#pragma warning disable S1144, S3459 // Unused private types or members - Properties are set by Dapper via reflection
    private sealed class ApartmentFlat
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = default!;
        public string Description { get; init; } = default!;
        public decimal Price { get; init; }
        public string Currency { get; init; } = default!;
        public decimal CleaningFee { get; init; }
        public string CleaningFeeCurrency { get; init; } = default!;
        public int[]? Amenities { get; init; } = [];
        public bool IsAvailable { get; init; }
        public double AverageRating { get; init; }
        public string Country { get; init; } = default!;
        public string State { get; init; } = default!;
        public string ZipCode { get; init; } = default!;
        public string City { get; init; } = default!;
        public string Street { get; init; } = default!;
        public int MinimumNights { get; init; }
        public int CheckInCutOffHours { get; init; }
        public int BaseGuests { get; init; }
        public int MaxGuests { get; init; }
        public decimal ExtraGuestFeeAmount { get; init; }
        public string ExtraGuestFeeCurrency { get; init; } = default!;
    }
#pragma warning restore S1144, S3459 // Unused private types or members - Properties are set by Dapper via reflection
}
