using MessagePack;

namespace Traceman
{
    [MessagePackObject]
    public class EventExceptionTraceData : EventBase
    {
        [Key(2)]
        public string Type { get; set; }

        [Key(3)]
        public string Message { get; set; }
    }
}