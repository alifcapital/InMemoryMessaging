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
    InMemoryMessagingRetryOptions options,
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
                catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
                {
                    /*
                     * The whole round failed, for example because the cache or the lock provider is unavailable. The
                     * job waits longer before the next round, so it does not fill the logs with the same error every
                     * second. A round which is cancelled because the application is stopping is not a failure, so it
                     * is not caught here.
                     */
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
