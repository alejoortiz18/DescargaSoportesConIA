namespace SoportesDescarga.Shared;

public static class RetryHelper
{
    public static async Task<T?> ExecuteWithRetryAsync<T>(
        Func<CancellationToken, Task<T?>> operation,
        CancellationToken ct = default,
        int maxRetries = 1,
        int delayMs = 1000)
    {
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                var result = await operation(ct);
                if (result is not null) return result;
            }
            catch (HttpRequestException) when (attempt < maxRetries)
            {
                await Task.Delay(delayMs, ct);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested && attempt < maxRetries)
            {
                await Task.Delay(delayMs, ct);
            }
        }
        return default;
    }
}
