using System.Diagnostics.Metrics;

namespace ESGService.Domain;

public sealed class EsgMetrics
{
    public const string MeterName = "GreenFinance.ESGService";

    private readonly Counter<long> _calculatedCounter;
    private readonly Counter<long> _unavailableCounter;
    private int _circuitBreakerState;

    public EsgMetrics(Meter meter)
    {
        _calculatedCounter = meter.CreateCounter<long>("esg.results.calculated", description: "Number of ESG results successfully calculated");
        _unavailableCounter = meter.CreateCounter<long>("esg.results.unavailable", description: "Number of ESG results marked temporarily unavailable");
        meter.CreateObservableGauge(
            "esg.referencedata.circuit_state",
            () => Volatile.Read(ref _circuitBreakerState),
            description: "ReferenceDataService circuit breaker state: 0=closed, 1=open, 2=half-open");
    }

    public void RecordCalculated() => _calculatedCounter.Add(1);

    public void RecordUnavailable() => _unavailableCounter.Add(1);

    public void SetCircuitClosed() => Volatile.Write(ref _circuitBreakerState, 0);

    public void SetCircuitOpen() => Volatile.Write(ref _circuitBreakerState, 1);

    public void SetCircuitHalfOpen() => Volatile.Write(ref _circuitBreakerState, 2);
}
