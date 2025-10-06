using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;

namespace SkTrailCourse.Infra;

public static class KernelRetryExtensions
{
    public static async Task<object?> InvokePromptWithRetryAsync(this Kernel kernel, string prompt, CancellationToken cancellationToken = default, int maxAttempts = 3)
    {
        if (kernel == null) throw new ArgumentNullException(nameof(kernel));
        int delay = 1000;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var res = await kernel.InvokePromptAsync(prompt, cancellationToken: cancellationToken);
                return res;
            }
            catch (Exception ex) when (IsTransient(ex) && attempt < maxAttempts)
            {
                Console.WriteLine($"⚠️ InvokePromptAsync failed (attempt {attempt}): {ex.Message}. Retrying in {delay}ms...");
                try { await Task.Delay(delay, cancellationToken); } catch { }
                delay *= 2;
            }
        }

        // Final attempt - let exceptions bubble
        return await kernel.InvokePromptAsync(prompt, cancellationToken: cancellationToken);
    }

    public static async Task<object?> InvokeWithRetryAsync(this Kernel kernel, string plugin, string function, KernelArguments args, int maxAttempts = 3)
    {
        if (kernel == null) throw new ArgumentNullException(nameof(kernel));
        int delay = 1000;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var res = await kernel.InvokeAsync(plugin, function, args);
                return res;
            }
            catch (Exception ex) when (IsTransient(ex) && attempt < maxAttempts)
            {
                Console.WriteLine($"⚠️ InvokeAsync failed (attempt {attempt}): {ex.Message}. Retrying in {delay}ms...");
                await Task.Delay(delay);
                delay *= 2;
            }
        }

        // Final attempt - let exceptions bubble
        return await kernel.InvokeAsync(plugin, function, args);
    }

    private static bool IsTransient(Exception ex)
    {
        if (ex is HttpRequestException) return true;
        if (ex is TaskCanceledException) return true;
        var msg = ex.Message?.ToLowerInvariant() ?? string.Empty;
        if (msg.Contains("503") || msg.Contains("service unavailable") || msg.Contains("rate limit") || msg.Contains("too many requests"))
            return true;
        if (ex.InnerException != null) return IsTransient(ex.InnerException);
        return false;
    }
}
