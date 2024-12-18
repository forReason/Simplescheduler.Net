namespace SimpleScheduler.Net;

public abstract class EventBase
{
    /// <summary>
    /// after this time, the task will be executed
    /// </summary>
    public DateTime StartTime { get; set; }
    /// <summary>
    /// after this time, the task can be removed
    /// </summary>
    public DateTime? EndTime { get; set; } = null;

    /// <summary>
    /// the action to invoke
    /// </summary>
    public string TaskData { get; set; }
    /// <summary>
    /// specifies the last execution time of the task
    /// </summary>
    /// <remarks>this is used to determine if the task run is still outstanding or if it should be run</remarks>
    public DateTime? Executed { get; set; } = null;

    /// <summary>
    /// a title for the task to better determine it
    /// </summary>
    public string? Title = null;
    internal readonly object _executionLock = new();
    public abstract Task<bool> ExecuteEvaluate(Action<string> action);
    public override string ToString()
    {
        return $"{StartTime.ToString("yy-MMM-dd yyyy HH:mm:ss")} - {Title ?? "Unnamed"}";
    }
}