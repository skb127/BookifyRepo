namespace Bookify.Api.Controllers.Users;

public record InitiateEmailChangeRequest(string NewEmail, string CurrentPassword);
