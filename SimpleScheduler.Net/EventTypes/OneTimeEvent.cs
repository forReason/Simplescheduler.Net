using OllamaClientLibrary.GeneralAi.PromptChain;

namespace SimpleScheduler.Net.EventTypes;

public class OneTimeEvent : EventBase
{
    /// <summary>
    /// for use with custom task interpreter
    /// </summary>
    /// <param name="startTime">the tiem when the event happens</param>
    /// <param name="taskData">custom task data, recommended in json format.</param>
    /// <param name="title">a title for the event</param>
    public OneTimeEvent(DateTime startTime, string taskData, string? title = null)
    {
        StartTime = startTime;
        TaskData = taskData;
        Title = title;
    }
    /// <summary>
    /// for use with llms
    /// </summary>
    /// <param name="startTime">the time when the event happens</param>
    /// <param name="taskChain">a promptchain for the llm to execute</param>
    /// <param name="title">a title for the event</param>
    public OneTimeEvent(DateTime startTime, PromptChain taskChain, string? title = null)
    {
        StartTime = startTime;
        TaskChain = taskChain;
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
    /// <summary>
    /// checks if the task should be executed. <br/>
    /// Executes the task if applicable. <br/>
    /// </summary>
    /// <returns>Returns true, if the task is complete and can be removed</returns>
    public override async Task<bool> ExecuteEvaluatePromptChain(Action<PromptChain> action)
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
            action?.Invoke(TaskChain);
        });

        return true;
    }

}