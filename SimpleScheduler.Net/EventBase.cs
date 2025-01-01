using OllamaClientLibrary.GeneralAi.PromptChain;

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
    /// blocks the task from executing after this time
    /// </summary>
    public TimeSpan MaxStartDelay { get; set; } = TimeSpan.FromHours(7);

    /// <summary>
    /// the action to invoke
    /// </summary>
    public string? TaskData { get; set; } = null;

    /// <summary>
    /// the actionchain to invoke (For LLMs)
    /// </summary>
    public PromptChain? TaskChain { get; set; } = null;
    /// <summary>
    /// specifies the last execution time of the task
    /// </summary>
    /// <remarks>this is used to determine if the task run is still outstanding or if it should be run</remarks>
    public DateTime? Executed { get; set; } = null;

    /// <summary>
    /// a title for the task to better determine it
    /// </summary>
    public string? Title { get; set; } = null;
    internal readonly object _executionLock = new();
    /// <summary>
    /// Evaluates if the task is ready for execution or should be rescheduled.
    /// </summary>
    /// <returns>(bool remove, bool execute)</returns>
    public abstract (bool remove, bool execute) EvaluateSchedule();
    public abstract void AdjustToNextExecutionTime();
    public override string ToString()
    {
        return $"{StartTime.ToString("yy-MMM-dd yyyy HH:mm:ss")} - {Title ?? "Unnamed"}";
    }
}