namespace Cirreum.Coordination;

/// <summary>
/// The result of an <see cref="IRequestThrottle.RecordAsync"/> call.
/// </summary>
/// <param name="Count">The number of hits recorded in the current window, this hit included.</param>
/// <param name="Allowed"><see langword="true"/> when <paramref name="Count"/> is within the limit.</param>
/// <param name="RetryAfter">When not allowed, the remaining time until the current window resets;
/// otherwise <see langword="null"/>. Suitable for a <c>Retry-After</c> response hint.</param>
public readonly record struct ThrottleOutcome(long Count, bool Allowed, TimeSpan? RetryAfter);
