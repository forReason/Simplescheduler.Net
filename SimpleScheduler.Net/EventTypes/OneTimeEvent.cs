namespace SimpleScheduler.Net.EventTypes;

public class OneTimeEvent : EventBase
{
    public OneTimeEvent(DateTime startTime, Action action, string? title = null)
    {
        StartTime = startTime;
        TaskToExecute = action;
        Title = title;
    }
    /// <summary>
    /// checks if the task should be executed. <br/>
    /// Executes the task if applicable. <br/>
    /// </summary>
    /// <returns>Returns true, if the task is complete and can be removed</returns>
    public override async Task<bool> ExecuteEvaluate()
    {
        DateTime now = DateTime.Now;
        if (now < StartTime) return false;

        lock (_executionLock)
        {
            if (Executed.HasValue) return true;
            Task.Run(async () =>
            {
                TaskToExecute?.Invoke();
                Executed = now;
            });
        }

        return true;
    }
}