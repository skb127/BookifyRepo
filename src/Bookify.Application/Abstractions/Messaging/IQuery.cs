using Bookify.Domain.Abstractions;
using MediatR;

namespace Bookify.Application.Abstractions.Messaging;

public interface IBaseQuery
{
}

public interface IQuery<TResponse> : IRequest<Result<TResponse>>, IBaseQuery
{
}
