namespace CourtFlow.Application.Scheduling;

public sealed class StaleScheduleApplyException : InvalidOperationException
{
    public StaleScheduleApplyException() : base("Schedule preview is stale — another change occurred. Preview again.") { }
}

public sealed class ScheduleApplyTokenInvalidException : InvalidOperationException
{
    public ScheduleApplyTokenInvalidException() : base("Schedule apply token is invalid or malformed.") { }
}

public sealed class ScheduleConflictAfterPreviewException : InvalidOperationException
{
    public ScheduleConflictAfterPreviewException()
        : base("The tournament schedule changed — conflicts appeared since preview.") { }
}
