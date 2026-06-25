using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Bookings;
using Bookify.Application.IntegrationTests.Apartments;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Domain.Bookings;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Bookify.Application.IntegrationTests.Bookings;

internal static class BookingTestHelpers
{
    // Helper 1: creates an Apartment and a Guest user
    public static async Task<(Guid apartmentId, string guestToken)>
        SetupApartmentAndGuestAsync(BaseIntegrationTest test, string password = "Password123!")
    {
        // 1. Create an admin to create the apartment
        var adminEmail = $"admin_{Guid.CreateVersion7()}@test.com";
        var registerAdminCommand = new Bookify.Application.Users.RegisterUser.RegisterUserCommand(
            adminEmail, "Admin", "User", password, new DateOnly(1990, 1, 1));
        await test.Sender.Send(registerAdminCommand).ConfigureAwait(false);
        await test.PromoteToAdminAsync(adminEmail).ConfigureAwait(false);

        // 2. Create apartment mapping to Admin context
        string adminToken = await test.GetAccessToken(adminEmail, password).ConfigureAwait(false);
        test.HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, adminToken);
        var aptData = ApartmentData.ValidCreateApartmentRequest;
        HttpResponseMessage aptResponse =
            await test.HttpClient.PostAsJsonAsync("api/v1/apartments", aptData).ConfigureAwait(false);
        aptResponse.EnsureSuccessStatusCode();
        var apartmentId = await aptResponse.Content.ReadFromJsonAsync<Guid>().ConfigureAwait(false);

        // 3. Create a guest to reserve
        var guestEmail = $"guest_{Guid.CreateVersion7()}@test.com";
        var registerGuestCommand = new Bookify.Application.Users.RegisterUser.RegisterUserCommand(
            guestEmail, "Guest", "User", password, new DateOnly(1995, 5, 5));
        await test.Sender.Send(registerGuestCommand).ConfigureAwait(false);

        string guestToken = await test.GetAccessToken(guestEmail, password).ConfigureAwait(false);

        return (apartmentId, guestToken);
    }

    // Helper 2: creates an Apartment and 1 Reserved booking for a Guest
    public static async Task<(Guid userId, Guid apartmentId, Guid bookingId, string guestToken, string guestEmail)>
        SetupReservedBookingAsync(BaseIntegrationTest test, string password = "Password123!")
    {
        // 1. Create two distinct users: One Admin (to create Apartment) and One Guest (to reserve)
        var adminEmail = $"admin_{Guid.CreateVersion7()}@test.com";
        var guestEmail = $"guest_{Guid.CreateVersion7()}@test.com";

        var registerAdminCommand = new Bookify.Application.Users.RegisterUser.RegisterUserCommand(
            adminEmail, "Admin", "User", password, new DateOnly(1990, 1, 1));
        _ = await test.Sender.Send(registerAdminCommand).ConfigureAwait(false);

        var registerGuestCommand = new Bookify.Application.Users.RegisterUser.RegisterUserCommand(
            guestEmail, "Guest", "User", password, new DateOnly(1995, 5, 5));
        var guestUserId = (await test.Sender.Send(registerGuestCommand).ConfigureAwait(false)).Value;

        // 2. Promote the admin user so they can create the apartment
        await test.PromoteToAdminAsync(adminEmail).ConfigureAwait(false);

        // 3. Create Apartment via HTTP endpoint
        string adminAccessToken = await test.GetAccessToken(adminEmail, password).ConfigureAwait(true);
        test.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            adminAccessToken);

        var aptData = ApartmentData.ValidCreateApartmentRequest;
        HttpResponseMessage createApartmentResponse = await test.HttpClient.PostAsJsonAsync(
            "api/v1/apartments", aptData).ConfigureAwait(true);
        createApartmentResponse.EnsureSuccessStatusCode();

        Guid apartmentId = await createApartmentResponse.Content.ReadFromJsonAsync<Guid>().ConfigureAwait(true);

        // 4. Reserve Booking as the Guest using HTTP endpoint
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
            bookingId, "session_123", "intent_123", IsInstantBooking: false);
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
            bookingId2, "session_456", "intent_456", IsInstantBooking: false);
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

    // Helper 4: creates an Apartment and 1 Reserved booking, returns BOTH owner (admin) and guest tokens
    public static async Task<(Guid apartmentId, Guid bookingId, string ownerToken, string ownerEmail, string guestToken,
            string guestEmail)>
        SetupApartmentWithOwnerAsync(BaseIntegrationTest test, string password = "Password123!")
    {
        // 1. Create two distinct users: One Admin (owner) and One Guest (to reserve)
        var ownerEmail = $"owner_{Guid.CreateVersion7()}@test.com";
        var guestEmail = $"guest_{Guid.CreateVersion7()}@test.com";

        var registerOwnerCommand = new Bookify.Application.Users.RegisterUser.RegisterUserCommand(
            ownerEmail, "AdminOwner", "User", password, new DateOnly(1990, 1, 1));
        _ = await test.Sender.Send(registerOwnerCommand).ConfigureAwait(false);

        var registerGuestCommand = new Bookify.Application.Users.RegisterUser.RegisterUserCommand(
            guestEmail, "Guest", "User", password, new DateOnly(1995, 5, 5));
        _ = await test.Sender.Send(registerGuestCommand).ConfigureAwait(false);

        // 2. Promote the owner user to admin so they can create the apartment
        await test.PromoteToAdminAsync(ownerEmail).ConfigureAwait(false);

        // 3. Create Apartment via HTTP endpoint (as owner => owner is set as OwnerId)
        string ownerAccessToken = await test.GetAccessToken(ownerEmail, password).ConfigureAwait(true);
        test.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            ownerAccessToken);

        var aptData = ApartmentData.ValidCreateApartmentRequest;
        HttpResponseMessage createApartmentResponse = await test.HttpClient.PostAsJsonAsync(
            "api/v1/apartments", aptData).ConfigureAwait(true);
        createApartmentResponse.EnsureSuccessStatusCode();

        Guid apartmentId = await createApartmentResponse.Content.ReadFromJsonAsync<Guid>().ConfigureAwait(true);

        // 4. Reserve Booking as the Guest using HTTP endpoint
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
            bookingId, "session_789", "intent_789", IsInstantBooking: false);
        var confirmResult = await test.Sender.Send(confirmCommand).ConfigureAwait(false);
        if (confirmResult.IsFailure)
        {
            throw new InvalidOperationException($"Failed to authorize payment: {confirmResult.Error.Name}");
        }

        return (apartmentId, bookingId, ownerAccessToken, ownerEmail, guestAccessToken, guestEmail);
    }

    // Helper 5: creates an Apartment and 1 Completed booking (no reviews yet), returns BOTH owner (admin) and guest tokens.
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
        // 1. Setup apartment and owner
        var ownerEmail = $"owner_{Guid.CreateVersion7()}@test.com";
        var registerOwnerCommand = new Bookify.Application.Users.RegisterUser.RegisterUserCommand(
            ownerEmail, "AdminOwner", "User", password, new DateOnly(1990, 1, 1));
        _ = await test.Sender.Send(registerOwnerCommand).ConfigureAwait(false);

        await test.PromoteToAdminAsync(ownerEmail).ConfigureAwait(false);

        string ownerToken = await test.GetAccessToken(ownerEmail, password).ConfigureAwait(true);
        test.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, ownerToken);

        var aptData = ApartmentData.ValidCreateApartmentRequest;
        HttpResponseMessage createApartmentResponse = await test.HttpClient.PostAsJsonAsync(
            "api/v1/apartments", aptData).ConfigureAwait(true);
        createApartmentResponse.EnsureSuccessStatusCode();

        Guid apartmentId = await createApartmentResponse.Content.ReadFromJsonAsync<Guid>().ConfigureAwait(true);

        // 2. Setup guest
        var guestEmail = $"guest_{Guid.CreateVersion7()}@test.com";
        var registerGuestCommand = new Bookify.Application.Users.RegisterUser.RegisterUserCommand(
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
        var adminEmail = $"admin_{Guid.CreateVersion7()}@test.com";
        var guestEmail = $"guest_{Guid.CreateVersion7()}@test.com";

        var registerAdminCommand = new Bookify.Application.Users.RegisterUser.RegisterUserCommand(
            adminEmail, "Admin", "User", password, new DateOnly(1990, 1, 1));
        _ = await test.Sender.Send(registerAdminCommand).ConfigureAwait(false);

        var registerGuestCommand = new Bookify.Application.Users.RegisterUser.RegisterUserCommand(
            guestEmail, "Guest", "User", password, new DateOnly(1995, 5, 5));
        var guestUserId = (await test.Sender.Send(registerGuestCommand).ConfigureAwait(false)).Value;

        await test.PromoteToAdminAsync(adminEmail).ConfigureAwait(false);

        string adminAccessToken = await test.GetAccessToken(adminEmail, password).ConfigureAwait(true);
        test.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            adminAccessToken);

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

    // Helper 9: Setup a Booking in Cancelled + Paid state using MediatR CancelBookingCommand
    public static async Task<(Guid userId, Guid apartmentId, Guid bookingId, string guestToken, string guestEmail)>
        SetupCancelledPaidBookingAsync(BaseIntegrationTest test, string password = "Password123!")
    {
        var result = await SetupConfirmedPaidBookingAsync(test, password).ConfigureAwait(false);
        var cancelCommand = new Bookify.Application.Bookings.CancelBooking.CancelBookingCommand(
            result.bookingId, ReasonType.None, "Test cancellation");
        var cancelResult = await test.Sender.Send(cancelCommand).ConfigureAwait(false);
        if (cancelResult.IsFailure)
        {
            throw new InvalidOperationException($"Failed to cancel booking: {cancelResult.Error.Name}");
        }

        return result;
    }

    // Helper 10: Setup a Booking in Cancelled + RefundProcessing state using domain method
    public static async Task<(Guid userId, Guid apartmentId, Guid bookingId, string guestToken, string guestEmail)>
        SetupRefundProcessingBookingAsync(BaseIntegrationTest test, string password = "Password123!")
    {
        var result = await SetupCancelledPaidBookingAsync(test, password).ConfigureAwait(false);

        Booking? booking = await test.DbContext.Set<Booking>().FindAsync(result.bookingId).ConfigureAwait(false);
        booking!.InitiateRefund(booking.TotalPrice.Amount, booking.TotalPrice.Currency.Code, "Customer request");
        await test.DbContext.SaveChangesAsync().ConfigureAwait(false);

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
}
