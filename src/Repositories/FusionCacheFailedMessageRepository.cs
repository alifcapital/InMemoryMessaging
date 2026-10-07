using System.Text.Json;
using InMemoryMessaging.Configurations;
using InMemoryMessaging.Constants;
using InMemoryMessaging.Converters;
using InMemoryMessaging.Exceptions;
using InMemoryMessaging.Management.Models;
using InMemoryMessaging.Models;
using Medallion.Threading;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace InMemoryMessaging.Repositories;

/// <summary>
/// Keeps the failed messages of the service in a single entry of the cache of the application. Every change reads the
/// entry, changes it and writes it back under the distributed lock of the service, so two replicas never write over
/// the change of each other. The filters, the sorting and the pages are applied in the memory.
/// </summary>
internal sealed class FusionCacheFailedMessageRepository : IFailedMessageRepository
{
    /// <summary>
    /// How long the entry is kept. A message leaves the entry when its handlers are executed or it is rejected, so the
    /// entry is not meant to expire by itself.
    /// </summary>
    private static readonly TimeSpan StoreDuration = TimeSpan.FromDays(3650);

    /// <summary>
    /// How long a change waits for the lock of the entry. The lock is held only while the entry is read and written.
    /// </summary>
    private static readonly TimeSpan StoreLockTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The cache keeps the only copy of the failed messages, so the entry is read and written like a store, not like a
    /// cache: every replica works with the distributed cache directly, an old value is never returned instead of the
    /// current one, and a failed read or write throws instead of being ignored.
    /// </summary>
    private static readonly FusionCacheEntryOptions EntryOptions = new()
    {
        Duration = StoreDuration,
        SkipMemoryCacheRead = true,
        SkipMemoryCacheWrite = true,
        SkipBackplaneNotifications = true,
        IsFailSafeEnabled = false,
        ReThrowDistributedCacheExceptions = true,
        AllowBackgroundDistributedCacheOperations = false
    };

    private readonly IFusionCache _cache;
    private readonly IDistributedLockProvider _lockProvider;
    private readonly ILogger<FusionCacheFailedMessageRepository> _logger;
    private readonly string _storeKey;
    private readonly string _storeLockName;

    /// <summary>
    /// Creates the repository on the cache and the lock provider of the application.
    /// </summary>
    /// <exception cref="InMemoryMessagingException">If the cache of the application has no distributed cache.</exception>
    public FusionCacheFailedMessageRepository(IFusionCache cache, IDistributedLockProvider lockProvider,
        InMemoryMessagingRetrySettings options, ILogger<FusionCacheFailedMessageRepository> logger)
    {
        if (!cache.HasDistributedCache)
            throw new InMemoryMessagingException(
                "The retry of the failed in-memory messages keeps them in the distributed cache of the FusionCache, " +
                "but the FusionCache of the application has none. Configure one, or disable the retry.");

        _cache = cache;
        _lockProvider = lockProvider;
        _logger = logger;
        _storeKey = FailedMessagesKeys.Store(options.ServiceName);
        _storeLockName = FailedMessagesKeys.StoreLock(options.ServiceName);
    }

    #region Writing

    public Task<bool> AddAsync(FailedMessage message, CancellationToken cancellationToken)
    {
        return ChangeAsync(messages =>
        {
            if (messages.Exists(stored => stored.Id == message.Id))
                return false;

            messages.Add(message with { Version = 1 });
            return true;
        }, cancellationToken);
    }

    public Task<bool> UpdateAsync(FailedMessage message, CancellationToken cancellationToken)
    {
        return ChangeAsync(messages =>
        {
            var index = messages.FindIndex(stored => stored.Id == message.Id);
            if (index < 0 || messages[index].Version != message.Version)
                return false;

            messages[index] = message with { Version = message.Version + 1 };
            return true;
        }, cancellationToken);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        return ChangeAsync(messages => messages.RemoveAll(stored => stored.Id == id) > 0, cancellationToken);
    }

    #endregion

    #region Reading

    public async Task<FailedMessage> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var messages = await ReadAsync(cancellationToken);
        return messages.Find(message => message.Id == id);
    }

    public async Task<IReadOnlyList<Guid>> GetDueMessageIdsAsync(DateTimeOffset upTo, CancellationToken cancellationToken)
    {
        var messages = await ReadAsync(cancellationToken);

        return messages
            .Where(message => message.TryAfterAt <= upTo)
            .OrderBy(message => message.TryAfterAt)
            .Select(message => message.Id)
            .ToArray();
    }

    public async Task<MessagePagedList> GetMessagesAsync(MessagesFilter filter, CancellationToken cancellationToken)
    {
        var messages = await ReadAsync(cancellationToken);

        var matching = messages.Where(message => Matches(message, filter));
        var ordered = filter.SortDescending
            ? matching.OrderByDescending(message => message.CreatedAt)
            : matching.OrderBy(message => message.CreatedAt);

        // One item more than the page is taken, to know whether there is a next page.
        var items = ordered
            .Skip((filter.PageIndex - 1) * filter.PageSize)
            .Take(filter.PageSize + 1)
            .Select(MessageSummary (message) => message)
            .ToArray();

        return new MessagePagedList
        {
            Items = items.Take(filter.PageSize).ToArray(),
            PageIndex = filter.PageIndex,
            PageSize = filter.PageSize,
            HasNextPage = items.Length > filter.PageSize
        };
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Reads the entry, applies the change and writes the entry back, all under the lock of the entry.
    /// </summary>
    /// <param name="change">Changes the read messages. Returns false when there is nothing to write.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Returns the result of the change.</returns>
    private async Task<bool> ChangeAsync(Func<List<FailedMessage>, bool> change, CancellationToken cancellationToken)
    {
        await using var storeLock = await _lockProvider.AcquireLockAsync(_storeLockName, StoreLockTimeout, cancellationToken);

        var messages = await ReadAsync(cancellationToken);
        if (!change(messages))
            return false;

        await WriteAsync(messages, cancellationToken);
        return true;
    }

    /// <summary>
    /// Reads all failed messages of the service.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Returns the messages, or an empty list when there is none.</returns>
    private async Task<List<FailedMessage>> ReadAsync(CancellationToken cancellationToken)
    {
        var data = await _cache.GetOrDefaultAsync<string>(_storeKey, options: EntryOptions, token: cancellationToken);
        if (string.IsNullOrEmpty(data))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<FailedMessage>>(data, MessageJsonConverters.Options) ?? [];
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not read the stored failed in-memory messages of the '{StoreKey}' key.", _storeKey);
            throw;
        }
    }

    /// <summary>
    /// Writes all failed messages of the service. The entry is removed when no message is left.
    /// </summary>
    /// <param name="messages">The messages to keep.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private async Task WriteAsync(List<FailedMessage> messages, CancellationToken cancellationToken)
    {
        if (messages.Count == 0)
        {
            await _cache.RemoveAsync(_storeKey, options: EntryOptions, token: cancellationToken);
            return;
        }

        var data = JsonSerializer.Serialize(messages, MessageJsonConverters.Options);
        await _cache.SetAsync(_storeKey, data, options: EntryOptions, token: cancellationToken);
    }

    /// <summary>
    /// Checks the message against every filter which is set.
    /// </summary>
    /// <param name="message">The stored message.</param>
    /// <param name="filter">The filter of the messages.</param>
    /// <returns>Returns true when the message matches every filter which is set.</returns>
    private static bool Matches(FailedMessage message, MessagesFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.MessageName) && message.MessageName != filter.MessageName) return false;
        if (filter.CreatedFrom.HasValue && message.CreatedAt < filter.CreatedFrom.Value) return false;
        if (filter.CreatedTo.HasValue && message.CreatedAt > filter.CreatedTo.Value) return false;
        if (filter.UpdatedFrom.HasValue && message.UpdatedAt < filter.UpdatedFrom.Value) return false;
        if (filter.UpdatedTo.HasValue && message.UpdatedAt > filter.UpdatedTo.Value) return false;
        if (filter.MinTryCount.HasValue && message.TryCount < filter.MinTryCount.Value) return false;
        if (!Contains(message.UpdatedBy, filter.UpdatedBy)) return false;
        if (!Contains(message.FailureReason, filter.FailureReasonContains)) return false;
        if (!Contains(message.Payload, filter.PayloadContains)) return false;

        if (!string.IsNullOrWhiteSpace(filter.HandlerPath) &&
            !message.Handlers.Any(handler => handler.HandlerPath.Contains(filter.HandlerPath, StringComparison.OrdinalIgnoreCase)))
            return false;

        return true;
    }

    /// <summary>
    /// Checks the value against a text filter, ignoring the case.
    /// </summary>
    /// <param name="value">The value of the message.</param>
    /// <param name="searchedText">The text to find. An empty one matches everything.</param>
    /// <returns>Returns true when the value contains the searched text.</returns>
    private static bool Contains(string value, string searchedText)
    {
        if (string.IsNullOrWhiteSpace(searchedText))
            return true;

        return value is not null && value.Contains(searchedText, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
