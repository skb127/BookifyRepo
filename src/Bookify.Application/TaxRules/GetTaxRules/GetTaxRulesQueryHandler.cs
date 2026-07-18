using System.Data;
using System.Text;
using Bookify.Application.Abstractions.Data;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Abstractions;
using Dapper;

namespace Bookify.Application.TaxRules.GetTaxRules;

internal sealed class GetTaxRulesQueryHandler : IQueryHandler<GetTaxRulesQuery, IReadOnlyList<TaxRuleResponse>>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public GetTaxRulesQueryHandler(ISqlConnectionFactory sqlConnectionFactory) =>
        _sqlConnectionFactory = sqlConnectionFactory;

    public async Task<Result<IReadOnlyList<TaxRuleResponse>>> Handle(GetTaxRulesQuery request, CancellationToken cancellationToken)
    {
        using IDbConnection connection = _sqlConnectionFactory.CreateConnection();

        var builder = new StringBuilder();
        var parameters = new DynamicParameters();

        builder.AppendLine($"""
            SELECT 
                id AS Id, country_code AS CountryCode, 
                region AS Region, 
                city AS City, 
                rate_value AS RateValue, 
                rate_type AS RateType, 
                name AS Name, 
                effective_from AS EffectiveFrom, 
                effective_to AS EffectiveTo, 
                is_active AS IsActive 
            FROM tax_rules
        """);

        if (!string.IsNullOrWhiteSpace(request.CountryCode))
        {
            builder.AppendLine("WHERE country_code = @CountryCode");
            parameters.Add("CountryCode", request.CountryCode);
        }

        builder.AppendLine("ORDER BY country_code, name");

        IEnumerable<TaxRuleResponse> taxRules = await connection.QueryAsync<TaxRuleResponse>(
            builder.ToString(),
            parameters);

        return Result.Success<IReadOnlyList<TaxRuleResponse>>([.. taxRules]);
    }
}
