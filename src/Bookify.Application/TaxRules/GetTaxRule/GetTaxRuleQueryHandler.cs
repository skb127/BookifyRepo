using System.Data;
using Bookify.Application.Abstractions.Data;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Abstractions;
using Bookify.Domain.TaxRules;
using Dapper;

namespace Bookify.Application.TaxRules.GetTaxRule;

internal sealed class GetTaxRuleQueryHandler : IQueryHandler<GetTaxRuleQuery, TaxRuleResponse>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public GetTaxRuleQueryHandler(ISqlConnectionFactory sqlConnectionFactory) =>
        _sqlConnectionFactory = sqlConnectionFactory;

    public async Task<Result<TaxRuleResponse>> Handle(GetTaxRuleQuery request, CancellationToken cancellationToken)
    {
        using IDbConnection connection = _sqlConnectionFactory.CreateConnection();

        const string sql = """
            SELECT
                id AS Id,
                country_code AS CountryCode,
                region AS Region,
                city AS City,
                rate_value AS RateValue,
                rate_type AS RateType,
                name AS Name,
                effective_from AS EffectiveFrom,
                effective_to AS EffectiveTo,
                is_active AS IsActive
            FROM tax_rules
            WHERE id = @TaxRuleId
            """;

        TaxRuleResponse? taxRule = await connection.QueryFirstOrDefaultAsync<TaxRuleResponse>(
            sql,
            new
            {
                request.TaxRuleId
            });

        if (taxRule is null)
        {
            return Result.Failure<TaxRuleResponse>(TaxRuleErrors.NotFound);
        }

        return taxRule;
    }
}
