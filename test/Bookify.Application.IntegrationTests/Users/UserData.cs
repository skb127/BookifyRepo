using Bookify.Api.Controllers.Users;

namespace Bookify.Application.IntegrationTests.Users;

internal static class UserData
{
    public static readonly RegisterUserRequest RegisterTestUserRequest = new("test@test.com", "name", "lastname", "ClaveSegura1$", new DateOnly(2000, 1, 1));
    public static readonly RegisterUserRequest LoginUserRequest = new("login@test.com", "Login", "User", "LoginPass123!", new DateOnly(2000, 1, 1));
    public static readonly RegisterUserRequest RefreshTokenUserRequest = new("refresh@test.com", "Refresh", "User", "RefreshPass123!", new DateOnly(2000, 1, 1));
    public static readonly RegisterUserRequest ExistingUserRequest = new("existing@test.com", "Existing", "User", "ExistingPass123!", new DateOnly(2000, 1, 1));
    public static readonly RegisterUserRequest LogoutTestUserRequest = new("logout@test.com", "logout", "lastname", "ClaveSegura6$", new DateOnly(2000, 1, 1));
    public static readonly RegisterUserRequest ChangePasswordUserRequest = new("changepassword@test.com", "Change", "User", "TestPassword123!", new DateOnly(2000, 1, 1));
    public static readonly RegisterUserRequest ChangePasswordUserRequest2 = new("changepassword2@test.com", "Change2", "User", "TestPassword123!", new DateOnly(2000, 1, 1));
    public static readonly RegisterUserRequest ChangeEmailUserRequest = new("changeemail@test.com", "Email", "Changer", "EmailChange123!", new DateOnly(2000, 1, 1));
    public static readonly RegisterUserRequest ChangeEmailUserRequest2 = new("changeemail2@test.com", "Email2", "Changer2", "EmailChange456!", new DateOnly(2000, 1, 1));
    public static readonly RegisterUserRequest ChangeEmailPendingUserRequest = new("changeemail_pending@test.com", "Pending", "EmailChanger", "PendingEmail123!", new DateOnly(2000, 1, 1));
    public static readonly RegisterUserRequest PasswordRecoveryUserRequest = new("recovery@test.com", "Recovery", "User", "RecoveryPass123!", new DateOnly(2000, 1, 1));
    public static readonly RegisterUserRequest PasswordResetUserRequest = new("reset@test.com", "Reset", "User", "ResetPass123!", new DateOnly(2000, 1, 1));
    public static readonly RegisterUserRequest UpdateProfileUserRequest = new("updateprofile@test.com", "Profile", "User", "ProfilePass123!", new DateOnly(2000, 1, 1));
    public static readonly RegisterUserRequest UpdateProfileUserRequest2 = new("updateprofile2@test.com", "Profile2", "User2", "ProfilePass456!", new DateOnly(2000, 1, 1));
    public static readonly RegisterUserRequest GetUserByIdUserRequest = new("getuserbyid@test.com", "Admin", "User", "AdminPass123!", new DateOnly(1995, 6, 15));
    public static readonly RegisterUserRequest RevokeSessionsUserRequest = new("revokesessions@test.com", "Revoke", "User", "RevokePass123!", new DateOnly(2000, 1, 1));

    // Dedicated Users for Apartment tests
    public static readonly RegisterUserRequest CreateApartmentStandardUserRequest = new("createapt_std@test.com", "AptStd", "User", "CreateApt123!", new DateOnly(2000, 1, 1));
    public static readonly RegisterUserRequest CreateApartmentAdminUserRequest = new("createapt_admin@test.com", "AptAdmin", "User", "CreateApt123!", new DateOnly(1995, 6, 15));

    // Dedicated Users for UpdateApartment tests
    public static readonly RegisterUserRequest UpdateApartmentStandardUserRequest = new("updateapt_std@test.com", "UpdateStd", "User", "UpdateApt123!", new DateOnly(2000, 1, 1));
    public static readonly RegisterUserRequest UpdateApartmentAdminUserRequest = new("updateapt_admin@test.com", "UpdateAdmin", "User", "UpdateApt123!", new DateOnly(1995, 6, 15));

    // Dedicated Users for DeleteApartment tests
    public static readonly RegisterUserRequest DeleteApartmentStandardUserRequest = new("deleteapt_std@test.com", "DeleteStd", "User", "DeleteApt123!", new DateOnly(2000, 1, 1));
    public static readonly RegisterUserRequest DeleteApartmentAdminUserRequest = new("deleteapt_admin@test.com", "DeleteAdmin", "User", "DeleteApt123!", new DateOnly(1995, 6, 15));

    // Dedicated Users for UpdateReview tests
    public static readonly RegisterUserRequest UpdateReviewSecondaryUserRequest = new("updatereview_sec@test.com", "UpdateSec", "User", "UpdateRev123!", new DateOnly(2000, 1, 1));

    // Dedicated Users for DeleteReview tests
    public static readonly RegisterUserRequest DeleteReviewSecondaryUserRequest = new("deletereview_sec@test.com", "DeleteSec", "User", "DeleteRev123!", new DateOnly(2000, 1, 1));
    public static readonly RegisterUserRequest DeleteReviewTertiaryUserRequest = new("deletereview_tertiary@test.com", "Tertiary", "User", "Password123!", new DateOnly(2000, 1, 1));

    // Dedicated Users for GetAllReviews tests
    public static readonly RegisterUserRequest GetAllReviewsAdminUserRequest = new("getallreviews_admin@test.com", "AllRevAdmin", "User", "AllRevAdmin123!", new DateOnly(1995, 6, 15));
    public static readonly RegisterUserRequest GetAllReviewsRegularUserRequest = new("getallreviews_regular@test.com", "AllRevRegular", "User", "AllRevRegular123!", new DateOnly(2000, 1, 1));

    // Dedicated users for cache invalidation E2E tests
    public static readonly RegisterUserRequest CacheInvalidationAdminUserRequest = new("cacheinvalidation_admin@test.com", "CacheAdmin", "User", "CacheAdmin123!", new DateOnly(1995, 6, 15));
    public static readonly RegisterUserRequest CacheInvalidationUserRequest = new("cacheinvalidation_user@test.com", "CacheUser", "User", "CacheUser123!", new DateOnly(1990, 3, 20));

    public const string FirstName = "Test";
    public const string LastName = "User";
    public const string Email = "test@test.com";
}
