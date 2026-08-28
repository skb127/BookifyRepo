using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Apartments.Requests;
using Bookify.Api.Controllers.Bookings.Requests;
using Bookify.Api.Controllers.TaxRules;
using Bookify.Application.IntegrationTests.Apartments;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Application.Users.RegisterGuest;
using Bookify.Application.Users.RegisterHost;
using Bookify.Domain.Bookings;
using Bookify.Infrastructure.Messaging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace Bookify.Application.IntegrationTests.Bookings;

internal static class BookingTestHelpers
{
    // Helper 1: creates an Apartment (using a Host user) and a Guest user
    public static async Task<(Guid apartmentId, string guestToken)>
        SetupApartmentAndGuestAsync(BaseIntegrationTest test, string password = "Password123!")
    {
        // 1. Create a host to create the apartment
        var hostEmail = $"host_{Guid.CreateVersion7()}@test.com";
        var registerHostCommand = new RegisterHostCommand(
            hostEmail, "Host", "User", password, new DateOnly(1990, 1, 1), "+34612345678");
        await test.Sender.Send(registerHostCommand).ConfigureAwait(false);

        // 2. Create apartment mapping to Host context
        string hostToken = await test.GetAccessToken(hostEmail, password).ConfigureAwait(false);
        test.HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, hostToken);
        var aptData = ApartmentData.ValidCreateApartmentRequest;
        HttpResponseMessage aptResponse =
            await test.HttpClient.PostAsJsonAsync("api/v1/apartments", aptData).ConfigureAwait(false);
        aptResponse.EnsureSuccessStatusCode();
        var apartmentId = await aptResponse.Content.ReadFromJsonAsync<Guid>().ConfigureAwait(false);

        // 3. Create a guest to reserve
        var guestEmail = $"guest_{Guid.CreateVersion7()}@test.com";
        var registerGuestCommand = new RegisterGuestCommand(
            guestEmail, "Guest", "User", password, new DateOnly(1995, 5, 5));
        await test.Sender.Send(registerGuestCommand).ConfigureAwait(false);

        string guestToken = await test.GetAccessToken(guestEmail, password).ConfigureAwait(false);

        return (apartmentId, guestToken);
    }

    // Helper 2: creates an Apartment (using a Host user) and 1 Reserved booking for a Guest
    public static async Task<(Guid userId, Guid apartmentId, Guid bookingId, string guestToken, string guestEmail)>
        SetupReservedBookingAsync(BaseIntegrationTest test, string password = "Password123!")
    {
        // 1. Create two distinct users: One Host (to create Apartment) and One Guest (to reserve)
        var hostEmail = $"host_{Guid.CreateVersion7()}@test.com";
        var guestEmail = $"guest_{Guid.CreateVersion7()}@test.com";

        var registerHostCommand = new RegisterHostCommand(
            hostEmail, "Host", "User", password, new DateOnly(1990, 1, 1), "+34612345678");
        _ = await test.Sender.Send(registerHostCommand).ConfigureAwait(false);

        var registerGuestCommand = new RegisterGuestCommand(
            guestEmail, "Guest", "User", password, new DateOnly(1995, 5, 5));
        var guestUserId = (await test.Sender.Send(registerGuestCommand).ConfigureAwait(false)).Value;

        // 2. Create Apartment via HTTP endpoint as Host
        string hostAccessToken = await test.GetAccessToken(hostEmail, password).ConfigureAwait(true);
        test.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            hostAccessToken);

        var aptData = ApartmentData.ValidCreateApartmentRequest;
        HttpResponseMessage createApartmentResponse = await test.HttpClient.PostAsJsonAsync(
            "api/v1/apartments", aptData).ConfigureAwait(true);
        createApartmentResponse.EnsureSuccessStatusCode();

        Guid apartmentId = await createApartmentResponse.Content.ReadFromJsonAsync<Guid>().ConfigureAwait(true);

        // 3. Reserve Booking as the Guest using HTTP endpoint
        string guestAccessToken = await test.GetAccessToken(guestEmail, password).ConfigureAwait(true);
        test.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, guestAccessToken);

        var reserveRequest = new ReserveBookingRequest(
            apartmentId,
            new DateOnly(2027, 1, 1),
            new DateOnly(2027, 1, 10))
        {
            ApartmentId = apartmentId,
            StartDate = new DateOnly(2027, 1, 1),
            EndDate = new DateOnly(2027, 1, 10)
        };

        HttpResponseMessage reserveResponse =
            await test.HttpClient.PostAsJsonAsync("api/v1/bookings", reserveRequest).ConfigureAwait(true);
        reserveResponse.EnsureSuccessStatusCode();

        Guid bookingId = await reserveResponse.Content.ReadFromJsonAsync<Guid>().ConfigureAwait(true);

        // Transition the booking from PendingPayment to Reserved
        var confirmCommand = new Bookify.Application.Payments.ConfirmPayment.ConfirmPaymentCommand(
            bookingId, $"session_{bookingId}", $"intent_{bookingId}", IsInstantBooking: false);
        var confirmResult = await test.Sender.Send(confirmCommand).ConfigureAwait(false);
        if (confirmResult.IsFailure)
        {
            throw new InvalidOperationException($"Failed to authorize payment: {confirmResult.Error.Name}");
        }

        return (guestUserId, apartmentId, bookingId, guestAccessToken, guestEmail);
    }

    // Helper 3: creates an Apartment and 2 bookings for the same Guest
    // - Booking 1: Reserved
    // - Booking 2: Confirmed
    public static async Task<(Guid userId, Guid apartmentId, Guid booking1ReservedId, Guid booking2ConfirmedId, string
            guestToken, string guestEmail)>
        SetupMultipleBookingsAsync(BaseIntegrationTest test, string password = "Password123!")
    {
        // Reuse Helper 1 to get the apartment, guest, and first reserved booking
        var (userId, apartmentId, bookingId1, guestToken, guestEmail) =
            await SetupReservedBookingAsync(test, password).ConfigureAwait(false);

        // Make sure the HTTP client uses the guest token to reserve a second one
        test.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, guestToken);

        // Reserve Booking 2 with DIFFERENT dates so it doesn't overlap
        var reserveRequest2 = new ReserveBookingRequest(
            apartmentId,
            new DateOnly(2027, 2, 1),
            new DateOnly(2027, 2, 10))
        {
            ApartmentId = apartmentId,
            StartDate = new DateOnly(2027, 2, 1),
            EndDate = new DateOnly(2027, 2, 10)
        };

        HttpResponseMessage reserveResponse2 =
            await test.HttpClient.PostAsJsonAsync("api/v1/bookings", reserveRequest2).ConfigureAwait(true);
        reserveResponse2.EnsureSuccessStatusCode();

        Guid bookingId2 = await reserveResponse2.Content.ReadFromJsonAsync<Guid>().ConfigureAwait(false);

        // Transition Booking 2 from PendingPayment to Reserved before confirming
        var confirmCommand = new Bookify.Application.Payments.ConfirmPayment.ConfirmPaymentCommand(
            bookingId2, $"session_{bookingId2}", $"intent_{bookingId2}", IsInstantBooking: false);
        var confirmResult = await test.Sender.Send(confirmCommand).ConfigureAwait(false);
        if (confirmResult.IsFailure)
        {
            throw new InvalidOperationException($"Failed to authorize payment: {confirmResult.Error.Name}");
        }

        // CONFIRM Booking 2
        HttpResponseMessage confirmResponse = await test.HttpClient.PutAsync(
            new Uri($"api/v1/bookings/{bookingId2}/confirmation", UriKind.Relative),
            null).ConfigureAwait(true);
        confirmResponse.EnsureSuccessStatusCode();

        return (userId, apartmentId, bookingId1, bookingId2, guestToken, guestEmail);
    }

    // Helper 4: creates an Apartment and 1 Reserved booking, returns BOTH owner (host) and guest tokens
    public static async Task<(Guid apartmentId, Guid bookingId, string ownerToken, string ownerEmail, string guestToken,
            string guestEmail)>
        SetupApartmentWithOwnerAsync(BaseIntegrationTest test, string password = "Password123!")
    {
        // 1. Create two distinct users: One Host (owner) and One Guest (to reserve)
        var ownerEmail = $"owner_{Guid.CreateVersion7()}@test.com";
        var guestEmail = $"guest_{Guid.CreateVersion7()}@test.com";

        var registerOwnerCommand = new RegisterHostCommand(
            ownerEmail, "HostOwner", "User", password, new DateOnly(1990, 1, 1), "+34612345678");
        _ = await test.Sender.Send(registerOwnerCommand).ConfigureAwait(false);

        var registerGuestCommand = new RegisterGuestCommand(
            guestEmail, "Guest", "User", password, new DateOnly(1995, 5, 5));
        _ = await test.Sender.Send(registerGuestCommand).ConfigureAwait(false);

        // 2. Create Apartment via HTTP endpoint (as owner => owner is set as OwnerId)
        string ownerAccessToken = await test.GetAccessToken(ownerEmail, password).ConfigureAwait(true);
        test.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            ownerAccessToken);

        var aptData = ApartmentData.ValidCreateApartmentRequest;
        HttpResponseMessage createApartmentResponse = await test.HttpClient.PostAsJsonAsync(
            "api/v1/apartments", aptData).ConfigureAwait(true);
        createApartmentResponse.EnsureSuccessStatusCode();

        Guid apartmentId = await createApartmentResponse.Content.ReadFromJsonAsync<Guid>().ConfigureAwait(true);

        // 3. Reserve Booking as the Guest using HTTP endpoint
        string guestAccessToken = await test.GetAccessToken(guestEmail, password).ConfigureAwait(true);
        test.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, guestAccessToken);

        var reserveRequest = new ReserveBookingRequest(
            apartmentId,
            new DateOnly(2027, 3, 1),
            new DateOnly(2027, 3, 10))
        {
            ApartmentId = apartmentId,
            StartDate = new DateOnly(2027, 3, 1),
            EndDate = new DateOnly(2027, 3, 10)
        };

        HttpResponseMessage reserveResponse =
            await test.HttpClient.PostAsJsonAsync("api/v1/bookings", reserveRequest).ConfigureAwait(true);
        reserveResponse.EnsureSuccessStatusCode();

        Guid bookingId = await reserveResponse.Content.ReadFromJsonAsync<Guid>().ConfigureAwait(true);

        // Transition the booking from PendingPayment to Reserved
        var confirmCommand = new Bookify.Application.Payments.ConfirmPayment.ConfirmPaymentCommand(
            bookingId, $"session_{bookingId}", $"intent_{bookingId}", IsInstantBooking: false);
        var confirmResult = await test.Sender.Send(confirmCommand).ConfigureAwait(false);
        if (confirmResult.IsFailure)
        {
            throw new InvalidOperationException($"Failed to authorize payment: {confirmResult.Error.Name}");
        }

        return (apartmentId, bookingId, ownerAccessToken, ownerEmail, guestAccessToken, guestEmail);
    }

    // Helper 5: creates an Apartment and 1 Completed booking (no reviews yet), returns BOTH owner (host) and guest tokens.
    public static async Task<(Guid apartmentId, Guid bookingId, string ownerToken, string ownerEmail, string guestToken,
            string guestEmail)>
        SetupCompletedBookingAsync(BaseIntegrationTest test, string password = "Password123!")
    {
        // 1. Setup apartment and reserved booking using existing helper
        var (apartmentId, bookingId, ownerToken, ownerEmail, guestToken, guestEmail) =
            await SetupApartmentWithOwnerAsync(test, password).ConfigureAwait(false);

        // 2. Confirm booking
        var confirmCommand = new Bookify.Application.Bookings.ConfirmBooking.ConfirmBookingCommand(bookingId);
        var confirmResult = await test.Sender.Send(confirmCommand).ConfigureAwait(false);
        if (confirmResult.IsFailure)
        {
            throw new InvalidOperationException($"Failed to confirm booking: {confirmResult.Error.Name}");
        }

        // Update Duration in database so that it starts yesterday and ends in 5 days
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dbBooking = await test.DbContext.Set<Booking>().FirstAsync(b => b.Id == bookingId).ConfigureAwait(false);
        typeof(Booking).GetProperty(nameof(Booking.Duration))!.SetValue(dbBooking,
            DateRange.Create(today.AddDays(-1), today.AddDays(5)));
        await test.DbContext.SaveChangesAsync().ConfigureAwait(false);

        // Check in booking (to transition from Confirmed to InProgress)
        var checkInCommand = new Bookify.Application.Bookings.CheckInBooking.CheckInBookingCommand(bookingId);
        var checkInResult = await test.Sender.Send(checkInCommand).ConfigureAwait(false);
        if (checkInResult.IsFailure)
        {
            throw new InvalidOperationException($"Failed to check in booking: {checkInResult.Error.Name}");
        }

        // 3. Complete booking
        var completeCommand = new Bookify.Application.Bookings.CompleteBooking.CompleteBookingCommand(bookingId);
        var completeResult = await test.Sender.Send(completeCommand).ConfigureAwait(false);
        if (completeResult.IsFailure)
        {
            throw new InvalidOperationException($"Failed to complete booking: {completeResult.Error.Name}");
        }

        return (apartmentId, bookingId, ownerToken, ownerEmail, guestToken, guestEmail);
    }

    // Helper 6: creates an Apartment and N Completed bookings (1 per review), returns owner token, guest token (from first guest), etc.
    public static async Task<(Guid apartmentId, List<Guid> bookingIds, string ownerToken, string ownerEmail, string
            guestToken, string guestEmail)>
        SetupApartmentWithMultipleReviewedBookingsAsync(BaseIntegrationTest test,
            IEnumerable<(int Rating, string Comment)> reviews, string password = "Password123!")
    {
        // 1. Setup apartment and owner (Host)
        var ownerEmail = $"owner_{Guid.CreateVersion7()}@test.com";
        var registerOwnerCommand = new RegisterHostCommand(
            ownerEmail, "HostOwner", "User", password, new DateOnly(1990, 1, 1), "+34612345678");
        _ = await test.Sender.Send(registerOwnerCommand).ConfigureAwait(false);

        string ownerToken = await test.GetAccessToken(ownerEmail, password).ConfigureAwait(true);
        test.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, ownerToken);

        var aptData = ApartmentData.CreateWithUniqueCity();

        HttpResponseMessage createApartmentResponse = await test.HttpClient.PostAsJsonAsync(
            "api/v1/apartments", aptData).ConfigureAwait(true);
        createApartmentResponse.EnsureSuccessStatusCode();

        Guid apartmentId = await createApartmentResponse.Content.ReadFromJsonAsync<Guid>().ConfigureAwait(true);

        // 2. Setup guest
        var guestEmail = $"guest_{Guid.CreateVersion7()}@test.com";
        var registerGuestCommand = new RegisterGuestCommand(
            guestEmail, "Guest", "User", password, new DateOnly(1995, 5, 5));
        _ = await test.Sender.Send(registerGuestCommand).ConfigureAwait(false);

        string guestToken = await test.GetAccessToken(guestEmail, password).ConfigureAwait(true);

        var bookingIds = new List<Guid>();
        int monthOffset = 1;
        int yearOffset = 2028;

        foreach (var (rating, comment) in reviews)
        {
            // Reserve Booking as the Guest
            test.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                JwtBearerDefaults.AuthenticationScheme, guestToken);

            var startDate = new DateOnly(yearOffset, monthOffset, 1);
            var endDate = new DateOnly(yearOffset, monthOffset, 10);

            monthOffset++;
            if (monthOffset > 12)
            {
                monthOffset = 1;
                yearOffset++;
            }

            var reserveRequest = new ReserveBookingRequest(apartmentId, startDate, endDate)
            {
                ApartmentId = apartmentId,
                StartDate = startDate,
                EndDate = endDate
            };

            HttpResponseMessage reserveResponse =
                await test.HttpClient.PostAsJsonAsync("api/v1/bookings", reserveRequest).ConfigureAwait(true);
            reserveResponse.EnsureSuccessStatusCode();

            Guid bookingId = await reserveResponse.Content.ReadFromJsonAsync<Guid>().ConfigureAwait(true);
            bookingIds.Add(bookingId);

            // Transition booking from PendingPayment to Reserved
            var confirmCommand = new Bookify.Application.Payments.ConfirmPayment.ConfirmPaymentCommand(
                bookingId, $"session_{bookingId}", $"intent_{bookingId}", IsInstantBooking: false);
            var confirmResult = await test.Sender.Send(confirmCommand).ConfigureAwait(false);
            if (confirmResult.IsFailure)
            {
                throw new InvalidOperationException($"Failed to authorize payment: {confirmResult.Error.Name}");
            }

            // Confirm booking
            var confirmBookingCommand =
                new Bookify.Application.Bookings.ConfirmBooking.ConfirmBookingCommand(bookingId);
            var confirmBookingResult = await test.Sender.Send(confirmBookingCommand).ConfigureAwait(false);
            if (confirmBookingResult.IsFailure)
            {
                throw new InvalidOperationException($"Failed to confirm booking: {confirmBookingResult.Error.Name}");
            }

            // Update Duration in database so that it starts yesterday and ends in 5 days
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var dbBooking = await test.DbContext.Set<Booking>().FirstAsync(b => b.Id == bookingId)
                .ConfigureAwait(false);
            typeof(Booking).GetProperty(nameof(Booking.Duration))!.SetValue(dbBooking,
                DateRange.Create(today.AddDays(-1), today.AddDays(5)));
            await test.DbContext.SaveChangesAsync().ConfigureAwait(false);

            // Check in booking (to transition from Confirmed to InProgress)
            var checkInCommand = new Bookify.Application.Bookings.CheckInBooking.CheckInBookingCommand(bookingId);
            var checkInResult = await test.Sender.Send(checkInCommand).ConfigureAwait(false);
            if (checkInResult.IsFailure)
            {
                throw new InvalidOperationException($"Failed to check in booking: {checkInResult.Error.Name}");
            }

            // Complete booking
            var completeCommand = new Bookify.Application.Bookings.CompleteBooking.CompleteBookingCommand(bookingId);
            var completeResult = await test.Sender.Send(completeCommand).ConfigureAwait(false);
            if (completeResult.IsFailure)
            {
                throw new InvalidOperationException($"Failed to complete booking: {completeResult.Error.Name}");
            }

            // Create review as Guest
            test.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                JwtBearerDefaults.AuthenticationScheme, guestToken);

            var addReviewRequest = new Api.Controllers.Reviews.AddReviewRequest(bookingId, rating, comment)
            {
                BookingId = bookingId,
                Rating = rating,
                Comment = comment
            };

            HttpResponseMessage reviewResponse = await test.HttpClient.PostAsJsonAsync(
                "api/v1/reviews", addReviewRequest).ConfigureAwait(true);
            reviewResponse.EnsureSuccessStatusCode();
        }

        return (apartmentId, bookingIds, ownerToken, ownerEmail, guestToken, guestEmail);
    }

    // Helper 7: Setup a Booking in PendingPayment + Unpaid state using HTTP endpoints
    public static async Task<(Guid userId, Guid apartmentId, Guid bookingId, string guestToken, string guestEmail)>
        SetupPendingPaymentBookingAsync(BaseIntegrationTest test, string password = "Password123!")
    {
        var hostEmail = $"host_{Guid.CreateVersion7()}@test.com";
        var guestEmail = $"guest_{Guid.CreateVersion7()}@test.com";

        var registerHostCommand = new RegisterHostCommand(
            hostEmail, "Host", "User", password, new DateOnly(1990, 1, 1), "+34612345678");
        _ = await test.Sender.Send(registerHostCommand).ConfigureAwait(false);

        var registerGuestCommand = new RegisterGuestCommand(
            guestEmail, "Guest", "User", password, new DateOnly(1995, 5, 5));
        var guestUserId = (await test.Sender.Send(registerGuestCommand).ConfigureAwait(false)).Value;

        string hostAccessToken = await test.GetAccessToken(hostEmail, password).ConfigureAwait(true);
        test.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            hostAccessToken);

        var aptData = ApartmentData.ValidCreateApartmentRequest;
        HttpResponseMessage createApartmentResponse = await test.HttpClient.PostAsJsonAsync(
            "api/v1/apartments", aptData).ConfigureAwait(true);
        createApartmentResponse.EnsureSuccessStatusCode();

        Guid apartmentId = await createApartmentResponse.Content.ReadFromJsonAsync<Guid>().ConfigureAwait(true);

        string guestAccessToken = await test.GetAccessToken(guestEmail, password).ConfigureAwait(true);
        test.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, guestAccessToken);

        var reserveRequest = new ReserveBookingRequest(
            apartmentId,
            new DateOnly(2027, 1, 1),
            new DateOnly(2027, 1, 10))
        {
            ApartmentId = apartmentId,
            StartDate = new DateOnly(2027, 1, 1),
            EndDate = new DateOnly(2027, 1, 10)
        };

        HttpResponseMessage reserveResponse =
            await test.HttpClient.PostAsJsonAsync("api/v1/bookings", reserveRequest).ConfigureAwait(true);
        reserveResponse.EnsureSuccessStatusCode();

        Guid bookingId = await reserveResponse.Content.ReadFromJsonAsync<Guid>().ConfigureAwait(true);

        return (guestUserId, apartmentId, bookingId, guestAccessToken, guestEmail);
    }

    // Helper 8: Setup a Booking in Confirmed + Paid state using MediatR ConfirmPaymentCommand
    public static async Task<(Guid userId, Guid apartmentId, Guid bookingId, string guestToken, string guestEmail)>
        SetupConfirmedPaidBookingAsync(BaseIntegrationTest test, string password = "Password123!")
    {
        var result = await SetupPendingPaymentBookingAsync(test, password).ConfigureAwait(false);
        var confirmCommand = new Bookify.Application.Payments.ConfirmPayment.ConfirmPaymentCommand(
            result.bookingId, $"session_{Guid.NewGuid()}", $"intent_{Guid.NewGuid()}", true);
        var confirmResult = await test.Sender.Send(confirmCommand).ConfigureAwait(false);
        if (confirmResult.IsFailure)
        {
            throw new InvalidOperationException($"Failed to confirm booking: {confirmResult.Error.Name}");
        }

        return result;
    }

    // Helper 9: Setup a Booking in Cancelled + Paid state using HTTP cancellation endpoint
    public static async Task<(Guid userId, Guid apartmentId, Guid bookingId, string guestToken, string guestEmail)>
        SetupCancelledPaidBookingAsync(BaseIntegrationTest test, string password = "Password123!")
    {
        var result = await SetupConfirmedPaidBookingAsync(test, password).ConfigureAwait(false);

        test.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, result.guestToken);

        HttpResponseMessage response = await test.HttpClient.PutAsync(
            new Uri($"api/v1/bookings/{result.bookingId}/cancellation", UriKind.Relative),
            null).ConfigureAwait(true);

        response.EnsureSuccessStatusCode();

        return result;
    }

    // Helper 10: Setup a Booking in Cancelled + RefundProcessing state by waiting for the background outbox processor
    public static async Task<(Guid userId, Guid apartmentId, Guid bookingId, string guestToken, string guestEmail)>
        SetupRefundProcessingBookingAsync(BaseIntegrationTest test, string password = "Password123!")
    {
        var result = await SetupCancelledPaidBookingAsync(test, password).ConfigureAwait(false);

        // Wait for the background Outbox processor to automatically transition the booking to RefundProcessing
        for (int i = 0; i < 30; i++)
        {
            test.DbContext.ChangeTracker.Clear();
            Booking? booking = await test.DbContext.Set<Booking>().FindAsync(result.bookingId).ConfigureAwait(false);
            if (booking is not null && booking.PaymentStatus == PaymentStatus.RefundProcessing)
            {
                break;
            }

            await Task.Delay(500).ConfigureAwait(false);
        }

        return result;
    }

    // Helper 11: Get booking via API
    public static async Task<Bookify.Application.Bookings.GetBooking.BookingResponse> GetBookingViaApiAsync(
        BaseIntegrationTest test, Guid bookingId, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/bookings/{bookingId}");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, token);

        HttpResponseMessage response = await test.HttpClient.SendAsync(request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<Bookify.Application.Bookings.GetBooking.BookingResponse>()
            .ConfigureAwait(false))!;
    }

    // Helper 12: Get booking transactions via API
    public static async
        Task<IReadOnlyList<Bookify.Application.Bookings.GetBookingTransactions.BookingTransactionResponse>>
        GetBookingTransactionsViaApiAsync(
            BaseIntegrationTest test, Guid bookingId, string adminToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/bookings/{bookingId}/transactions");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, adminToken);

        HttpResponseMessage response = await test.HttpClient.SendAsync(request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return (await response.Content
            .ReadFromJsonAsync<
                IReadOnlyList<Bookify.Application.Bookings.GetBookingTransactions.BookingTransactionResponse>>()
            .ConfigureAwait(false))!;
    }

    // Helper 13: creates an Apartment and 1 Reserved booking for a Guest and returns host email
    public static async Task<(Guid userId, Guid apartmentId, Guid bookingId, string guestToken, string guestEmail,
            string hostEmail)>
        SetupReservedBookingWithHostAsync(BaseIntegrationTest test, string password = "Password123!")
    {
        var hostEmail = $"host_{Guid.CreateVersion7()}@test.com";
        var guestEmail = $"guest_{Guid.CreateVersion7()}@test.com";

        var registerHostCommand = new RegisterHostCommand(
            hostEmail, "Host", "User", password, new DateOnly(1990, 1, 1), "+34612345678");
        _ = await test.Sender.Send(registerHostCommand).ConfigureAwait(false);

        var registerGuestCommand = new RegisterGuestCommand(
            guestEmail, "Guest", "User", password, new DateOnly(1995, 5, 5));
        var guestUserId = (await test.Sender.Send(registerGuestCommand).ConfigureAwait(false)).Value;

        string hostAccessToken = await test.GetAccessToken(hostEmail, password).ConfigureAwait(true);
        test.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            hostAccessToken);

        var aptData = ApartmentData.ValidCreateApartmentRequest;
        HttpResponseMessage createApartmentResponse = await test.HttpClient.PostAsJsonAsync(
            "api/v1/apartments", aptData).ConfigureAwait(true);
        createApartmentResponse.EnsureSuccessStatusCode();

        Guid apartmentId = await createApartmentResponse.Content.ReadFromJsonAsync<Guid>().ConfigureAwait(true);

        string guestAccessToken = await test.GetAccessToken(guestEmail, password).ConfigureAwait(true);
        test.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, guestAccessToken);

        var reserveRequest = new ReserveBookingRequest(
            apartmentId,
            new DateOnly(2027, 1, 1),
            new DateOnly(2027, 1, 10))
        {
            ApartmentId = apartmentId,
            StartDate = new DateOnly(2027, 1, 1),
            EndDate = new DateOnly(2027, 1, 10)
        };

        HttpResponseMessage reserveResponse =
            await test.HttpClient.PostAsJsonAsync("api/v1/bookings", reserveRequest).ConfigureAwait(true);
        reserveResponse.EnsureSuccessStatusCode();

        Guid bookingId = await reserveResponse.Content.ReadFromJsonAsync<Guid>().ConfigureAwait(true);

        var confirmCommand = new Bookify.Application.Payments.ConfirmPayment.ConfirmPaymentCommand(
            bookingId, $"session_{bookingId}", $"intent_{bookingId}", IsInstantBooking: false);
        var confirmResult = await test.Sender.Send(confirmCommand).ConfigureAwait(false);
        if (confirmResult.IsFailure)
        {
            throw new InvalidOperationException($"Failed to authorize payment: {confirmResult.Error.Name}");
        }

        return (guestUserId, apartmentId, bookingId, guestAccessToken, guestEmail, hostEmail);
    }

    // Helper 14: creates an Apartment and 1 PendingPayment booking for a Guest and returns host email
    public static async Task<(Guid userId, Guid apartmentId, Guid bookingId, string guestToken, string guestEmail,
            string hostEmail)>
        SetupPendingPaymentBookingWithHostAsync(BaseIntegrationTest test, string password = "Password123!")
    {
        var hostEmail = $"host_{Guid.CreateVersion7()}@test.com";
        var guestEmail = $"guest_{Guid.CreateVersion7()}@test.com";

        var registerHostCommand = new RegisterHostCommand(
            hostEmail, "Host", "User", password, new DateOnly(1990, 1, 1), "+34612345678");
        _ = await test.Sender.Send(registerHostCommand).ConfigureAwait(false);

        var registerGuestCommand = new RegisterGuestCommand(
            guestEmail, "Guest", "User", password, new DateOnly(1995, 5, 5));
        var guestUserId = (await test.Sender.Send(registerGuestCommand).ConfigureAwait(false)).Value;

        string hostAccessToken = await test.GetAccessToken(hostEmail, password).ConfigureAwait(true);
        test.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            hostAccessToken);

        var aptData = ApartmentData.ValidCreateApartmentRequest;
        HttpResponseMessage createApartmentResponse = await test.HttpClient.PostAsJsonAsync(
            "api/v1/apartments", aptData).ConfigureAwait(true);
        createApartmentResponse.EnsureSuccessStatusCode();

        Guid apartmentId = await createApartmentResponse.Content.ReadFromJsonAsync<Guid>().ConfigureAwait(true);

        string guestAccessToken = await test.GetAccessToken(guestEmail, password).ConfigureAwait(true);
        test.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, guestAccessToken);

        var reserveRequest = new ReserveBookingRequest(
            apartmentId,
            new DateOnly(2027, 1, 1),
            new DateOnly(2027, 1, 10))
        {
            ApartmentId = apartmentId,
            StartDate = new DateOnly(2027, 1, 1),
            EndDate = new DateOnly(2027, 1, 10)
        };

        HttpResponseMessage reserveResponse =
            await test.HttpClient.PostAsJsonAsync("api/v1/bookings", reserveRequest).ConfigureAwait(true);
        reserveResponse.EnsureSuccessStatusCode();

        Guid bookingId = await reserveResponse.Content.ReadFromJsonAsync<Guid>().ConfigureAwait(true);

        return (guestUserId, apartmentId, bookingId, guestAccessToken, guestEmail, hostEmail);
    }

    // Helper 15: creates an Apartment and 1 ConfirmedPaid booking for a Guest and returns host email
    public static async Task<(Guid userId, Guid apartmentId, Guid bookingId, string guestToken, string guestEmail,
            string hostEmail)>
        SetupConfirmedPaidBookingWithHostAsync(BaseIntegrationTest test, string password = "Password123!")
    {
        var result = await SetupPendingPaymentBookingWithHostAsync(test, password).ConfigureAwait(false);
        var confirmCommand = new Bookify.Application.Payments.ConfirmPayment.ConfirmPaymentCommand(
            result.bookingId, $"session_{Guid.NewGuid()}", $"intent_{Guid.NewGuid()}", true);
        var confirmResult = await test.Sender.Send(confirmCommand).ConfigureAwait(false);
        if (confirmResult.IsFailure)
        {
            throw new InvalidOperationException($"Failed to confirm booking: {confirmResult.Error.Name}");
        }

        return result;
    }

    // Helper 16: creates an Apartment and 1 ConfirmedPaid booking for a Guest and returns host email
    public static async Task<(Guid apartmentId, string guestToken, string guestEmail, string hostEmail)>
        SetupApartmentAndGuestWithHostAsync(BaseIntegrationTest test, string password = "Password123!")
    {
        // 1. Create a host to create the apartment
        var hostEmail = $"host_{Guid.CreateVersion7()}@test.com";
        var registerHostCommand = new RegisterHostCommand(
            hostEmail, "Host", "User", password, new DateOnly(1990, 1, 1), "+34612345678");
        await test.Sender.Send(registerHostCommand).ConfigureAwait(false);

        // 2. Create apartment mapping to Host context
        string hostToken = await test.GetAccessToken(hostEmail, password).ConfigureAwait(false);
        test.HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, hostToken);
        var aptData = ApartmentData.ValidCreateApartmentRequest;
        HttpResponseMessage aptResponse =
            await test.HttpClient.PostAsJsonAsync("api/v1/apartments", aptData).ConfigureAwait(false);
        aptResponse.EnsureSuccessStatusCode();
        var apartmentId = await aptResponse.Content.ReadFromJsonAsync<Guid>().ConfigureAwait(false);

        // 3. Create a guest to reserve
        var guestEmail = $"guest_{Guid.CreateVersion7()}@test.com";
        var registerGuestCommand = new RegisterGuestCommand(
            guestEmail, "Guest", "User", password, new DateOnly(1995, 5, 5));
        await test.Sender.Send(registerGuestCommand).ConfigureAwait(false);

        string guestToken = await test.GetAccessToken(guestEmail, password).ConfigureAwait(false);

        return (apartmentId, guestToken, guestEmail, hostEmail);
    }

    // Helper 17: Sets up tax rule, host, apartment, and guest with a reserved booking
    public static async Task<(Guid apartmentId, Guid bookingId, string guestToken, string guestEmail, string hostToken, string hostEmail, string adminToken)>
        SetupInvoiceTestDataAsync(
            BaseIntegrationTest test,
            CreateApartmentRequest? apartmentRequest = null,
            int guestCount = 2,
            DateOnly? startDate = null,
            DateOnly? endDate = null,
            string password = "Password123!")
    {
        startDate ??= new DateOnly(2027, 1, 1);
        endDate ??= new DateOnly(2027, 1, 10);
        string uniqueLocation = $"Madrid_{Guid.CreateVersion7():N}";
        var baseRequest = apartmentRequest ?? ApartmentData.InvoiceTestApartmentRequest;
        apartmentRequest = baseRequest with
        {
            Address = baseRequest.Address with
            {
                City = uniqueLocation,
                State = uniqueLocation
            }
        };

        // 1. Create admin and ensure tax rule for ES/uniqueLocation/uniqueLocation (21% IVA)
        string adminToken = await test.GetAdminTokenAsync(password).ConfigureAwait(false);
        test.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, adminToken);

        var taxRuleRequest = new CreateTaxRuleRequest(
            "ES",
            uniqueLocation,
            uniqueLocation,
            0.21m,
            1, // Percentage
            "IVA España " + Guid.CreateVersion7(),
            new DateOnly(2026, 1, 1),
            null)
        {
            RateValue = 0.21m,
            RateType = 1,
            EffectiveFrom = new DateOnly(2026, 1, 1)
        };

        HttpResponseMessage taxRuleResponse = await test.HttpClient.PostAsJsonAsync(
            new Uri("api/v1/tax-rules", UriKind.Relative), taxRuleRequest).ConfigureAwait(false);
        taxRuleResponse.EnsureSuccessStatusCode();

        // 2. Create Host and Apartment
        var hostEmail = $"host_{Guid.CreateVersion7()}@test.com";
        var registerHostCommand = new RegisterHostCommand(
            hostEmail, "Host", "User", password, new DateOnly(1990, 1, 1), "+34612345678");
        await test.Sender.Send(registerHostCommand).ConfigureAwait(false);

        string hostToken = await test.GetAccessToken(hostEmail, password).ConfigureAwait(false);
        test.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, hostToken);

        HttpResponseMessage aptResponse = await test.HttpClient.PostAsJsonAsync(
            new Uri("api/v1/apartments", UriKind.Relative), apartmentRequest).ConfigureAwait(false);
        aptResponse.EnsureSuccessStatusCode();
        Guid apartmentId = await aptResponse.Content.ReadFromJsonAsync<Guid>().ConfigureAwait(false);

        // 3. Create Guest and Reserve Booking
        var guestEmail = $"guest_{Guid.CreateVersion7()}@test.com";
        var registerGuestCommand = new RegisterGuestCommand(
            guestEmail, "Guest", "User", password, new DateOnly(1995, 5, 5));
        await test.Sender.Send(registerGuestCommand).ConfigureAwait(false);

        string guestToken = await test.GetAccessToken(guestEmail, password).ConfigureAwait(false);
        test.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, guestToken);

        var reserveRequest = new ReserveBookingRequest(
            apartmentId,
            startDate.Value,
            endDate.Value,
            guestCount)
        {
            ApartmentId = apartmentId,
            StartDate = startDate.Value,
            EndDate = endDate.Value
        };

        HttpResponseMessage reserveResponse = await test.HttpClient.PostAsJsonAsync(
            new Uri("api/v1/bookings", UriKind.Relative), reserveRequest).ConfigureAwait(false);
        reserveResponse.EnsureSuccessStatusCode();
        Guid bookingId = await reserveResponse.Content.ReadFromJsonAsync<Guid>().ConfigureAwait(false);

        return (apartmentId, bookingId, guestToken, guestEmail, hostToken, hostEmail, adminToken);
    }

    // Helper 18: Sets up an end-to-end booking with a generated Invoice PDF (Status == Generated)
    public static async Task<(Guid apartmentId, Guid bookingId, Guid invoiceId, string guestToken, string guestEmail, string hostToken, string hostEmail, string adminToken)>
        SetupBookingWithGeneratedInvoiceAsync(
            BaseIntegrationTest test,
            CreateApartmentRequest? apartmentRequest = null,
            int guestCount = 2,
            DateOnly? startDate = null,
            DateOnly? endDate = null,
            string password = "Password123!")
    {
        apartmentRequest ??= ApartmentData.InvoiceTestInstantApartmentRequest;
        bool isInstant = apartmentRequest.InstantBooking;

        var setup = await SetupInvoiceTestDataAsync(test, apartmentRequest, guestCount, startDate, endDate, password).ConfigureAwait(false);

        // Publish checkout.session.completed event via Service Bus MessagePublisher
        var webhookEvent = new StripeWebhookEvent
        {
            EventType = "checkout.session.completed",
            BookingId = setup.bookingId,
            SessionId = $"session_{setup.bookingId}",
            PaymentIntentId = $"intent_{setup.bookingId}",
            IsInstant = isInstant
        };

        await test.MessagePublisher.PublishAsync("stripe-events", webhookEvent).ConfigureAwait(false);

        if (!isInstant)
        {
            // Wait until booking is in Reserved status (Authorized payment)
            await PollingHelper.WaitUntilAsync(
                action: async () =>
                {
                    test.DbContext.ChangeTracker.Clear();
                    return await test.DbContext.Set<Booking>()
                        .AsNoTracking()
                        .FirstOrDefaultAsync(b => b.Id == setup.bookingId).ConfigureAwait(false);
                },
                isReady: b => b is not null && b.Status == BookingStatus.Reserved,
                timeout: TimeSpan.FromSeconds(15),
                interval: TimeSpan.FromMilliseconds(300)).ConfigureAwait(false);

            // Host confirms the booking
            test.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                JwtBearerDefaults.AuthenticationScheme, setup.hostToken);

            HttpResponseMessage confirmResponse = await test.HttpClient.PutAsync(
                new Uri($"api/v1/bookings/{setup.bookingId}/confirmation", UriKind.Relative), null).ConfigureAwait(false);
            confirmResponse.EnsureSuccessStatusCode();
        }

        // Wait for the invoice to be generated by the background Function
        Invoice? invoice;
        try
        {
            invoice = await PollingHelper.WaitUntilAsync(
                action: async () =>
                {
                    test.DbContext.ChangeTracker.Clear();
                    return await test.DbContext.Set<Invoice>()
                        .AsNoTracking()
                        .FirstOrDefaultAsync(i =>
                            i.BookingId == setup.bookingId &&
                            i.InvoiceType == InvoiceType.Invoice &&
                            i.Status == InvoiceStatus.Generated).ConfigureAwait(false);
                },
                isReady: inv => inv is not null,
                timeout: TimeSpan.FromSeconds(30),
                interval: TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);
        }
        catch (TimeoutException ex)
        {
            var logs = await test.Factory.GetFunctionLogsAsync().ConfigureAwait(false);
            test.DbContext.ChangeTracker.Clear();
            var dbInvoices = await test.DbContext.Set<Invoice>().AsNoTracking().Where(i => i.BookingId == setup.bookingId).ToListAsync().ConfigureAwait(false);
            string invoiceSummary = string.Join(", ", dbInvoices.Select(i => $"[Id={i.Id}, Type={i.InvoiceType}, Status={i.Status}, Blob={i.PdfBlobName}]"));
            throw new TimeoutException($"Invoice generation timed out for booking {setup.bookingId}. Existing invoices in DB: ({invoiceSummary}). Function Logs:\nSTDOUT:\n{logs.Stdout}\nSTDERR:\n{logs.Stderr}", ex);
        }

        return (setup.apartmentId, setup.bookingId, invoice!.Id, setup.guestToken, setup.guestEmail, setup.hostToken, setup.hostEmail, setup.adminToken);
    }

    // Helper 19: Sets up an end-to-end booking with a generated Credit Note (Status == Generated)
    public static async Task<(Guid apartmentId, Guid bookingId, Guid invoiceId, Guid creditNoteId, string guestToken, string guestEmail, string hostToken, string hostEmail, string adminToken)>
        SetupBookingWithCreditNoteAsync(
            BaseIntegrationTest test,
            bool cancelledByHost = true,
            CreateApartmentRequest? apartmentRequest = null,
            int guestCount = 2,
            DateOnly? startDate = null,
            DateOnly? endDate = null,
            string password = "Password123!")
    {
        var setup = await SetupBookingWithGeneratedInvoiceAsync(test, apartmentRequest, guestCount, startDate, endDate, password).ConfigureAwait(false);

        // Cancel booking via HTTP
        string cancellationToken = cancelledByHost ? setup.hostToken : setup.guestToken;
        test.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, cancellationToken);

        HttpResponseMessage cancelResponse = await test.HttpClient.PutAsync(
            new Uri($"api/v1/bookings/{setup.bookingId}/cancellation", UriKind.Relative), null).ConfigureAwait(false);
        cancelResponse.EnsureSuccessStatusCode();

        // Wait until booking reaches RefundProcessing
        Booking booking = await PollingHelper.WaitUntilAsync(
            action: async () =>
            {
                test.DbContext.ChangeTracker.Clear();
                return await test.DbContext.Set<Booking>()
                    .Include(b => b.Refund)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(b =>
                        b.Id == setup.bookingId &&
                        b.PaymentStatus == PaymentStatus.RefundProcessing).ConfigureAwait(false);
            },
            isReady: b => b is not null && b.Refund is not null,
            timeout: TimeSpan.FromSeconds(30),
            interval: TimeSpan.FromMilliseconds(500)).ConfigureAwait(false)
            ?? throw new TimeoutException($"Booking {setup.bookingId} did not reach RefundProcessing within the timeout period.");

        decimal refundAmount = booking.Refund!.Amount;

        // Publish charge.refunded event via Service Bus MessagePublisher
        var refundWebhookEvent = new StripeWebhookEvent
        {
            EventType = "charge.refunded",
            BookingId = setup.bookingId,
            RefundId = $"ref_{Guid.CreateVersion7()}",
            Amount = refundAmount
        };

        await test.MessagePublisher.PublishAsync("stripe-events", refundWebhookEvent).ConfigureAwait(false);

        // Wait until CreditNote reaches Generated
        Invoice creditNote = await PollingHelper.WaitUntilAsync(
            action: async () =>
            {
                test.DbContext.ChangeTracker.Clear();
                return await test.DbContext.Set<Invoice>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i =>
                        i.BookingId == setup.bookingId &&
                        i.InvoiceType == InvoiceType.CreditNote &&
                        i.Status == InvoiceStatus.Generated).ConfigureAwait(false);
            },
            isReady: inv => inv is not null,
            timeout: TimeSpan.FromSeconds(30),
            interval: TimeSpan.FromMilliseconds(500)).ConfigureAwait(false)
            ?? throw new TimeoutException($"Credit note for booking {setup.bookingId} was not generated within the timeout period.");

        return (setup.apartmentId, setup.bookingId, setup.invoiceId, creditNote.Id, setup.guestToken, setup.guestEmail, setup.hostToken, setup.hostEmail, setup.adminToken);
    }
}
