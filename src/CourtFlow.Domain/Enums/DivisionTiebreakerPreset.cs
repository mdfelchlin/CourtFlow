namespace CourtFlow.Domain.Enums;

/// <summary>Per-division standings tiebreaker chain (see data-model §10).</summary>
public enum DivisionTiebreakerPreset
{
    WinsH2HSetsPoints = 0,
    WinsSetsPoints = 1,
    SetsPoints = 2,
    H2HSetsPoints = 3
}
