using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Abstractions;
using Bookify.Domain.TaxRules;

namespace Bookify.Application.TaxRules.CreateTaxRule;

internal sealed class CreateTaxRuleCommandHandler : ICommandHandler<CreateTaxRuleCommand, Guid>
{
    private readonly ITaxRuleRepository _taxRuleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CreateTaxRuleCommandHandler(
        ITaxRuleRepository taxRuleRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _taxRuleRepository = taxRuleRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<Guid>> Handle(CreateTaxRuleCommand request, CancellationToken cancellationToken)
    {
        var taxRate = TaxRate.Create(request.RateValue, (TaxType)request.RateType);

        var taxRule = TaxRule.Create(
            request.CountryCode,
            request.Region,
            request.City,
            taxRate,
            request.Name,
            request.EffectiveFrom,
            request.EffectiveTo,
            _dateTimeProvider.UtcNow);

        _taxRuleRepository.Add(taxRule);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return taxRule.Id;
    }
}
