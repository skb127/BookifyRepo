namespace Bookify.Api.Controllers.Users.Requests;

public record InitiateEmailChangeRequest(string NewEmail, string CurrentPassword);
