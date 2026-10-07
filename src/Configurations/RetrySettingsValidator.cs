using InMemoryMessaging.Exceptions;

namespace InMemoryMessaging.Configurations;

/// <summary>
/// Checks the settings of the retry on the start of the application, so a wrong value is reported at once rather than
/// on the first failed message.
/// </summary>
internal static class RetrySettingsValidator
{
    /// <summary>
    /// Checks that the settings of an enabled retry are filled and usable.
    /// </summary>
    /// <param name="options">The settings of the retry.</param>
    /// <exception cref="InMemoryMessagingException">If the retry is enabled, but its settings are wrong.</exception>
    internal static void Validate(InMemoryMessagingRetrySettings options)
    {
        if (!options.IsEnabled)
            return;

        if (string.IsNullOrWhiteSpace(options.ServiceName))
            throw new InMemoryMessagingException(
                "The service name is required to retry the failed in-memory messages, and it must be unique per service.");

        EnsureIsPositive(options.MaxConcurrency, nameof(options.MaxConcurrency));
        EnsureIsPositive(options.TryCount, nameof(options.TryCount));
        EnsureIsPositive(options.TryAfterSeconds, nameof(options.TryAfterSeconds));
        EnsureIsPositive(options.TryAfterMinutesIfTryCountExceeded, nameof(options.TryAfterMinutesIfTryCountExceeded));
        EnsureIsPositive(options.TryAfterMinutesIfMessageOrHandlerNotFound, nameof(options.TryAfterMinutesIfMessageOrHandlerNotFound));
        EnsureIsPositive(options.SecondsToDelayProcessMessages, nameof(options.SecondsToDelayProcessMessages));
        EnsureIsPositive(options.MinutesToDelayAfterFailedRound, nameof(options.MinutesToDelayAfterFailedRound));
        EnsureIsNotNegative(options.MaxFailureReasonLength, nameof(options.MaxFailureReasonLength));
    }

    #region Private Methods

    /// <summary>
    /// Checks that the option is at least one.
    /// </summary>
    /// <param name="value">The value of the option.</param>
    /// <param name="name">The name of the option.</param>
    /// <exception cref="InMemoryMessagingException">If the value is lower than one.</exception>
    private static void EnsureIsPositive(int value, string name)
    {
        if (value < 1)
            throw new InMemoryMessagingException(
                $"The '{name}' option of the retry must be at least 1, but it is {value}.");
    }

    /// <summary>
    /// Checks that the option is not negative. Such an option is switched off by the zero value.
    /// </summary>
    /// <param name="value">The value of the option.</param>
    /// <param name="name">The name of the option.</param>
    /// <exception cref="InMemoryMessagingException">If the value is negative.</exception>
    private static void EnsureIsNotNegative(int value, string name)
    {
        if (value < 0)
            throw new InMemoryMessagingException(
                $"The '{name}' option of the retry cannot be negative, but it is {value}.");
    }

    #endregion
}
