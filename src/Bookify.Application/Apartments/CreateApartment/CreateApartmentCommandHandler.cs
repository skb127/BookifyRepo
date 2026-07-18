using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using Bookify.Domain.Shared;

namespace Bookify.Application.Apartments.CreateApartment;

internal sealed class CreateApartmentCommandHandler : ICommandHandler<CreateApartmentCommand, Guid>
{
    private readonly IApartmentRepository _apartmentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserContext _userContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CreateApartmentCommandHandler(
        IApartmentRepository apartmentRepository,
        IUnitOfWork unitOfWork,
        IUserContext userContext, IDateTimeProvider dateTimeProvider)
    {
        _apartmentRepository = apartmentRepository;
        _unitOfWork = unitOfWork;
        _userContext = userContext;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<Guid>> Handle(CreateApartmentCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var priceCurrency = Currency.FromCode(request.PriceCurrency);
            var cleaningFeeCurrency = Currency.FromCode(request.CleaningFeeCurrency);
            Currency extraGuestFeeCurrency = request.ExtraGuestFeeAmount == 0
                ? priceCurrency
                : Currency.FromCode(request.ExtraGuestFeeCurrency);

            var address = new Address(
                request.Country,
                request.State,
                request.ZipCode,
                request.City,
                request.Street);

            var price = new Money(request.PriceAmount, priceCurrency);
            var cleaningFee = new Money(request.CleaningFeeAmount, cleaningFeeCurrency);
            var extraGuestFee = new Money(request.ExtraGuestFeeAmount, extraGuestFeeCurrency);

            var amenities = request.Amenities
                .Select(a => (Amenity)a).ToList();

            var apartment = Apartment.Create(
                _userContext.UserId,
                new Name(request.Name),
                new Description(request.Description),
                address,
                price,
                cleaningFee,
                amenities,
                _dateTimeProvider.UtcNow,
                request.InstantBooking,
                request.CancellationPolicyId,
                request.MinimumNights,
                request.CheckInCutOffHours,
                request.BaseGuests,
                request.MaxGuests,
                extraGuestFee);

            _apartmentRepository.Add(apartment);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return apartment.Id;
        }
        catch (InvalidOperationException)
        {
            return Result.Failure<Guid>(ApartmentErrors.InvalidCurrency);
        }
    }
}
