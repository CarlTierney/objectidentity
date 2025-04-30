// This file provides compatibility helpers for different target frameworks
namespace ObjectIdentity;

#if NETSTANDARD2_0
// Polyfills and compatibility methods for .NET Standard 2.0
internal static class CompatibilityExtensions
{
    // Add methods that mimic newer .NET APIs but work on .NET Standard 2.0
    
    // Example: Task.WaitAsync polyfill
    public static async Task<T> WaitAsync<T>(this Task<T> task, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>();
        using (cancellationToken.Register(() => tcs.TrySetResult(true)))
        {
            if (task.IsCompleted)
                return await task;
                
            var completedTask = await Task.WhenAny(task, tcs.Task);
            if (completedTask == tcs.Task)
                throw new OperationCanceledException(cancellationToken);
                
            return await task;
        }
    }
    
    // Example: String.Contains with StringComparison polyfill
    public static bool Contains(this string source, string value, StringComparison comparisonType)
    {
        return source.IndexOf(value, comparisonType) >= 0;
    }
}
#endif