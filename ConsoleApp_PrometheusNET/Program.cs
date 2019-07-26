using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Prometheus;

namespace ConsoleApp_PrometheusNET
{
    class Program
    {
        private static Counter AppHeartbeat;
        private static Counter AppRequests;
        private static Gauge AppThreads;
        private static Histogram AppLatency;

        private static object locker;

        static void Main(string[] args)
        {
            //metrics will be served on http://localhost:9009/metrics
            var metricserver = new MetricServer(hostname: "localhost", port: 9090);
            metricserver.Start();

            //create metrics registry and factory for creating and collecting metrics
            var registry = Metrics.DefaultRegistry;
            var factory = Metrics.WithCustomRegistry(registry);
            locker = new object();

            //initialize our metrics
            AppHeartbeat = factory.CreateCounter("app_heartbeat", "application heartbeat");

            AppRequests = factory.CreateCounter("app_requests", "application requests count", new CounterConfiguration()
            {
                LabelNames = new[] { "status" }
            });

            AppLatency = factory.CreateHistogram("app_latency", "application latency milliseconds", new HistogramConfiguration
            {
                Buckets = new[] { 10.0, 50.0, 100.0, 500.0, 1000.0 }
            });

            AppThreads = factory.CreateGauge("app_threads", "application threads count");
            AppThreads.Set(0);

            for (var i = 0; i < 10; i++)
            {
                var t = new Thread(new ThreadStart(DoSomeWork));
                t.Start();
            }

            var rand = new Random();

            while (true)
            {
                //Increment Heartbeat indicating application is responsive
                AppHeartbeat.Inc();

                //Observe application thread count - demo of gauge
                AppThreads.Set(System.Diagnostics.Process.GetCurrentProcess().Threads.Count);

                //start timer to observe latency
                var t = AppLatency.NewTimer();

                var r = rand.Next(0, 1100);
                // demo use of labels to count app requests success/fail
                if (r % 5 == 0)
                {
                    AppRequests.WithLabels("503").Inc();
                }
                else
                {
                    AppRequests.WithLabels("200").Inc();
                }

                Thread.Sleep(r);

                //observer latency using above timer
                AppLatency.Observe(t.ObserveDuration().TotalMilliseconds);

                //output metrics to console for demonstration purpose
                //in realworld we output the metrics using metricserver above
                var stream = new MemoryStream();
                registry.CollectAndExportAsTextAsync(stream);
                
                stream.Position = 0;
                var text = new StreamReader(stream).ReadToEnd();
                
                lock (locker)
                {
                    Console.WriteLine();
                    Console.WriteLine(text);
                }
            }
        }

        /// <summary>
        /// Test Thread Worker
        /// </summary>
        static void DoSomeWork()
        {
            var rand = new Random();

            while (true)
            {
                AppHeartbeat.Inc();
                AppThreads.Set(System.Diagnostics.Process.GetCurrentProcess().Threads.Count);

                var t = AppLatency.NewTimer();

                var r = rand.Next(0, 1100);
                if (r % 5 == 0)
                {
                    AppRequests.WithLabels("503").Inc();
                }
                else
                {
                    AppRequests.WithLabels("200").Inc();
                }

                Thread.Sleep(r);
                Console.Write(".");

                AppLatency.Observe(t.ObserveDuration().TotalMilliseconds);
            }
        }
    }
}
