namespace Bookify.Application.Common;

public interface IPagedQuery
{
    int Page { get; }

    int PageSize { get; }
}
