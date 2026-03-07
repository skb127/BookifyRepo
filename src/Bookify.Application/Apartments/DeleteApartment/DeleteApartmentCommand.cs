using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.Apartments.DeleteApartment;

public sealed record DeleteApartmentCommand(Guid ApartmentId) : ICommand;
