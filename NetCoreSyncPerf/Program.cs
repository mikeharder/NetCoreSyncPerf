using Microsoft.Extensions.Configuration;
using System;
using System.Threading;

namespace NetCoreSyncPerf
{
    public class Program
    {
        private static readonly int _defaultThreads = Environment.ProcessorCount;
        private static readonly TimeSpan _defaultDuration = TimeSpan.FromSeconds(10);
        private static readonly SyncPrimitive _defaultSyncPrimitive = SyncPrimitive.Lock;
        private static readonly Workload _defaultWorkload = Workload.Read;

        private static long _locksTaken = 0;

        private static object _lock = new object();
        private static ReaderWriterLockSlim _readerWriterLockSlim = new ReaderWriterLockSlim();

        public static void Main(string[] args)
        {
            var config = new ConfigurationBuilder().AddCommandLine(args).Build();

            var threads = int.Parse(config["threads"] ?? _defaultThreads.ToString());
            var duration = TimeSpan.FromSeconds(int.Parse(config["duration"] ?? _defaultDuration.TotalSeconds.ToString()));
            var syncPrimitive = (SyncPrimitive)Enum.Parse(typeof(SyncPrimitive),
                config["syncPrimitive"] ?? _defaultSyncPrimitive.ToString(), ignoreCase: true);
            var workload = (Workload)Enum.Parse(typeof(Workload),
                config["workload"] ?? _defaultWorkload.ToString(), ignoreCase: true);

            Console.WriteLine($"Duration: {duration}");
            Console.WriteLine($"Threads: {threads}");
            Console.WriteLine($"SyncPrimitive: {syncPrimitive}");
            Console.WriteLine($"Workload: {workload}");

            CancellationToken cancellationToken;

            var threadObjects = new Thread[threads];
            for (var i = 0; i < threads; i++)
            {
                threadObjects[i] = new Thread(() =>
                {
                    if (syncPrimitive == SyncPrimitive.Lock)
                    {
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            lock (_lock)
                            {
                            }

                            Interlocked.Increment(ref _locksTaken);
                        }
                    }
                    else if (syncPrimitive == SyncPrimitive.ReaderWriterLockSlim)
                    {
                        if (workload == Workload.Read)
                        {
                            while (!cancellationToken.IsCancellationRequested)
                            {
                                _readerWriterLockSlim.EnterReadLock();
                                try
                                {
                                }
                                finally
                                {
                                    _readerWriterLockSlim.ExitReadLock();
                                }

                                Interlocked.Increment(ref _locksTaken);
                            }
                        }
                    }
                });
            }

            cancellationToken = new CancellationTokenSource(duration).Token;

            for (var i = 0; i < threads; i++)
            {
                threadObjects[i].Start();
            }

            for (var i = 0; i < threads; i++)
            {
                threadObjects[i].Join();
            }

            Console.WriteLine($"Total Locks Taken: {_locksTaken.ToString("N0")}");

            var locksTakenPerSecond = (int)Math.Round(_locksTaken / duration.TotalSeconds, 0);
            Console.WriteLine($"Locks Taken per Second: {locksTakenPerSecond.ToString("N0")}");
        }
    }

    public enum SyncPrimitive
    {
        Lock,
        ReaderWriterLockSlim
    }

    public enum Workload
    {
        Read,
    }
}
