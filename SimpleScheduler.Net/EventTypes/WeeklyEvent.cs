using OllamaClientLibrary.GeneralAi.PromptChain;

namespace SimpleScheduler.Net.EventTypes;

public class WeeklyEvent : EventBase
{
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
    /// checks if the task should be executed. <br/>
    /// Executes the task if applicable. <br/>
    /// </summary>
    /// <returns>Returns true, if the task is complete and can be removed</returns>
    /// <summary>
    /// Checks if the task should be executed.
    /// Executes the task if applicable.
    /// </summary>
    /// <returns>Returns true if the task is complete and can be removed.</returns>
    public override async Task<bool> ExecuteEvaluate(Action<string> action)
    {
        DateTime now = DateTime.Now;
        if (EndTime.HasValue && now > EndTime) return true;
        if (now < StartTime) return false;

        lock (_executionLock)
        {
            if (Executed.HasValue && Executed.Value >= StartTime) return false;

            // Execute the task
            Task.Run(() => action?.Invoke(TaskData));
            Executed = now;

            // Adjust StartTime to the next execution time
            AdjustToNextExecution();
        }

        return false;
    }
    /// <summary>
    /// executes a prompt chain injection
    /// </summary>
    /// <param name="action"></param>
    /// <returns></returns>
    public override async Task<bool> ExecuteEvaluatePromptChain(Action<PromptChain> action)
    {
        DateTime now = DateTime.Now;
        if (EndTime.HasValue && now > EndTime) return true;
        if (now < StartTime) return false;

        lock (_executionLock)
        {
            if (Executed.HasValue && Executed.Value >= StartTime) return false;

            // Execute the task
            Task.Run(() => action?.Invoke(TaskChain));
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