using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace Cashflow.SharedKernel.Resilience;

/// <summary>
/// Reusable Polly v8 resilience pipelines for outbound HTTP calls.
/// Composition order (outer → inner): Retry → CircuitBreaker → Timeout per attempt.
/// </summary>
public static class ResiliencePipelines
{
    public sealed record HttpPipelineOptions
    {
        public int MaxRetryAttempts { get; init; } = 3;
        public TimeSpan BaseDelay { get; init; } = TimeSpan.FromMilliseconds(200);
        public TimeSpan AttemptTimeout { get; init; } = TimeSpan.FromSeconds(10);
        public double CircuitFailureRatio { get; init; } = 0.5;
        public int CircuitMinimumThroughput { get; init; } = 10;
        public TimeSpan CircuitSamplingDuration { get; init; } = TimeSpan.FromSeconds(30);
        public TimeSpan CircuitBreakDuration { get; init; } = TimeSpan.FromSeconds(15);
    }

    /// <summary>
    /// Builds a generic-typed pipeline suitable for arbitrary HTTP-ish operations.
    /// Transient = TimeoutRejectedException, BrokenCircuitException, or any other
    /// Exception that is not an OperationCanceledException.
    /// </summary>
    public static ResiliencePipeline BuildHttpPipeline(HttpPipelineOptions? options = null)
    {
        var opt = options ?? new HttpPipelineOptions();

        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<TimeoutRejectedException>()
                    .Handle<BrokenCircuitException>()
                    .Handle<Exception>(static ex => ex is not OperationCanceledException),
                MaxRetryAttempts = opt.MaxRetryAttempts,
                Delay = opt.BaseDelay,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = opt.CircuitFailureRatio,
                MinimumThroughput = opt.CircuitMinimumThroughput,
                SamplingDuration = opt.CircuitSamplingDuration,
                BreakDuration = opt.CircuitBreakDuration
            })
            .AddTimeout(opt.AttemptTimeout)
            .Build();
    }
}
