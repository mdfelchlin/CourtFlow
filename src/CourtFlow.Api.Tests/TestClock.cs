namespace CourtFlow.Api.Tests;

/// <summary>
/// The API's clock under test. Tests move it to reach the 270-day mark instead of waiting, and the shared
/// fixture rewinds it between tests, which <c>FakeTimeProvider</c> refuses to do.
/// </summary>
public sealed class TestClock : TimeProvider
{
    public static readonly DateTimeOffset Start = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private DateTimeOffset _utcNow = Start;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void SetUtcNow(DateTimeOffset value) => _utcNow = value;

    public void Advance(TimeSpan amount) => _utcNow += amount;

    public void Reset() => _utcNow = Start;
}