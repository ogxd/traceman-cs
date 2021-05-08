using MessagePack;
using System.Collections.Generic;

namespace Traceman
{
    /// <summary>
    /// https://docs.microsoft.com/fr-fr/dotnet/fundamentals/diagnostics/runtime-garbage-collection-events#gcheapstats_v2-event
    /// </summary>
    [MessagePackObject]
    public class Events
    {
        [Key(0)]
        public List<EventBase> Objects { get; set; } = new List<EventBase>();

        [Key(1)]
        public int Version { get; set; }
    }
}