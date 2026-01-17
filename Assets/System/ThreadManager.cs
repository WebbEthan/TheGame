using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public static class ThreadManager
{
    private static string LogPath = "";
    public static int TerrainThreadID;
    public static int PlayerThreadID;

    public static LogHandler MainLog;
    public static LogHandler ObjectLog;
    public static void StartThreadManager()
    {
        // Start Logging
        LogPath = Path.Combine(Application.dataPath, "Editor", "Logs");
        MainLog = new LogHandler("MainLog");
        ObjectLog = new LogHandler("ObjectLog");
        // Start Main Thread Dispatcher

        // Start Threads
        TerrainThreadID = StartNewThread("Terraing Generation Thread");
        PlayerThreadID = StartNewThread("Dynamic Player Thread");


    }


    private static readonly ConcurrentQueue<Action> MainThreadExecutionQueue = new ConcurrentQueue<Action>();
    // Pushes operations to main thread
    public static void MainThreadDispacher(Action action)
    {
        if (action == null) return;
        MainThreadExecutionQueue.Enqueue(action);
    }

    // Awaits a task on the main thread and returns the result
    public static async Task<T> AwaitTaskResultOnMainThread<T>(Func<T> action)
    {
        var tcs = new TaskCompletionSource<T>();

        MainThreadDispacher(() =>
        {
            try
            {
                T result = action();
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        return await tcs.Task;
    }

    public static void MainThreadDispatchUpdate()
    {
        // Execute all queued actions on the Main Thread
        while (MainThreadExecutionQueue.TryDequeue(out var action))
        {
            action.Invoke();
        }
    }

    // Sync async bridge
    public static async Task<T> AwaitTaskResultOnThread<T>(int threadID, Func<T> action)
    {
        return await Threads[threadID].AwaitTaskResult(action);
    }
    // Fire and forget enqueue
    public static void ExecuteOnThread(int threadID, Action action)
    {
        Threads[threadID].Execute(action);
    }




    private static Dictionary<int, IndependentThread> Threads = new Dictionary<int, IndependentThread>();
    // Starts a new thread returns the ID
    private static int StartNewThread(string name = null)
    {
        int threadID = Threads.Count;
        Threads.Add(threadID, new IndependentThread(threadID, name));
        MainLog.LogItem($"Started New Thread (ID:{threadID}) : {name}");
        return threadID;
    }
    private class IndependentThread : IDisposable
    {
        private readonly ConcurrentQueue<Action> queue = new();
        private readonly AutoResetEvent signal = new(false);
        private readonly Thread thread;
        private volatile bool running = true;
        private int id;
        private string name;
        public IndependentThread(int id, string name)
        {
            this.id = id;
            this.name = name;
            thread = new Thread(RunLoop)
            {
                IsBackground = true,
                Name = "IndependentThread"
            };
            thread.Start();
        }
        // Fire and forget enqueue
        public void Execute(Action action)
        {
            if (!running) throw new ObjectDisposedException(nameof(IndependentThread));
            queue.Enqueue(action);
            // wake thread
            signal.Set();
        }
        // Sync async bridge
        public async Task<T> AwaitTaskResult<T>(Func<T> action)
        {
            var tcs = new TaskCompletionSource<T>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            Execute(() =>
            {
                try
                {
                    T result = action();
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            return await tcs.Task;
        }

        private void RunLoop()
        {
            while (running)
            {
                // Drain queue
                while (queue.TryDequeue(out var action))
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        MainLog.LogItem($"Issue In Thread [(ID:{id}) : {name}] : {ex}");
                    }
                }

                // Sleep until new work arrives
                signal.WaitOne();
            }
        }

        public void Dispose()
        {
            running = false;
            signal.Set();
            thread.Join();
            signal.Dispose();
        }
    }
    public class LogHandler
    {
        private readonly string logPath;
        private readonly object fileLock = new();

        public LogHandler(string logName)
        {
            // Ensure directory exists
            Directory.CreateDirectory(LogPath);

            logPath = Path.Combine(LogPath, $"{logName}.log");

            // Clear old log (truncate or create new)
            File.WriteAllText(logPath, string.Empty);

            // header
            WriteLine($"--- Log started {DateTime.Now:yyyy-MM-dd HH:mm:ss} ---");
        }


        public void LogItem(string msg)
        {
            WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {msg}");
            Debug.Log(msg);
        }

        private void WriteLine(string line)
        {
            lock (fileLock)
            {
                File.AppendAllText(logPath, line + Environment.NewLine);
            }
        }
    }
}
