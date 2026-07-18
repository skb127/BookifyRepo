using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Abstractions;
using Bookify.Domain.TaxRules;

namespace Bookify.Application.TaxRules.UpdateTaxRule;

internal sealed class UpdateTaxRuleCommandHandler : ICommandHandler<UpdateTaxRuleCommand>
{
    private readonly ITaxRuleRepository _taxRuleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public UpdateTaxRuleCommandHandler(
        ITaxRuleRepository taxRuleRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _taxRuleRepository = taxRuleRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result> Handle(UpdateTaxRuleCommand request, CancellationToken cancellationToken)
    {
        TaxRule? taxRule = await _taxRuleRepository.GetByIdAsync(request.Id, cancellationToken);

        if (taxRule is null)
        {
            return Result.Failure(TaxRuleErrors.NotFound);
        }

        var taxRate = TaxRate.Create(request.RateValue, (TaxType)request.RateType);

        taxRule.Update(
            request.CountryCode,
            request.Region,
            request.City,
            taxRate,
            request.Name,
            request.EffectiveFrom,
            request.EffectiveTo,
            _dateTimeProvider.UtcNow);

        _taxRuleRepository.Update(taxRule);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
