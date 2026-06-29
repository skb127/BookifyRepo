using FluentValidation;

namespace Bookify.Application.Bookings.GetCancellationPreview;

internal sealed class GetCancellationPreviewQueryValidator : AbstractValidator<GetCancellationPreviewQuery>
{
    public GetCancellationPreviewQueryValidator() =>
        RuleFor(c => c.BookingId).NotEmpty();
}
