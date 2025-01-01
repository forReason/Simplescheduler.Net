using OllamaClientLibrary.GeneralAi.PromptChain;

namespace SimpleScheduler.Net.EventTypes;

public class RepeatingEvent : EventBase
{
    /// <summary>
    /// for json serialization only
    /// </summary>
    public RepeatingEvent()
    {
        
    }
    public RepeatingEvent(DateTime startTime, string taskData, TimeSpan interval,string? title = null ,DateTime? endTime = null)
    {
        StartTime = startTime;
        Interval = interval;
        TaskData = taskData;
        EndTime = endTime;
        Title = title;
    }
    public RepeatingEvent(DateTime startTime, PromptChain taskChain, TimeSpan interval,string? title = null ,DateTime? endTime = null)
    {
        StartTime = startTime;
        Interval = interval;
        TaskChain = taskChain;
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
    /// Evaluates if the task is ready for execution or should be rescheduled.
    /// </summary>
    /// <returns>(bool remove, bool execute)</returns>
    public override (bool remove, bool execute) EvaluateSchedule()
    {
        DateTime now = DateTime.Now;
        if (EndTime.HasValue && now > EndTime) return (true, false);
        if (now < StartTime) return (false, false);
        return (true, true);
    }


    public override void AdjustToNextExecutionTime()
    {
        DateTime now = DateTime.Now;
        DateTime nextStart = StartTime;
        if (nextStart < now) nextStart = now;

        // Find the next valid day of the week
        while (nextStart <= now)
        {
            nextStart += Interval;
        }

        StartTime = new DateTime(nextStart.Year, nextStart.Month, nextStart.Day, StartTime.Hour, StartTime.Minute, StartTime.Second);
    }

    public override string ToString()
    {
        return EndTime.HasValue
            ? $"{StartTime.ToString("yy-MMM-dd yyyy HH:mm:ss")} - {Title ?? "Unnamed"} - {EndTime.Value.ToString("yy-MMM-dd HH:mm:ss")}"
            : $"{StartTime.ToString("yy-MMM-dd HH:mm:ss")} - {Title ?? "Unnamed"}";
    }
}