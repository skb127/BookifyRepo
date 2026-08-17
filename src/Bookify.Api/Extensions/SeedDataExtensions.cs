using System.Data;
using Bogus;
using Bookify.Application.Abstractions.Data;
using Bookify.Domain.Apartments;
using Dapper;

namespace Bookify.Api.Extensions;

internal static class SeedDataExtensions
{
    public static void SeedData(this IApplicationBuilder app)
    {
        using IServiceScope scope = app.ApplicationServices.CreateScope();

        ISqlConnectionFactory sqlConnectionFactory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
        using IDbConnection connection = sqlConnectionFactory.CreateConnection();

        // Ensure System User exists to own the seeded apartments
        const string insertSystemUserSql = """
            INSERT INTO public.users (id, first_name, last_name, email, identity_id, status, date_of_birth)
            VALUES (@Id, @FirstName, @LastName, @Email, @IdentityId, @Status, @DateOfBirth)
            ON CONFLICT (id) DO NOTHING;
            """;

        var systemUser = new
        {
            Id = Guid.Empty,
            FirstName = "System",
            LastName = "User",
            Email = "system@bookify.internal",
            IdentityId = "00000000-0000-0000-0000-000000000000",
            Status = 'A',
            DateOfBirth = new DateOnly(1900, 1, 1)
        };

        connection.Execute(insertSystemUserSql, systemUser);

        const string insertDefaultCancellationPolicySql = """
            INSERT INTO public.cancellation_policies (id, name, early_guest_penalty_rate, late_guest_penalty_rate, early_host_penalty_rate, late_host_penalty_rate, threshold_hours, is_default, created_on_utc)
            VALUES (@Id, @Name, @EarlyGuestPenaltyRate, @LateGuestPenaltyRate, @EarlyHostPenaltyRate, @LateHostPenaltyRate, @ThresholdHours, @IsDefault, @CreatedOnUtc)
            ON CONFLICT (id) DO NOTHING;
            """;

        var defaultPolicy = new
        {
            Id = new Guid("c0000000-0000-0000-0000-000000000001"),
            Name = "Default Cancellation Policy",
            EarlyGuestPenaltyRate = 0.10m,
            LateGuestPenaltyRate = 0.50m,
            EarlyHostPenaltyRate = 0.10m,
            LateHostPenaltyRate = 0.50m,
            ThresholdHours = 48,
            IsDefault = true,
            CreatedOnUtc = new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc)
        };

        var strictPolicy = new
        {
            Id = new Guid("c0000000-0000-0000-0000-000000000002"),
            Name = "Strict Cancellation Policy",
            EarlyGuestPenaltyRate = 0.50m,
            LateGuestPenaltyRate = 1.00m,
            EarlyHostPenaltyRate = 0.50m,
            LateHostPenaltyRate = 1.00m,
            ThresholdHours = 72,
            IsDefault = false,
            CreatedOnUtc = new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc)
        };

        connection.Execute(insertDefaultCancellationPolicySql, defaultPolicy);
        connection.Execute(insertDefaultCancellationPolicySql, strictPolicy);

        var faker = new Faker();

        List<object> apartments = new();
        for (int i = 0; i < 100; i++)
        {
            apartments.Add(new
            {
                Id = Guid.NewGuid(),
                OwnerId = systemUser.Id,
                Name = faker.Company.CompanyName(),
                Description = "Amazing view",
                Country = faker.Address.Country(),
                State = faker.Address.State(),
                ZipCode = faker.Address.ZipCode(),
                City = faker.Address.City(),
                Street = faker.Address.StreetAddress(),
                PriceAmount = faker.Random.Decimal(50, 1000),
                PriceCurrency = "USD",
                CleaningFeeAmount = faker.Random.Decimal(25, 200),
                CleaningFeeCurrency = "USD",
                Amenities = new List<int> { (int)Amenity.Parking, (int)Amenity.MountainView },
                LastBookedOn = DateTime.MinValue,
                CancellationPolicyId = faker.Random.Bool() ? strictPolicy.Id : defaultPolicy.Id,
                MinimumNights = faker.Random.Int(1, 5),
                CheckInCutOffHours = faker.Random.Int(0, 24),
                BaseGuests = faker.Random.Int(1, 4),
                MaxGuests = faker.Random.Int(4, 10),
                ExtraGuestFeeAmount = faker.Random.Decimal(0, 50),
                ExtraGuestFeeCurrency = "USD"
            });
        }

        const string sql = """
            INSERT INTO public.apartments
            (id, owner_id, "name", description, address_country, address_state, address_zip_code, address_city, address_street, price_amount, price_currency, cleaning_fee_amount, cleaning_fee_currency, amenities, last_booked_on_utc, cancellation_policy_id, minimum_nights, check_in_cut_off_hours, base_guests, max_guests, extra_guest_fee_amount, extra_guest_fee_currency)
            VALUES(@Id, @OwnerId, @Name, @Description, @Country, @State, @ZipCode, @City, @Street, @PriceAmount, @PriceCurrency, @CleaningFeeAmount, @CleaningFeeCurrency, @Amenities, @LastBookedOn, @CancellationPolicyId, @MinimumNights, @CheckInCutOffHours, @BaseGuests, @MaxGuests, @ExtraGuestFeeAmount, @ExtraGuestFeeCurrency);
            """;

        connection.Execute(sql, apartments);

        const string insertTaxRulesSql = """
            INSERT INTO public.tax_rules
            (id, country_code, region, city, rate_value, rate_type, name, effective_from, effective_to, is_active, created_on_utc)
            VALUES(@Id, @CountryCode, @Region, @City, @RateValue, @RateType, @Name, @EffectiveFrom, @EffectiveTo, @IsActive, @CreatedOnUtc)
            ON CONFLICT (id) DO NOTHING;
            """;

        connection.Execute(insertTaxRulesSql, new
        {
            Id = new Guid("a0000000-0000-0000-0000-000000000001"),
            CountryCode = "US",
            Region = (string?)null,
            City = (string?)null,
            RateValue = 0.10m,
            RateType = 1,   // TaxType.Percentage = 1
            Name = "Standard Tax 10%",
            EffectiveFrom = new DateOnly(2020, 1, 1),
            EffectiveTo = (DateOnly?)null,
            IsActive = true,
            CreatedOnUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        connection.Execute(insertTaxRulesSql, new
        {
            Id = new Guid("a0000000-0000-0000-0000-000000000002"),
            CountryCode = "US",
            Region = (string?)null,
            City = (string?)null,
            RateValue = 5.00m,
            RateType = 2,   // TaxType.FixedPerNight = 2
            Name = "Tourist Tax 5/night",
            EffectiveFrom = new DateOnly(2020, 1, 1),
            EffectiveTo = (DateOnly?)null,
            IsActive = true,
            CreatedOnUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}
