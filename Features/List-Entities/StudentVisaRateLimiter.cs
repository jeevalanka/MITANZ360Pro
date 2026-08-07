using System.Collections.Concurrent;

namespace MITANZ360Pro.Web.Modules.Entities;

/// <summary>Simple in-memory rate limiter for the public student visa endpoint.</summary>
public interface IStudentVisaRateLimiter
{
    bool TryAcquire(string key, out string? errorMessage);
}

public sealed class StudentVisaRateLimiter : IStudentVisaRateLimiter
{
    private readonly ConcurrentDictionary<string, Queue<DateTimeOffset>> _hits = new();
    private readonly TimeSpan _window = TimeSpan.FromMinutes(15);
    private readonly int _maxRequests = 8;

    public bool TryAcquire(string key, out string? errorMessage)
    {
        errorMessage = null;
        var normalized = string.IsNullOrWhiteSpace(key) ? "unknown" : key.Trim();
        var now = DateTimeOffset.UtcNow;
        var queue = _hits.GetOrAdd(normalized, _ => new Queue<DateTimeOffset>());

        lock (queue)
        {
            while (queue.Count > 0 && now - queue.Peek() > _window)
            {
                queue.Dequeue();
            }

            if (queue.Count >= _maxRequests)
            {
                errorMessage = "Too many registration attempts. Please wait a few minutes and try again.";
                return false;
            }

            queue.Enqueue(now);
            return true;
        }
    }
}
