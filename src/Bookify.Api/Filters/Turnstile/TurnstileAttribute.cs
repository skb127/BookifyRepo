using Microsoft.AspNetCore.Mvc;

namespace Bookify.Api.Filters.Turnstile;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
internal sealed class TurnstileAttribute : TypeFilterAttribute
{
    public TurnstileAttribute() : base(typeof(TurnstileFilter))
    {
    }
}
