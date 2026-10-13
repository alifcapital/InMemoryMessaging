using System.Text.Json;
using InMemoryMessaging.Configurations;
using InMemoryMessaging.Constants;
using InMemoryMessaging.Converters;
using InMemoryMessaging.Exceptions;
using InMemoryMessaging.Management.Models;
using Medallion.Threading;
using ZiggyCreatures.Caching.Fusion;

namespace InMemoryMessaging.Repositories;

/// <summary>
/// Keeps the failed messages of the service in a single entry of the cache of the application. Every change reads the
/// entry, changes it and writes it back under the store lock, so two changes never overwrite each other. Reading does
/// not take the lock. The filters, the sorting and the pages are applied in the memory.
/// </summary>
internal sealed class FusionCacheFailedMessageRepository : IFailedMessageRepository
{
    /// <summary>
    /// How long the entry is kept. A message leaves the entry when its handlers are executed or it is rejected, so the
    /// entry is not meant to expire by itself.
    /// </summary>
    private static readonly TimeSpan StoreDuration = TimeSpan.FromDays(3650);

    /// <summary>
    /// How long a change waits for the store lock. The lock is held only while the entry is read and written, so a
    /// longer wait means the lock provider has a problem.
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
    private readonly string _storeKey;
    private readonly string _storeLockName;

    /// <summary>
    /// Creates the repository on the cache and the lock provider of the application.
    /// </summary>
    /// <exception cref="InMemoryMessagingException">If the cache of the application has no distributed cache.</exception>
    public FusionCacheFailedMessageRepository(IFusionCache cache, IDistributedLockProvider lockProvider,
        InMemoryMessagingRetryOptions options)
    {
        if (!cache.HasDistributedCache)
            throw new InMemoryMessagingException(
                "The retry of the in-memory messages needs a distributed cache in the FusionCache. Add one to the application or disable the retry.");

        _cache = cache;
        _lockProvider = lockProvider;
        _storeKey = FailedMessagesKeys.Store(options.ServiceName);
        _storeLockName = FailedMessagesKeys.StoreLock(options.ServiceName);
    }

    #region Writing

    public Task AddAsync(MessageDetails message, CancellationToken cancellationToken)
    {
        return ChangeAsync(messages =>
        {
            messages.Add(message);
            return true;
        }, cancellationToken);
    }

    public Task<bool> UpdateAsync(MessageDetails message, CancellationToken cancellationToken)
    {
        return ChangeAsync(messages =>
        {
            var index = messages.FindIndex(stored => stored.Id == message.Id);
            if (index < 0)
                return false;

            messages[index] = message;
            return true;
        }, cancellationToken);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        return ChangeAsync(messages => messages.RemoveAll(stored => stored.Id == id) > 0, cancellationToken);
    }

    #endregion

    #region Reading

    public async Task<MessageDetails> GetByIdAsync(Guid id, CancellationToken cancellationToken)
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

        var matching = messages.Where(message => MatchesFilter(message, filter));
        var ordered = filter.SortDescending
            ? matching.OrderByDescending(message => message.CreatedAt)
            : matching.OrderBy(message => message.CreatedAt);

        /*
         * One item more than the page size is taken. If that extra item exists, there is a next page. The extra
         * item itself is not returned.
         */
        var items = ordered
            .Skip((filter.PageIndex - 1) * filter.PageSize)
            .Take(filter.PageSize + 1)
            .Cast<MessageSummary>()
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
    /// Reads the entry, applies the change and writes the entry back, all under the store lock.
    /// </summary>
    /// <param name="change">Changes the read messages. Returns false when there is nothing to write.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Returns the result of the change.</returns>
    private async Task<bool> ChangeAsync(Func<List<MessageDetails>, bool> change, CancellationToken cancellationToken)
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
    private async Task<List<MessageDetails>> ReadAsync(CancellationToken cancellationToken)
    {
        var data = await _cache.GetOrDefaultAsync<string>(_storeKey, options: EntryOptions, token: cancellationToken);
        if (string.IsNullOrEmpty(data))
            return [];

        return JsonSerializer.Deserialize<List<MessageDetails>>(data, MessageJsonConverters.Options) ?? [];
    }

    /// <summary>
    /// Writes all failed messages of the service. The entry is removed when no message is left.
    /// </summary>
    /// <param name="messages">The messages to keep.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private async Task WriteAsync(List<MessageDetails> messages, CancellationToken cancellationToken)
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
    private static bool MatchesFilter(MessageDetails message, MessagesFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.MessageName) && message.MessageName != filter.MessageName) return false;
        if (filter.CreatedFrom.HasValue && message.CreatedAt < filter.CreatedFrom.Value) return false;
        if (filter.CreatedTo.HasValue && message.CreatedAt > filter.CreatedTo.Value) return false;
        if (filter.UpdatedFrom.HasValue && message.UpdatedAt < filter.UpdatedFrom.Value) return false;
        if (filter.UpdatedTo.HasValue && message.UpdatedAt > filter.UpdatedTo.Value) return false;
        if (filter.MinTryCount.HasValue && message.TryCount < filter.MinTryCount.Value) return false;
        if (!ContainsIgnoringCase(message.UpdatedBy, filter.UpdatedBy)) return false;
        if (!ContainsIgnoringCase(message.FailureReason, filter.FailureReasonContains)) return false;
        if (!ContainsIgnoringCase(message.Payload, filter.PayloadContains)) return false;

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
    private static bool ContainsIgnoringCase(string value, string searchedText)
    {
        if (string.IsNullOrWhiteSpace(searchedText))
            return true;

        return value is not null && value.Contains(searchedText, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
