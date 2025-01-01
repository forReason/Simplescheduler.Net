using OllamaClientLibrary.GeneralAi.PromptChain;

namespace SimpleScheduler.Net.EventTypes;

public class OneTimeEvent : EventBase
{
    /// <summary>
    /// for json serialization only
    /// </summary>
    public OneTimeEvent()
    {
        
    }
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
    /// Evaluates if the task is ready for execution or should be rescheduled.
    /// </summary>
    /// <returns>(bool remove, bool execute)</returns>
    public override (bool remove, bool execute) EvaluateSchedule()
    {
        DateTime now = DateTime.Now;

        // Ensure the task is not executed before the start time
        if (now < StartTime) return (false, false);

        lock (_executionLock)
        {
            if (!Executed.HasValue)
            {
                Executed = now; // Mark as executed inside the lock
            }
            else
            {
                return (true, false); // Avoid race conditions for double execution
            }
        }

        return (true, true);
    }


    public override void AdjustToNextExecutionTime()
    {
        throw new NotImplementedException("a one time event cannot be rescheduled.");
    }
}