public static class StructuredLogging
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static ILogger<T> ForContext<T>(this ILogger<T> logger, string propertyName, object value)
    {
        return new StructuredLogger<T>(logger, new Dictionary<string, object>
        {
            { propertyName, value }
        });
    }

    public static ILogger<T> ForContext<T>(this ILogger<T> logger, Dictionary<string, object> properties)
    {
        return new StructuredLogger<T>(logger, properties);
    }

    private class StructuredLogger<T> : ILogger<T>
    {
        private readonly ILogger<T> _logger;
        private readonly Dictionary<string, object> _properties;

        public StructuredLogger(ILogger<T> logger, Dictionary<string, object> properties)
        {
            _logger = logger;
            _properties = properties;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter)
        {
            var enrichedState = new EnrichedLogState<TState>(state, _properties);
            _logger.Log(logLevel, eventId, enrichedState, exception, EnrichedFormatter);
        }

        public bool IsEnabled(LogLevel logLevel) => _logger.IsEnabled(logLevel);

        public IDisposable BeginScope<TState>(TState state) => _logger.BeginScope(state);

        private string EnrichedFormatter<TState>(TState state, Exception exception)
        {
            if (state is EnrichedLogState<TState> enrichedState)
            {
                var logObject = new Dictionary<string, object>
                {
                    { "message", state.ToString() },
                    { "properties", enrichedState.Properties }
                };

                if (exception != null)
                {
                    logObject["exception"] = new
                    {
                        type = exception.GetType().Name,
                        message = exception.Message,
                        stackTrace = exception.StackTrace
                    };
                }

                return JsonSerializer.Serialize(logObject, JsonOptions);
            }

            return state.ToString();
        }
    }

    private class EnrichedLogState<TState>
    {
        public TState State { get; }
        public Dictionary<string, object> Properties { get; }

        public EnrichedLogState(TState state, Dictionary<string, object> properties)
        {
            State = state;
            Properties = properties;
        }

        public override string ToString() => State.ToString();
    }
} 