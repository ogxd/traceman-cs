using MessagePack;
using System;

namespace Traceman
{
    [Union(0, typeof(EventExceptionTraceData))]
    public abstract class EventBase
    {
        [Key(0)]
        public DateTime TimeStamp { get; set; }

        [Key(1)]
        public int ThreadID { get; set; }
    }
}