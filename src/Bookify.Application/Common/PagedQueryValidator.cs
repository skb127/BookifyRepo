using FluentValidation;

namespace Bookify.Application.Common;

internal abstract class PagedQueryValidator<T> : AbstractValidator<T>
    where T : IPagedQuery
{
    protected PagedQueryValidator()
    {
        RuleFor(q => q.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(q => q.PageSize)
            .GreaterThanOrEqualTo(1)
            .LessThanOrEqualTo(100);
    }
}
