using Microsoft.Diagnostics.Symbols;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Stacks;
using System;
using System.Collections.Generic;

namespace Traceman.Explorer
{
    public class TraceReader
    {
        public void Read(string output, DrawCanvas canvas)
        {
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



                    //PrintStack(threadId, samples[0], stackSource);
                }
            }
        }

        private void PrintStack(int threadId, StackSourceSample stackSourceSample, StackSource stackSource)
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
    }
}
