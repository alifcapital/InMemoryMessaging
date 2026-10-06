using InMemoryMessaging.Configurations;
using InMemoryMessaging.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InMemoryMessaging.BackgroundServices;

/// <summary>
/// Retries the handlers of the failed messages whose time has come, round by round, and survives a failure of a
/// single round.
/// </summary>
internal sealed class FailedMessagesProcessorJob(
    IFailedMessagesProcessor processor,
    InMemoryMessagingRetrySettings options,
    ILogger<FailedMessagesProcessorJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.SecondsToDelayProcessMessages));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    await processor.RetryDueMessagesAsync(cancellationToken);
                }
                catch (Exception exception)
                {
                    // Waits longer after a failure, to not hammer the store and the logs while it is unavailable.
                    logger.LogCritical(exception, "Something is wrong while retrying the failed in-memory messages.");
                    await Task.Delay(TimeSpan.FromMinutes(options.MinutesToDelayAfterFailedRound), cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The application is stopping.
        }
    }
}
