using OllamaClientLibrary.GeneralAi.PromptChain;

namespace SimpleScheduler.Net.EventTypes;

public class WeeklyEvent : EventBase
{
    /// <summary>
    /// for json serialization only
    /// </summary>
    public WeeklyEvent()
    {
        /* for json serialization*/
    }
    public WeeklyEvent(DateTime startTime, string taskData, HashSet<DayOfWeek> interval,string? title = null ,DateTime? endTime = null, DateTime? executed = null)
    {
        if (!interval.Any()) throw new ArgumentException("Days cannot be empty!");
        StartTime = startTime;
        Interval = interval;
        TaskData = taskData;
        EndTime = endTime;
        Title = title;
        Executed = executed;
    }
    public WeeklyEvent(DateTime startTime, PromptChain taskChain, HashSet<DayOfWeek> interval,string? title = null ,DateTime? endTime = null, DateTime? executed = null)
    {
        if (!interval.Any()) throw new ArgumentException("Days cannot be empty!");
        StartTime = startTime;
        Interval = interval;
        TaskChain = taskChain;
        EndTime = endTime;
        Title = title;
        Executed = executed;
    }

    /// <summary>
    /// the task will be executed on these days, at time of startTime
    /// </summary>
    public HashSet<DayOfWeek> Interval { get; set; }

    /// <summary>
    /// Evaluates if the task is ready for execution or should be rescheduled.
    /// </summary>
    /// <returns>(bool remove, bool execute)</returns>
    public override (bool remove, bool execute) EvaluateSchedule()
    {
        DateTime now = DateTime.Now;

        // Check if the event has expired
        if (EndTime.HasValue && now > EndTime) return (true, false);

        // Check if the event is not yet ready to execute
        if (now < StartTime) return (false, false);

        if (!Interval.Contains(now.DayOfWeek)) return (true, false);
        // Check if the event missed its maximum start delay
        if (now > StartTime + MaxStartDelay) return (true, false);

        // Lock to ensure thread safety
        lock (_executionLock)
        {
            // Check if the task has already been executed
            if (Executed.HasValue && Executed.Value >= StartTime) 
                return (true, false);

            // Mark the task as executed
            Executed = now;
        }

        // start task and reschedule!
        return (true, true);
    }


    public override void AdjustToNextExecutionTime()
    {
        DateTime now = DateTime.Now;
        DateTime nextStart = StartTime;
        if (nextStart < now) nextStart = now;

        // Find the next valid day of the week
        while (!Interval.Contains(nextStart.DayOfWeek) || nextStart <= now)
        {
            nextStart = nextStart.AddDays(1);
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