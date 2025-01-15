using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using OllamaClientLibrary.GeneralAi.PromptChain;
using SimpleScheduler.Net.EventTypes;
using SimpleScheduler.Net.util.json;

namespace SimpleScheduler.Net;

/// <summary>
/// the scheduler allows to set up simple events and cron jobs associated with a Task
/// </summary>
public class Scheduler
{
    [JsonIgnore] public string? SavePath { get; set; }

    public bool AutoSave { get; set; } = true;

    /// <summary>
    /// holds all one time events. Managed automatically
    /// </summary>
    /// <remarks>please use AddEventAsync to add events to the scheduler to avoid race conditions</remarks>
    public SortedList<DateTime, OneTimeEvent> OneTimeEvents { get; set; } = new SortedList<DateTime, OneTimeEvent>();

    private SemaphoreSlim _OneTimeSemaphore = new SemaphoreSlim(1);

    /// <summary>
    /// holds all simple repeating events. Managed automatically
    /// </summary>
    /// <remarks>please use AddEventAsync to add events to the scheduler to avoid race conditions</remarks>
    public SortedList<DateTime, RepeatingEvent> CronJobs { get; set; } = new SortedList<DateTime, RepeatingEvent>();

    private SemaphoreSlim _CronSemaphore = new SemaphoreSlim(1);

    /// <summary>
    /// holds all weekly scheduled events. Managed automatically
    /// </summary>
    /// <remarks>please use AddEventAsync to add events to the scheduler to avoid race conditions</remarks>
    public SortedList<DateTime, WeeklyEvent> WeeklySchedule { get; set; } = new SortedList<DateTime, WeeklyEvent>();

    private SemaphoreSlim _WeeklySemaphore = new SemaphoreSlim(1);

    private Func<string, Task> Processor { get; set; }

    /// <summary>
    /// runs the scheduler infinitely, processing all tasks
    /// </summary>
    /// <param name="processor">function which takes a string parameter (presumably json) to process</param>
    public async Task RunAsync(Func<object, Task> processor)
    {
        List<Task<bool>> tasks = new List<Task<bool>>();

        while (true)
        {
            //Debug.WriteLine($"checking schedule for: {SavePath}");
            try
            {
                tasks.Clear();
                tasks.Add(ProcessEvents(OneTimeEvents, _OneTimeSemaphore, processor));
                tasks.Add(ProcessEvents(CronJobs, _CronSemaphore, processor, isRepeating: true));
                tasks.Add(ProcessEvents(WeeklySchedule, _WeeklySemaphore, processor, isRepeating: true));
                await Task.WhenAll(tasks);
                bool changes = false;
                foreach (Task<bool> process in tasks)
                {
                    if (process.Result)
                    {
                        changes = true;
                        break;
                    }
                }
                if (AutoSave && changes &&  !string.IsNullOrEmpty(SavePath))
                { 
                    Save();
                    Debug.WriteLine("saved scheduler");
                }
                
                try
                {
                    await Task.Delay(5000).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    Debugger.Break();
                }
                {}
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                await Task.Delay(1000);
            }
        }
        Debugger.Break();
    }


    /// <summary>
    /// adds an event to the OneTimeEvents list in thread safe manner
    /// </summary>
    /// <param name="newEvent">a one time event</param>
    public async Task AddEventAsync(OneTimeEvent newEvent)
    {
        await _OneTimeSemaphore.WaitAsync();
        try
        {
            OneTimeEvents.Add(newEvent.StartTime, newEvent);
            if (AutoSave && !string.IsNullOrEmpty(SavePath)) Save();
        }
        finally
        {
            _OneTimeSemaphore.Release();
        }
    }

    /// <summary>
    /// adds a CronJob to the repeatingEvents list in thread safe manner
    /// </summary>
    /// <param name="newEvent">a cron event</param>
    public async Task AddEventAsync(RepeatingEvent newEvent)
    {
        await _CronSemaphore.WaitAsync();
        try
        {
            CronJobs.Add(newEvent.StartTime, newEvent);
            if (AutoSave && !string.IsNullOrEmpty(SavePath)) Save();
        }
        finally
        {
            _CronSemaphore.Release();
        }
    }

    /// <summary>
    /// adds a weekly event to the weeklyEvents list in thread safe manner
    /// </summary>
    /// <param name="newEvent">a weekly event</param>
    public async Task AddEventAsync(WeeklyEvent newEvent)
    {
        await _WeeklySemaphore.WaitAsync();
        try
        {
            WeeklySchedule.Add(newEvent.StartTime, newEvent);
            if (AutoSave && !string.IsNullOrEmpty(SavePath)) Save();
        }
        finally
        {
            _WeeklySemaphore.Release();
        }
    }

    private async Task<bool> ProcessEvents<T>(
        SortedList<DateTime, T> events,
        SemaphoreSlim semaphore,
        Func<object, Task> processor,
        bool isRepeating = false
    ) where T : EventBase
    {
        await semaphore.WaitAsync();
        bool schedulerChanged = false;
        try
        {
            if (events.Any())
            {
                var firstEvent = events.Values.First();
                (bool remove, bool execute) taskStartInfo = firstEvent.EvaluateSchedule();
                
                try
                {
                    if (taskStartInfo.execute)
                    {
                        schedulerChanged = true;
                        // Process the event dynamically
                        if (firstEvent.TaskChain is not null)
                        {
                            // Execute using TaskChain
                            await processor(firstEvent.TaskChain);
                        }
                        else if (!string.IsNullOrEmpty(firstEvent.TaskData))
                        {
                            // Execute using TaskData
                            await processor(firstEvent.TaskData);
                        }
                        else
                        {
                            Debug.WriteLine($"Event does not have valid TaskData or TaskChain: {firstEvent.GetType()}");
                            throw new InvalidOperationException($"Event does not have valid TaskData or TaskChain: {firstEvent.GetType()}");
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"Failed to execute event: {e}");
                }

                // Remove completed tasks
                if (taskStartInfo.remove)
                {
                    schedulerChanged = true;
                    events.RemoveAt(0);
                    if (isRepeating)
                    {
                        firstEvent.AdjustToNextExecutionTime();
                        events.Add(firstEvent.StartTime, firstEvent);
                    }
                }
            }
        }
        finally
        {
            semaphore.Release();
        }
        return schedulerChanged;
    }

    /// <summary>
    /// returns a list of all events in the scheduler between now and timespan
    /// </summary>
    /// <param name="searchWindow">the time from now, to search in the future</param>
    /// <param name="includeSingleEvents">include one time events</param>
    /// <param name="includeWeeklyEvents">include weekly events</param>
    /// <param name="includeCronJobs">include cronJobs</param>
    /// <returns>a list of upcoming events, </returns>
    public async Task<List<EventBase>> ListUpcomingEventsAsync(
        TimeSpan searchWindow,
        bool includeSingleEvents = true,
        bool includeWeeklyEvents = true,
        bool includeCronJobs = false)
    {
        var events = new List<EventBase>();
        DateTime endTime = DateTime.Now + searchWindow;

        await _OneTimeSemaphore.WaitAsync();
        try
        {
            if (includeSingleEvents)
                events.AddRange(OneTimeEvents.Values.Where(e => e.StartTime <= endTime));
        }
        finally
        {
            _OneTimeSemaphore.Release();
        }

        await _WeeklySemaphore.WaitAsync();
        try
        {
            if (includeWeeklyEvents)
                events.AddRange(WeeklySchedule.Values.Where(e => e.StartTime <= endTime));
        }
        finally
        {
            _WeeklySemaphore.Release();
        }

        await _CronSemaphore.WaitAsync();
        try
        {
            if (includeCronJobs)
                events.AddRange(CronJobs.Values.Where(e => e.StartTime <= endTime));
        }
        finally
        {
            _CronSemaphore.Release();
        }

        return events.OrderBy(e => e.StartTime).ToList();
    }

    /// <summary>
    /// Saves the current state of the scheduler to a file using atomic saving.
    /// </summary>
    /// <param name="savePath">The file path where the scheduler data should be saved.</param>
    public void Save(string? savePath = null)
    {
        Debug.WriteLine("Starting SaveAsync");
        Debug.WriteLine($"SavePath: {SavePath}");
        

        if (!string.IsNullOrEmpty(savePath))
            SavePath = savePath;

        if (string.IsNullOrEmpty(SavePath))
            throw new ArgumentException("You need to define the savePath first!");

        if (!SavePath.EndsWith(".schedule"))
            SavePath += ".schedule";

        string directoryPath = Path.GetDirectoryName(SavePath)
                               ?? throw new InvalidOperationException("Failed to determine directory path.");

        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
        

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        string tempFilePath = SavePath + ".tmp";
        Debug.WriteLine($"TempFilePath: {tempFilePath}");
        
        try
        {
            if (File.Exists(tempFilePath))
            {
                Debug.WriteLine($"deleting temp file: {tempFilePath}");
                File.Delete(tempFilePath);
            }
                
            Debug.WriteLine($"writing new temp file: {tempFilePath}");
            string json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(tempFilePath, json);
            Debug.WriteLine($"overwriting old file: {tempFilePath} -> {SavePath}");
            File.Move(tempFilePath, SavePath, true);
            Debug.WriteLine($"scheduler saving complete");
        }
        catch (Exception ex)
        {
            // Log and handle specific exceptions as needed
            if (File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch (Exception fileException)
                {
                    Debug.WriteLine($"Failed to cleanup failure: {fileException.Message}");
                    Debug.WriteLine(fileException.StackTrace);
                }
            }

            Console.WriteLine($"An error occurred while saving the file: {tempFilePath}");
            Debug.WriteLine($"An error occurred while saving the file: {tempFilePath}");
            Console.WriteLine(ex.Message);
            Debug.WriteLine(ex.Message);
            Debug.WriteLine(ex.StackTrace);
        }
    }

    /// <summary>
    /// Loads the state of the scheduler from a file.
    /// </summary>
    /// <param name="filePath">The file path from where the scheduler data should be loaded.</param>
    public async Task LoadAsync(string? filePath = null, bool throwOnNotExist = true)
    {
        if (!string.IsNullOrEmpty(filePath))
            SavePath = filePath;
        if (string.IsNullOrEmpty(SavePath))
            throw new ArgumentException("You need to define the SavePath first!");
        if (!SavePath.EndsWith(".schedule"))
            SavePath += ".schedule";
        if (throwOnNotExist && !File.Exists(SavePath))
            throw new FileNotFoundException("Scheduler state file not found.", SavePath);
        if (!File.Exists(SavePath))
        {
            return;
        }

        var options = new JsonSerializerOptions
        {
            Converters = { new JsonStringEnumConverter() }
        };

        using (var fileStream = new FileStream(SavePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (fileStream is null || fileStream.Length == 0) return;

            try
            {
                var state = await JsonSerializer.DeserializeAsync<JsonElement>(fileStream, options);
                if (state.ValueKind == JsonValueKind.Object)
                {
                    this.OneTimeEvents = state.TryGetProperty("OneTimeEvents", out var oneTimeEvents) &&
                                         oneTimeEvents.ValueKind != JsonValueKind.Null
                        ? JsonSerializer.Deserialize<SortedList<DateTime, OneTimeEvent>>(oneTimeEvents.GetRawText(),
                            options)
                        : new SortedList<DateTime, OneTimeEvent>();

                    this.CronJobs = state.TryGetProperty("CronJobs", out var cronJobs) &&
                                    cronJobs.ValueKind != JsonValueKind.Null
                        ? JsonSerializer.Deserialize<SortedList<DateTime, RepeatingEvent>>(cronJobs.GetRawText(),
                            options)
                        : new SortedList<DateTime, RepeatingEvent>();

                    this.WeeklySchedule = state.TryGetProperty("WeeklySchedule", out var weeklySchedule) &&
                                          weeklySchedule.ValueKind != JsonValueKind.Null
                        ? JsonSerializer.Deserialize<SortedList<DateTime, WeeklyEvent>>(weeklySchedule.GetRawText(),
                            options)
                        : new SortedList<DateTime, WeeklyEvent>();
                }
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("Failed to deserialize scheduler state.", ex);
            }
        }
    }

}