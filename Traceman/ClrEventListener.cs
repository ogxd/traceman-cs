using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Traceman
{
    public class ClrEventListener : IDisposable
    {
        public int EventsReceived { get; private set; }
        public int EventsLost => _source?.EventsLost ?? 0;

        private EventPipeSession _session;
        private EventPipeEventSource _source;

        public ClrTraceEventParser Parser => _source.Clr;

        public ClrEventListener(int pid, Keywords keywords, EventLevel eventLevel)
        {
            var _client = new DiagnosticsClient(pid);
            var arguments = new Dictionary<string, string> { { "StacksEnabled", "true" } };
            var provider = new EventPipeProvider(ClrTraceEventParser.ProviderName, (System.Diagnostics.Tracing.EventLevel)eventLevel, (long)keywords, arguments);
            _session = _client.StartEventPipeSession(provider, false, circularBufferMB: 128);

            _source = new EventPipeEventSource(_session.EventStream);

            _source.Clr.All += (_) => EventsReceived++;

            Task.Run(_source.Process);
        }

        public void Dispose()
        {
            _source?.StopProcessing();
            _source?.Dispose();
            _session?.Stop();
            _session?.Dispose();
        }
    }
}