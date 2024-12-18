namespace SimpleScheduler.Net.EventTypes;

public class OneTimeEvent : EventBase
{
    public OneTimeEvent(DateTime startTime, string taskData, string? title = null)
    {
        StartTime = startTime;
        TaskData = taskData;
        Title = title;
    }
    /// <summary>
    /// checks if the task should be executed. <br/>
    /// Executes the task if applicable. <br/>
    /// </summary>
    /// <returns>Returns true, if the task is complete and can be removed</returns>
    public override async Task<bool> ExecuteEvaluate(Action<string> action)
    {
        DateTime now = DateTime.Now;

        // Ensure the task is not executed before the start time
        if (now < StartTime) return false;

        lock (_executionLock)
        {
            if (!Executed.HasValue)
            {
                Executed = now; // Mark as executed inside the lock
            }
            else
            {
                return true; // Avoid race conditions for double execution
            }
        }

        // Execute the action asynchronously outside the lock
        await Task.Run(() =>
        {
            action?.Invoke(TaskData);
        });

        return true;
    }

}