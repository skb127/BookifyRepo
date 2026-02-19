using Bookify.Domain.Abstractions;

namespace Bookify.Application.Abstractions.Security;

public interface ITurnstileValidator
{
    Task<Result> Validate(string token, CancellationToken cancellationToken);
}
