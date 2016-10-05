using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace PlaintextSyncPerf
{
    public class Program
    {
        private static readonly int _defaultWorkInsideLock = 1;

        private static readonly byte[] _noLockPayload = Encoding.UTF8.GetBytes("Hello,noLock!");
        private static readonly byte[] _lockPayload = Encoding.UTF8.GetBytes("Hello,  lock!");
        private static readonly byte[] _rwlsPayload = Encoding.UTF8.GetBytes("Hello,  rwls!");
        private static readonly byte[] _rDictPayload = Encoding.UTF8.GetBytes("Hello, rDict!");
        private static readonly byte[] _cDictPayload = Encoding.UTF8.GetBytes("Hello, cDict!");

        private static readonly PathString _noLockPath = new PathString("/nolock");
        private static readonly PathString _lockPath = new PathString("/lock");
        private static readonly PathString _rwlsPath = new PathString("/rwls");
        private static readonly PathString _rDictPath = new PathString("/rdict");
        private static readonly PathString _cDictPath = new PathString("/cdict");

        private static readonly object _lock = new object();
        private static readonly ReaderWriterLockSlim _rwls = new ReaderWriterLockSlim();

        private static readonly object _key = new object();
        private static readonly Dictionary<object, byte[]> _dict = new Dictionary<object, byte[]>();
        private static readonly ConcurrentDictionary<object, byte[]> _cDict = new ConcurrentDictionary<object, byte[]>();

        static Program()
        {
            _dict[_key] = _rDictPayload;
            _cDict[_key] = _cDictPayload;
        }

        public static void Main(string[] args)
        {
            var config = new ConfigurationBuilder().AddCommandLine(args).Build();

            var workInsideLock = int.Parse(config["workInsideLock"] ?? _defaultWorkInsideLock.ToString());

            Console.WriteLine($"Work Inside Lock: {workInsideLock}");
            Console.WriteLine();

            new WebHostBuilder()
                .UseUrls("http://+:5000")
                .UseKestrel()
                .Configure(app =>
                {
                    app.Run(async context =>
                    {
                        context.Response.StatusCode = 200;
                        context.Response.ContentType = "text/plain";
                        context.Response.Headers["Content-Length"] = "13";

                        byte[] payload;
                        if (context.Request.Path.StartsWithSegments(_noLockPath, StringComparison.Ordinal))
                        {
                            DoWork(workInsideLock);
                            payload = _noLockPayload;
                        }
                        else if (context.Request.Path.StartsWithSegments(_lockPath, StringComparison.Ordinal))
                        {
                            lock (_lock)
                            {
                                DoWork(workInsideLock);
                            }
                            payload = _lockPayload;
                        }
                        else if (context.Request.Path.StartsWithSegments(_rwlsPath, StringComparison.Ordinal))
                        {
                            _rwls.EnterReadLock();
                            try
                            {
                                DoWork(workInsideLock);
                            }
                            finally
                            {
                                _rwls.ExitReadLock();
                            }
                            payload = _rwlsPayload;
                        }
                        else if (context.Request.Path.StartsWithSegments(_rDictPath, StringComparison.Ordinal))
                        {
                            _rwls.EnterReadLock();
                            try
                            {
                                _dict.TryGetValue(_key, out payload);
                            }
                            finally
                            {
                                _rwls.ExitReadLock();
                            }
                        }
                        else if (context.Request.Path.StartsWithSegments(_cDictPath, StringComparison.Ordinal))
                        {
                            _cDict.TryGetValue(_key, out payload);
                        }
                        else
                        {
                            throw new InvalidOperationException();
                        }

                        await context.Response.Body.WriteAsync(payload, 0, payload.Length);
                    });
                })
                .Build()
                .Run();
        }

        private static void DoWork(int iterations)
        {
            for (int i = 0; i < iterations; i++)
            {
                DateTime.Now.ToString();
            }
        }
    }
}
