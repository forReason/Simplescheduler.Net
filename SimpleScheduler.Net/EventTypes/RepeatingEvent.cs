namespace SimpleScheduler.Net.EventTypes;

public class RepeatingEvent : EventBase
{
    public RepeatingEvent(DateTime startTime, Action action, TimeSpan interval,string? title = null ,DateTime? endTime = null)
    {
        StartTime = startTime;
        Interval = interval;
        TaskToExecute = action;
        EndTime = endTime;
        Title = title;
    }
    /// <summary>
    /// after this time, the task will be executed
    /// </summary>
    public DateTime StartTime { get; set; }
    /// <summary>
    /// after this time, the task can be removed
    /// </summary>
    public DateTime? EndTime { get; set; }

    public TimeSpan Interval { get; set; }

    /// <summary>
    /// checks if the task should be executed. <br/>
    /// Executes the task if applicable. <br/>
    /// </summary>
    /// <returns>Returns true, if the task is complete and can be removed</returns>
    public override async Task<bool> ExecuteEvaluate()
    {
        DateTime now = DateTime.Now;
        if (EndTime.HasValue && now > EndTime) return true;
        if (now < StartTime) return false;

        lock (_executionLock)
        {
            Task.Run(async () =>
            {
                TaskToExecute?.Invoke();
                Executed = now;
            });
            StartTime += Interval;
        }

        return false;
    }

    public override string ToString()
    {
        return EndTime.HasValue
            ? $"{StartTime.ToString("yy-MMM-dd yyyy HH:mm:ss")} - {Title ?? "Unnamed"} - {EndTime.Value.ToString("yy-MMM-dd HH:mm:ss")}"
            : $"{StartTime.ToString("yy-MMM-dd HH:mm:ss")} - {Title ?? "Unnamed"}";
    }
}