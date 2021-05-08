using MessagePack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Traceman.Analyzer
{
    class Program
    {
        static void Main(string[] args)
        {
            Events events;

            using (FileStream fs = new FileStream(Environment.ExpandEnvironmentVariables(@"%tmp%\\traceman_output.bin"), FileMode.Open))
            {
                Console.WriteLine("Deserializing...");
                var lz4Options = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);
                events = MessagePackSerializer.Deserialize<Events>(fs, lz4Options);
            }

            Console.WriteLine("Analyzing...");

            var perThreadEvents = events.Objects
                .GroupBy(u => u.ThreadID)
                .ToDictionary(x => x.Key, x => x.ToArray());

            Console.WriteLine($"- Threads : {perThreadEvents.Count}");

            foreach (var pair in perThreadEvents.OrderByDescending(x => x.Value.Length))
            {
                var perException = pair.Value
                    .OfType<EventExceptionTraceData>()
                    .GroupBy(u => u.Type)
                    .ToDictionary(x => x.Key, x => x.ToArray());

                Console.WriteLine($"  - Thread id : {pair.Key}, exceptions : {pair.Value.Length}");

                foreach (var perType in perException)
                {
                    Console.WriteLine($"    - Exception type : {perType.Key}, count : {perType.Value.Length}");
                }
            }

            Console.ReadKey();
        }
    }
}
