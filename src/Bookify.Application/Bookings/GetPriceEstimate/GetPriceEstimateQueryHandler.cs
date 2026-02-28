using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;

namespace Bookify.Application.Bookings.GetPriceEstimate;

internal sealed class GetPriceEstimateQueryHandler : IQueryHandler<GetPriceEstimateQuery, PriceEstimateResponse>
{
    private readonly IApartmentRepository _apartmentRepository;
    private readonly PricingService _pricingService;

    public GetPriceEstimateQueryHandler(
        IApartmentRepository apartmentRepository,
        PricingService pricingService)
    {
        _apartmentRepository = apartmentRepository;
        _pricingService = pricingService;
    }

    public async Task<Result<PriceEstimateResponse>> Handle(GetPriceEstimateQuery request, CancellationToken cancellationToken)
    {
        Apartment? apartment = await _apartmentRepository.GetByIdAsync(request.ApartmentId, cancellationToken);

        if (apartment is null)
        {
            return Result.Failure<PriceEstimateResponse>(ApartmentErrors.NotFound);
        }

        var dateRange = DateRange.Create(request.StartDate, request.EndDate);

        PricingDetails pricingDetails = _pricingService.CalculatePrice(apartment, dateRange);

        return new PriceEstimateResponse(
            pricingDetails.PriceForPeriod.Amount,
            pricingDetails.PriceForPeriod.Currency.Code,
            pricingDetails.CleaningFee.Amount,
            pricingDetails.CleaningFee.Currency.Code,
            pricingDetails.AmenitiesUpCharge.Amount,
            pricingDetails.AmenitiesUpCharge.Currency.Code,
            pricingDetails.TotalPrice.Amount,
            pricingDetails.TotalPrice.Currency.Code,
            dateRange.LengthInDays);
    }
}
