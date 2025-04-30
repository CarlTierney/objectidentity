using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ObjectIdentity;

/// <summary>
/// Defines the telemetry interface for the ObjectIdentity library.
/// </summary>
/// <remarks>
/// <para>
/// This interface provides methods for tracking operations, metrics, and exceptions
/// throughout the ID generation process. Implementing this interface allows for integration
/// with various telemetry and monitoring systems.
/// </para>
/// <para>
/// Telemetry can be used to monitor performance, track resource usage, and troubleshoot issues
/// in the ObjectIdentity system, especially in high-volume production environments.
/// </para>
/// </remarks>
public interface IObjectIdentityTelemetry
{
    /// <summary>
    /// Tracks the start of an operation for performance monitoring.
    /// </summary>
    /// <param name="operationName">The name of the operation being performed (e.g., "GetNextIdentity", "FetchBlock").</param>
    /// <param name="context">Additional context information, typically the scope name.</param>
    /// <returns>An <see cref="IDisposable"/> that, when disposed, marks the end of the operation and records its duration.</returns>
    /// <remarks>
    /// This method follows the disposable pattern for timing operations:
    /// <code>
    /// using (telemetry.StartOperation("MyOperation", "Customer"))
    /// {
    ///     // Do work here
    /// } // Operation ends and duration is automatically recorded when the scope is disposed
    /// </code>
    /// </remarks>
    IDisposable StartOperation(string operationName, string context);
    
    /// <summary>
    /// Tracks a metric value such as block size, queue length, or other numerical data.
    /// </summary>
    /// <param name="metricName">The name of the metric being recorded.</param>
    /// <param name="value">The value of the metric.</param>
    /// <param name="context">Optional additional context information, typically the scope name.</param>
    /// <remarks>
    /// Metrics can be used to monitor the health and performance of the system, such as:
    /// <list type="bullet">
    ///   <item><description>Number of IDs in the queue</description></item>
    ///   <item><description>Block sizes</description></item>
    ///   <item><description>Time to fetch a new block</description></item>
    ///   <item><description>ID consumption rate</description></item>
    /// </list>
    /// </remarks>
    void TrackMetric(string metricName, double value, string context = null);
    
    /// <summary>
    /// Tracks an exception that occurred during ID generation or processing.
    /// </summary>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="context">Optional additional context information, typically the operation or scope name.</param>
    /// <remarks>
    /// Exception tracking is crucial for identifying and troubleshooting issues in the ID generation system,
    /// particularly transient errors related to database connectivity or resource contention.
    /// </remarks>
    void TrackException(Exception exception, string context = null);
}

/// <summary>
/// Default implementation of <see cref="IObjectIdentityTelemetry"/> that logs to the provided <see cref="ILogger"/>.
/// </summary>
/// <remarks>
/// <para>
/// This implementation uses Microsoft.Extensions.Logging to record telemetry information.
/// It provides basic telemetry capabilities without requiring any additional monitoring systems.
/// </para>
/// <para>
/// For production environments, consider implementing a custom telemetry provider that integrates
/// with more sophisticated monitoring systems like Application Insights, Prometheus, or other APM tools.
/// </para>
/// </remarks>
public class DefaultObjectIdentityTelemetry : IObjectIdentityTelemetry
{
    private readonly ILogger<DefaultObjectIdentityTelemetry> _logger;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultObjectIdentityTelemetry"/> class.
    /// </summary>
    /// <param name="logger">The logger instance to use for telemetry logging.</param>
    public DefaultObjectIdentityTelemetry(ILogger<DefaultObjectIdentityTelemetry> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <summary>
    /// Tracks the start of an operation and returns a disposable scope that, when disposed,
    /// records the operation's duration.
    /// </summary>
    /// <param name="operationName">The name of the operation being performed.</param>
    /// <param name="context">Additional context information, typically the scope name.</param>
    /// <returns>A disposable scope that records the operation duration when disposed.</returns>
    public IDisposable StartOperation(string operationName, string context)
    {
        return new OperationScope(_logger, operationName, context);
    }
    
    /// <summary>
    /// Tracks a metric value by logging it at Debug level.
    /// </summary>
    /// <param name="metricName">The name of the metric being recorded.</param>
    /// <param name="value">The value of the metric.</param>
    /// <param name="context">Optional additional context information.</param>
    public void TrackMetric(string metricName, double value, string context = null)
    {
        _logger.LogDebug("{MetricName}: {Value} {Context}", metricName, value, context ?? string.Empty);
    }
    
    /// <summary>
    /// Tracks an exception by logging it at Error level.
    /// </summary>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="context">Optional additional context information.</param>
    public void TrackException(Exception exception, string context = null)
    {
        _logger.LogError(exception, "Exception in ObjectIdentity {Context}", context ?? string.Empty);
    }
    
    /// <summary>
    /// Represents an operation scope that records the duration of an operation.
    /// </summary>
    private class OperationScope : IDisposable
    {
        private readonly ILogger _logger;
        private readonly string _operationName;
        private readonly string _context;
        private readonly Stopwatch _stopwatch;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="OperationScope"/> class.
        /// </summary>
        /// <param name="logger">The logger to use for recording operation events.</param>
        /// <param name="operationName">The name of the operation being timed.</param>
        /// <param name="context">Additional context information about the operation.</param>
        public OperationScope(ILogger logger, string operationName, string context)
        {
            _logger = logger;
            _operationName = operationName;
            _context = context;
            _stopwatch = Stopwatch.StartNew();
            
            _logger.LogDebug("Start: {OperationName} {Context}", _operationName, _context);
        }
        
        /// <summary>
        /// Stops timing the operation and logs its duration.
        /// </summary>
        public void Dispose()
        {
            _stopwatch.Stop();
            _logger.LogDebug("End: {OperationName} {Context} - Duration: {DurationMs}ms", 
                _operationName, _context, _stopwatch.ElapsedMilliseconds);
        }
    }
}