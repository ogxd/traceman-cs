using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Traceman.Collector
{
    public static class Debug
    {
        public static int ThreadId;

        public static async Task ConsumeMany(CancellationToken token)
        {
            Console.WriteLine("THREAD ID = " + Thread.CurrentThread.ManagedThreadId);
            ThreadId = Thread.CurrentThread.ManagedThreadId;
            do
            {
                Consume();
                await Task.Delay(1);
            } while (!token.IsCancellationRequested);
        }

        private static long Consume()
        {
            long i = 0;
            HashSet<string> list = new HashSet<string>();
            for (int k = 0; k < 10; k++)
            {
                list.Add($"hello world :) {k}");
                try
                {
                    i++;
                    throw new NullReferenceException("clapin");
                }
                catch (Exception ex)
                {
                    i--;
                }
            }
            return i;
        }
    }
}