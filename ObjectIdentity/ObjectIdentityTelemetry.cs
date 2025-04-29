namespace ObjectIdentity;

/// <summary>
/// Provides telemetry services for the ObjectIdentity library
/// </summary>
public interface IObjectIdentityTelemetry
{
    /// <summary>
    /// Track an operation start for performance monitoring
    /// </summary>
    IDisposable StartOperation(string operationName, string context);
    
    /// <summary>
    /// Track a metric value such as block size, queue length, etc.
    /// </summary>
    void TrackMetric(string metricName, double value, string context = null);
    
    /// <summary>
    /// Track an exception that occurred during ID generation
    /// </summary>
    void TrackException(Exception exception, string context = null);
}

/// <summary>
/// Default implementation of telemetry that logs to the provided ILogger
/// </summary>
public class DefaultObjectIdentityTelemetry : IObjectIdentityTelemetry
{
    private readonly ILogger<DefaultObjectIdentityTelemetry> _logger;
    
    public DefaultObjectIdentityTelemetry(ILogger<DefaultObjectIdentityTelemetry> logger)
    {
        _logger = logger;
    }
    
    public IDisposable StartOperation(string operationName, string context)
    {
        return new OperationScope(_logger, operationName, context);
    }
    
    public void TrackMetric(string metricName, double value, string context = null)
    {
        _logger.LogDebug("{MetricName}: {Value} {Context}", metricName, value, context ?? string.Empty);
    }
    
    public void TrackException(Exception exception, string context = null)
    {
        _logger.LogError(exception, "Exception in ObjectIdentity {Context}", context ?? string.Empty);
    }
    
    private class OperationScope : IDisposable
    {
        private readonly ILogger _logger;
        private readonly string _operationName;
        private readonly string _context;
        private readonly Stopwatch _stopwatch;
        
        public OperationScope(ILogger logger, string operationName, string context)
        {
            _logger = logger;
            _operationName = operationName;
            _context = context;
            _stopwatch = Stopwatch.StartNew();
            
            _logger.LogDebug("Start: {OperationName} {Context}", _operationName, _context);
        }
        
        public void Dispose()
        {
            _stopwatch.Stop();
            _logger.LogDebug("End: {OperationName} {Context} - Duration: {DurationMs}ms", 
                _operationName, _context, _stopwatch.ElapsedMilliseconds);
        }
    }
}