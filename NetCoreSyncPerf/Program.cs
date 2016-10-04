using Microsoft.Extensions.Configuration;
using System;
using System.Diagnostics;
using System.Threading;

namespace NetCoreSyncPerf
{
    public class Program
    {
        private static readonly int _defaultThreads = Environment.ProcessorCount;
        private static readonly TimeSpan _defaultDuration = TimeSpan.FromSeconds(10);
        private static readonly Sync _defaultSync = Sync.Lock;
        private static readonly Workload _defaultWorkload = Workload.Read;

        // On a 12-core server, 7 iterations results in approximately the same RPS as PlaintextSyncPerf(sync=nolock, threads=256)
        private static readonly int _defaultWorkOutsideLock = 7;
        private static readonly int _defaultWorkInsideLock = 1;

        private static long _locksTaken = 0;

        private static object _lock = new object();
        private static ReaderWriterLockSlim _readerWriterLockSlim = new ReaderWriterLockSlim();

        public static void Main(string[] args)
        {
            var config = new ConfigurationBuilder().AddCommandLine(args).Build();

            var threads = int.Parse(config["threads"] ?? _defaultThreads.ToString());
            var duration = TimeSpan.FromSeconds(int.Parse(config["duration"] ?? _defaultDuration.TotalSeconds.ToString()));
            var syncPrimitive = (Sync)Enum.Parse(typeof(Sync),
                config["sync"] ?? _defaultSync.ToString(), ignoreCase: true);
            var workload = (Workload)Enum.Parse(typeof(Workload),
                config["workload"] ?? _defaultWorkload.ToString(), ignoreCase: true);

            var workOutsideLock = int.Parse(config["workOutsideLock"] ?? _defaultWorkOutsideLock.ToString());
            var workInsideLock = int.Parse(config["workInsideLock"] ?? _defaultWorkInsideLock.ToString());

            Console.WriteLine($"Duration: {duration}");
            Console.WriteLine($"Threads: {threads}");
            Console.WriteLine($"SyncPrimitive: {syncPrimitive}");
            Console.WriteLine($"Workload: {workload}");
            Console.WriteLine($"Work Outside Lock: {workOutsideLock}");
            Console.WriteLine($"Work Inside Lock: {workInsideLock}");
            Console.WriteLine();

            var sw = new Stopwatch();

            var threadObjects = new Thread[threads];
            for (var i = 0; i < threads; i++)
            {
                threadObjects[i] = new Thread(() =>
                {
                    if (syncPrimitive == Sync.NoLock)
                    {
                        while (sw.Elapsed < duration)
                        {
                            DoWork(workInsideLock);
                            DoWork(workOutsideLock);

                            Interlocked.Increment(ref _locksTaken);
                        }
                    }
                    else if (syncPrimitive == Sync.Lock)
                    {
                        while (sw.Elapsed < duration)
                        {
                            lock (_lock)
                            {
                                DoWork(workInsideLock);
                            }
                            DoWork(workOutsideLock);

                            Interlocked.Increment(ref _locksTaken);
                        }
                    }
                    else if (syncPrimitive == Sync.Rwls)
                    {
                        if (workload == Workload.Read)
                        {
                            while (sw.Elapsed < duration)
                            {
                                _readerWriterLockSlim.EnterReadLock();
                                try
                                {
                                    DoWork(workInsideLock);
                                }
                                finally
                                {
                                    _readerWriterLockSlim.ExitReadLock();
                                }
                                DoWork(workOutsideLock);

                                Interlocked.Increment(ref _locksTaken);
                            }
                        }
                    }
                });
            }

            sw.Start();
            for (var i = 0; i < threads; i++)
            {
                threadObjects[i].Start();
            }

            for (var i = 0; i < threads; i++)
            {
                threadObjects[i].Join();
            }
            sw.Stop();

            Console.WriteLine($"Actual Duration: {sw.Elapsed}");
            Console.WriteLine($"Total Locks Taken: {_locksTaken.ToString("N0")}");

            var locksTakenPerSecond = (int)Math.Round(_locksTaken / sw.Elapsed.TotalSeconds, 0);
            Console.WriteLine($"Locks Taken per Second: {locksTakenPerSecond.ToString("N0")}");
        }

        private static void DoWork(int iterations)
        {
            for (int i = 0; i < iterations; i++)
            {
                DateTime.Now.ToString();
            }
        }
    }

    public enum Sync
    {
        NoLock,
        Lock,
        Rwls,
    }

    public enum Workload
    {
        Read,
    }
}
