using Bookify.Application.Users.FinalizeAccountDeletion;
using Bookify.Domain.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Bookify.Infrastructure.Users;

[DisallowConcurrentExecution]
internal sealed class FinalizeAccountDeletionJob : IJob
{
    private readonly ISender _sender;
    private readonly ILogger<FinalizeAccountDeletionJob> _logger;

    public FinalizeAccountDeletionJob(ISender sender, ILogger<FinalizeAccountDeletionJob> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        string? userIdStr = context.MergedJobDataMap.GetString("UserId");

        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out Guid userId))
        {
            _logger.LogWarning("Invalid UserId in FinalizeAccountDeletionJob execution context.");
            return;
        }

        _logger.LogInformation("Executing FinalizeAccountDeletionJob for User ID {UserId}.", userId);

        Result result = await _sender.Send(new FinalizeAccountDeletionCommand(userId), context.CancellationToken);

        if (result.IsFailure)
        {
            _logger.LogError("Failed to finalize account deletion for User ID {UserId}. Error: {Error}", userId, result.Error);
            throw new JobExecutionException($"Failed to finalize account deletion for user {userId}: {result.Error.Code}");
        }

        _logger.LogInformation("Successfully completed FinalizeAccountDeletionJob for User ID {UserId}.", userId);
    }
}
