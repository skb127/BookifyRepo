using System.Data;
using System.Text;
using Bookify.Application.Abstractions.Data;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Application.Common;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;
using Dapper;

namespace Bookify.Application.Apartments.SearchApartments;

internal sealed class SearchApartmentsQueryHandler : IQueryHandler<SearchApartmentsQuery, PagedResponse<ApartmentResponse>>
{
    private static readonly int[] ActiveBookingStatuses =
    [
        (int)BookingStatus.Reserved,
        (int)BookingStatus.Confirmed,
        (int)BookingStatus.Completed
    ];

    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public SearchApartmentsQueryHandler(ISqlConnectionFactory sqlConnectionFactory) =>
        _sqlConnectionFactory = sqlConnectionFactory;

    public async Task<Result<PagedResponse<ApartmentResponse>>> Handle(SearchApartmentsQuery request, CancellationToken cancellationToken)
    {
        using IDbConnection connection = _sqlConnectionFactory.CreateConnection();

        var builder = new StringBuilder();
        var parameters = new DynamicParameters();

        builder.AppendLine("WHERE a.deleted_at IS NULL");
        parameters.Add("ActiveBookingStatuses", ActiveBookingStatuses);

        string isAvailableExpression = "true";

        if (request.StartDate.HasValue && request.EndDate.HasValue)
        {
            builder.AppendLine(@"
                AND NOT EXISTS (
                    SELECT 1
                    FROM bookings AS b
                    WHERE b.apartment_id = a.id 
                    AND b.status = ANY(@ActiveBookingStatuses)
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
                a.address_street AS Street
            FROM apartments AS a
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
            IsAvailable = a.IsAvailable
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
        public Guid Id { get; init; } = Guid.Empty;
        public string Name { get; init; } = default!;
        public string Description { get; init; } = default!;
        public decimal Price { get; init; } = default!;
        public string Currency { get; init; } = default!;
        public decimal CleaningFee { get; init; } = default!;
        public string CleaningFeeCurrency { get; init; } = default!;
        public int[]? Amenities { get; init; } = [];
        public bool IsAvailable { get; init; }
        public string Country { get; init; } = default!;
        public string State { get; init; } = default!;
        public string ZipCode { get; init; } = default!;
        public string City { get; init; } = default!;
        public string Street { get; init; } = default!;
    }
#pragma warning restore S1144, S3459 // Unused private types or members - Properties are set by Dapper via reflection

}
