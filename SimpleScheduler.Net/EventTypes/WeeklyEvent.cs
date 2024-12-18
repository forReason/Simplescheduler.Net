namespace SimpleScheduler.Net.EventTypes;

public class WeeklyEvent : EventBase
{
    public WeeklyEvent(DateTime startTime, Action action, DayOfWeek[] days,string? title = null ,DateTime? endTime = null)
    {
        if (!days.Any()) throw new ArgumentException("Days cannot be empty!");
        StartTime = startTime;
        Interval = new HashSet<DayOfWeek>(days);
        TaskToExecute = action;
        EndTime = endTime;
        Title = title;
    }
    /// <summary>
    /// the task will be executed on these days, at time of startTime
    /// </summary>
    public HashSet<DayOfWeek> Interval { get; set; }

    /// <summary>
    /// checks if the task should be executed. <br/>
    /// Executes the task if applicable. <br/>
    /// </summary>
    /// <returns>Returns true, if the task is complete and can be removed</returns>
    /// <summary>
    /// Checks if the task should be executed.
    /// Executes the task if applicable.
    /// </summary>
    /// <returns>Returns true if the task is complete and can be removed.</returns>
    public override async Task<bool> ExecuteEvaluate()
    {
        DateTime now = DateTime.Now;
        if (EndTime.HasValue && now > EndTime) return true;
        if (now < StartTime) return false;

        lock (_executionLock)
        {
            if (Executed.HasValue && Executed.Value >= StartTime) return false;

            // Execute the task
            Task.Run(() => TaskToExecute?.Invoke());
            Executed = now;

            // Adjust StartTime to the next execution time
            AdjustToNextExecution();
        }

        return false;
    }

    private void AdjustToNextExecution()
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