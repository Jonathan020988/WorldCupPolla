using System.Collections.Concurrent;

namespace WorldCup.Api.Services
{
    public class AttemptRateLimiter
    {
        private readonly ConcurrentDictionary<string, AttemptWindow> _windows = new();

        public bool Allow(
            string key,
            int limit,
            TimeSpan window,
            out TimeSpan retryAfter)
        {
            var now = DateTimeOffset.UtcNow;
            var current = _windows.AddOrUpdate(
                key,
                _ => new AttemptWindow(1, now.Add(window)),
                (_, existing) =>
                {
                    if (existing.ResetAt <= now)
                    {
                        return new AttemptWindow(1, now.Add(window));
                    }

                    return existing with { Count = existing.Count + 1 };
                });

            retryAfter = current.ResetAt > now
                ? current.ResetAt - now
                : TimeSpan.Zero;

            if (_windows.Count > 5000)
            {
                foreach (var pair in _windows.Where(pair => pair.Value.ResetAt <= now).Take(1000))
                {
                    _windows.TryRemove(pair.Key, out _);
                }
            }

            return current.Count <= limit;
        }

        private sealed record AttemptWindow(int Count, DateTimeOffset ResetAt);
    }
}
