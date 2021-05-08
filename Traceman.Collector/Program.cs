using CommandLine;
using MessagePack;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Symbols;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Stacks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Traceman.Collector
{
    class Options
    {
        [Option('p', "process", Required = true, HelpText = "Process ID or Name.")]
        public string Process { get; set; }
    }

    class Program
    {
        static ClrEventListener _listener;
        static Events _events = new Events();

        static NumberFormatInfo _nfi;
        static NumberFormatInfo Nfi
        {
            get
            {
                if (_nfi == null)
                {
                    _nfi = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
                    _nfi.NumberGroupSeparator = " ";
                }
                return _nfi;
            }
        }

        static CancellationTokenSource _cts;

        static void Main(string[] args)
        {
            _cts = new CancellationTokenSource();
            _events.Version = Version.CURRENT;

            Task.Run(() =>
            {
                try
                {
                    Parser.Default.ParseArguments<Options>(args)
                        .WithNotParsed(HandleParseError)
                        .WithParsedAsync(RunOptions);
                }
                catch (Exception ex)
                {
                    _cts.Cancel();
                    Console.WriteLine(ex);
                }
            });

            Task.Run(async () =>
            {
                do
                {
                    await Task.Delay(1000);
                    if (_listener != null)
                    {
                        long eventsReceived = _listener.EventsReceived;
                        Console.WriteLine("Events received : " + eventsReceived.ToString("#,0", Nfi));
                    }
                } while (!_cts.Token.IsCancellationRequested);
            });

            Console.ReadKey();
            _cts.Cancel();
            _listener.Dispose();

            /*
            var lz4Options = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);
            byte[] bytes = MessagePackSerializer.Serialize(_events, lz4Options);

            string output = Environment.ExpandEnvironmentVariables(@"%tmp%\\traceman_output.bin");

            File.WriteAllBytes(output, bytes);

            Console.WriteLine($"Events written to '{output}'");
            */
        }

        private static void PrintStack(int threadId, StackSourceSample stackSourceSample, StackSource stackSource)
        {
            Console.WriteLine($"Thread (0x{threadId:X}):");
            var stackIndex = stackSourceSample.StackIndex;
            while (!stackSource.GetFrameName(stackSource.GetFrameIndex(stackIndex), verboseName: false).StartsWith("Thread ("))
            {
                Console.WriteLine($"  time:{stackSourceSample.TimeRelativeMSec}");
                Console.WriteLine($"  {stackSource.GetFrameName(stackSource.GetFrameIndex(stackIndex), verboseName: false)}"
                    .Replace("UNMANAGED_CODE_TIME", "[Native Frames]"));
                stackIndex = stackSource.GetCallerIndex(stackIndex);
            }
            Console.WriteLine();
        }

        static async Task Test(int pid)
        {
            var client = new DiagnosticsClient(pid);
            var providers = new List<EventPipeProvider>()
            {
                // "Microsoft-Windows-DotNETRuntime"
                new EventPipeProvider("Microsoft-DotNETCore-SampleProfiler", (System.Diagnostics.Tracing.EventLevel)EventLevel.Informational)
            };

            string output = Environment.ExpandEnvironmentVariables(@"%tmp%\\traceman_output.bin");

            // collect a *short* trace with stack samples
            // the hidden '--duration' flag can increase the time of this trace in case 10ms
            // is too short in a given environment, e.g., resource constrained systems
            // N.B. - This trace INCLUDES rundown.  For sufficiently large applications, it may take non-trivial time to collect
            //        the symbol data in rundown.
            using (EventPipeSession session = client.StartEventPipeSession(providers))
            using (FileStream fs = File.OpenWrite(output))
            {
                Task copyTask = session.EventStream.CopyToAsync(fs);
                await Task.Delay(5000);
                session.Stop();

                // check if rundown is taking more than 5 seconds and add comment to report
                Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
                Task completedTask = await Task.WhenAny(copyTask, timeoutTask);
                if (completedTask == timeoutTask)
                {
                    Console.WriteLine($"# Sufficiently large applications can cause this command to take non-trivial amounts of time");
                }
                await copyTask;
            }

            // using the generated trace file, symbolocate and compute stacks.
            string tempEtlxFilename = TraceLog.CreateFromEventPipeDataFile(output);
            using (var symbolReader = new SymbolReader(System.IO.TextWriter.Null) { SymbolPath = SymbolPath.MicrosoftSymbolServerPath })
            using (var eventLog = new TraceLog(tempEtlxFilename))
            {
                var stackSource = new MutableTraceEventStackSource(eventLog)
                {
                    OnlyManagedCodeStacks = true
                };

                var computer = new SampleProfilerThreadTimeComputer(eventLog, symbolReader);
                computer.GenerateThreadTimeStacks(stackSource);

                var samplesForThread = new Dictionary<int, List<StackSourceSample>>();

                stackSource.ForEach((sample) =>
                {
                    var stackIndex = sample.StackIndex;
                    while (!stackSource.GetFrameName(stackSource.GetFrameIndex(stackIndex), false).StartsWith("Thread ("))
                        stackIndex = stackSource.GetCallerIndex(stackIndex);

                    // long form for: int.Parse(threadFrame["Thread (".Length..^1)])
                    // Thread id is in the frame name as "Thread (<ID>)"
                    string template = "Thread (";
                    string threadFrame = stackSource.GetFrameName(stackSource.GetFrameIndex(stackIndex), false);
                    int threadId = int.Parse(threadFrame.Substring(template.Length, threadFrame.Length - (template.Length + 1)));

                    //if (threadId != Debug.ThreadId)
                    //    return;

                    if (samplesForThread.TryGetValue(threadId, out var samples))
                    {
                        samples.Add(sample);
                    }
                    else
                    {
                        samplesForThread[threadId] = new List<StackSourceSample>() { sample };
                    }
                });

                // For every thread recorded in our trace, print the first stack
                foreach (var (threadId, samples) in samplesForThread)
                {
#if DEBUG
                    Console.WriteLine($"Found {samples.Count} stacks for thread {threadId}");
#endif
                    PrintStack(threadId, samples[0], stackSource);
                }
            }
        }

        static async Task RunOptions(Options options)
        {
            int pid;

            Process process = null;

            if (options.Process == "test")
            {
                options.Process = Process.GetCurrentProcess().ProcessName;

                // Trigger some events
                var task = Task.Run(() =>
                {
                    Debug.ConsumeMany(_cts.Token);
                });
            } 

            if (int.TryParse(options.Process, out pid))
            {
                try
                {
                    process = Process.GetProcessById(pid);
                }
                catch
                {
                    if (process == null)
                        throw new Exception($"No process with id '{pid}' was found.");
                }
            }
            else
            {
                try
                {
                    process = Process.GetProcessesByName(options.Process).FirstOrDefault();
                }
                catch
                {
                    if (process == null)
                        throw new Exception($"No process with name '{options.Process}' was found.");
                }
                pid = process.Id;
            }

            //_listener = new ClrEventListener(pid, Keywords.Exception, EventLevel.Error);
            //_listener.Parser.AddCallbackForEvents<ExceptionTraceData>(EventPipeSessions_OnExceptionTraceData);

            await Test(pid);
        }

        private static void EventPipeSessions_OnExceptionTraceData(ExceptionTraceData obj)
        {
            var eventData = new EventExceptionTraceData();
            eventData.TimeStamp = obj.TimeStamp;
            eventData.ThreadID = obj.ThreadID;
            eventData.Type = string.Intern(obj.ExceptionType);
            eventData.Message = string.Intern(obj.ExceptionMessage);
            _events.Objects.Add(eventData);
        }

        static void HandleParseError(IEnumerable<Error> errors)
        {
            foreach (var error in errors)
                Console.WriteLine(error);
        }
    }
}