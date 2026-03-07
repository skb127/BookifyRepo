using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using Bookify.Domain.Shared;

namespace Bookify.Application.Apartments.UpdateApartment;

internal sealed class UpdateApartmentCommandHandler : ICommandHandler<UpdateApartmentCommand>
{
    private readonly IApartmentRepository _apartmentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateApartmentCommandHandler(
        IApartmentRepository apartmentRepository,
        IUnitOfWork unitOfWork)
    {
        _apartmentRepository = apartmentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(UpdateApartmentCommand request, CancellationToken cancellationToken)
    {
        Apartment? apartment = await _apartmentRepository.GetByIdAsync(request.Id, cancellationToken);

        if (apartment is null)
        {
            return Result.Failure(ApartmentErrors.NotFound);
        }

        try
        {
            var priceCurrency = Currency.FromCode(request.PriceCurrency);
            var cleaningFeeCurrency = Currency.FromCode(request.CleaningFeeCurrency);

            var address = new Address(
                request.Country,
                request.State,
                request.ZipCode,
                request.City,
                request.Street);

            var price = new Money(request.PriceAmount, priceCurrency);
            var cleaningFee = new Money(request.CleaningFeeAmount, cleaningFeeCurrency);

            var amenities = request.Amenities
                .Select(a => (Amenity)a).ToList();

            apartment.Update(
                new Name(request.Name),
                new Description(request.Description),
                address,
                price,
                cleaningFee,
                amenities);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (InvalidOperationException)
        {
            return Result.Failure(ApartmentErrors.InvalidCurrency);
        }
    }
}
