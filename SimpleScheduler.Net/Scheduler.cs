using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using SimpleScheduler.Net.EventTypes;

namespace SimpleScheduler.Net;

/// <summary>
/// the scheduler allows to set up simple events and cron jobs associated with a Task
/// </summary>
public class Scheduler
{
    [JsonIgnore]
    public string? SavePath { get; set; }

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
    private SemaphoreSlim _CronSemaphore = new SemaphoreSlim(1);/// <summary>
    /// holds all weekly scheduled events. Managed automatically
    /// </summary>
    /// <remarks>please use AddEventAsync to add events to the scheduler to avoid race conditions</remarks>
    public SortedList<DateTime, WeeklyEvent> WeeklySchedule { get; set; } = new SortedList<DateTime, WeeklyEvent>();
    private SemaphoreSlim _WeeklySemaphore = new SemaphoreSlim(1);

    /// <summary>
    /// runs the scheduler infinitely, processing all tasks
    /// </summary>
    public async Task Run()
    {
        List<Task> tasks = new List<Task>();
        while (true)
        {
            tasks.Clear();
            tasks.Add(ProcessEvents(OneTimeEvents, _OneTimeSemaphore));
            tasks.Add(ProcessEvents(CronJobs,_CronSemaphore, isRepeating: true));
            tasks.Add(ProcessEvents(WeeklySchedule, _WeeklySemaphore ,isRepeating: true));
            await Task.WhenAll(tasks);
            if (AutoSave && !string.IsNullOrEmpty(SavePath)) await SaveAsync();
            await Task.Delay(1000);
        }
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
            if (AutoSave && !string.IsNullOrEmpty(SavePath)) await SaveAsync();
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
            if (AutoSave && !string.IsNullOrEmpty(SavePath)) await SaveAsync();
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
            if (AutoSave && !string.IsNullOrEmpty(SavePath)) await SaveAsync();
        }
        finally
        {
            _WeeklySemaphore.Release();
        }
    }

    private async Task ProcessEvents<T>(SortedList<DateTime, T> events, SemaphoreSlim semaphore, bool isRepeating = false) where T : EventBase
    {
        await semaphore.WaitAsync();
        try
        {
            if (events.Any())
            {
                var firstEvent = events.Values.First();
                bool remove = false;
                try
                {
                    remove = await firstEvent.ExecuteEvaluate(); // Ensure async task completion
                }
                catch (Exception e)
                {
                    Console.WriteLine($"failed to execute event {firstEvent.ToString()}");
                    Debug.WriteLine($"failed to execute event {firstEvent.ToString()}");
                }
                finally
                {
                    events.RemoveAt(0);
                    if (!remove && isRepeating) events.Add(firstEvent.StartTime, firstEvent);
                }
            }
        }
        finally
        {
            semaphore.Release();
        }
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
    public async Task SaveAsync(string? savePath = null)
    {
        if (!string.IsNullOrEmpty(savePath))
            SavePath = savePath;
        if (string.IsNullOrEmpty(SavePath))
            throw new ArgumentException("you need to define the savePath first!");
        if (!SavePath.EndsWith(".schedule"))
            SavePath += ".schedule";

        // Ensure the directory exists
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

        using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(fileStream, this, options);
        }

        File.Replace(tempFilePath, SavePath, null);
    }

    /// <summary>
    /// Loads the state of the scheduler from a file.
    /// </summary>
    /// <param name="filePath">The file path from where the scheduler data should be loaded.</param>
    public async Task LoadAsync(string? filePath = null, bool throwOnNotExist = true )
    {
        if (!string.IsNullOrEmpty(filePath))
            SavePath = filePath;
        if (string.IsNullOrEmpty(SavePath))
            throw new ArgumentException("you need to define the savePath first!");
        if (!SavePath.EndsWith(".schedule"))
            SavePath += ".schedule";
        if (throwOnNotExist && !File.Exists(SavePath))
            throw new FileNotFoundException("Scheduler state file not found.", SavePath);

        var options = new JsonSerializerOptions
        {
            Converters = { new JsonStringEnumConverter() }
        };

        using (var fileStream = new FileStream(SavePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var state = await JsonSerializer.DeserializeAsync<dynamic>(fileStream, options);
            if (state != null)
            {
                this.OneTimeEvents = state.OneTimeEvents ?? new SortedList<DateTime, OneTimeEvent>();
                this.CronJobs = state.CronJobs ?? new SortedList<DateTime, RepeatingEvent>();
                this.WeeklySchedule = state.WeeklySchedule ?? new SortedList<DateTime, WeeklyEvent>();
            }
        }
    }
}