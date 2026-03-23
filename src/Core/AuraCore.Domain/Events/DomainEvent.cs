namespace AuraCore.Domain.Events;

public abstract record DomainEvent(DateTimeOffset OccurredAt);

public sealed record OptimizationCompletedEvent(
    string ModuleId,
    int ItemsProcessed,
    TimeSpan Duration,
    DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);

public sealed record RestorePointCreatedEvent(
    string RestorePointId,
    string Description,
    DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);
