using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Abstractions;
using Bookify.Domain.TaxRules;

namespace Bookify.Application.TaxRules.DeactivateTaxRule;

internal sealed class DeactivateTaxRuleCommandHandler : ICommandHandler<DeactivateTaxRuleCommand>
{
    private readonly ITaxRuleRepository _taxRuleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public DeactivateTaxRuleCommandHandler(
        ITaxRuleRepository taxRuleRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _taxRuleRepository = taxRuleRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result> Handle(DeactivateTaxRuleCommand request, CancellationToken cancellationToken)
    {
        TaxRule? taxRule = await _taxRuleRepository.GetByIdAsync(request.Id, cancellationToken);

        if (taxRule is null)
        {
            return Result.Failure(TaxRuleErrors.NotFound);
        }

        if (!taxRule.IsActive)
        {
            return Result.Failure(TaxRuleErrors.AlreadyInactive);
        }

        taxRule.Deactivate(_dateTimeProvider.UtcNow);

        _taxRuleRepository.Update(taxRule);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
