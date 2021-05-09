using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MessagePack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Traceman.Explorer.Views
{
    public class DrawCanvas : Canvas
    {
        private SolidColorBrush _brush;
        private SolidColorBrush _textBrush;
        private Pen _pen;
        private List<TimeEvent> _events;

        private List<RectangleGeometry> _geometries;

        private bool _geometriesUpToDate = false;

        private DateTime _minTime;

        public DrawCanvas()
        {
            _brush = new SolidColorBrush(Colors.Beige);
            _textBrush = new SolidColorBrush(Colors.Black);
            _pen = new Pen(_textBrush, 1);
            _events = new List<TimeEvent>();
            _geometries = new List<RectangleGeometry>();

            Events events;

            using (FileStream fs = new FileStream(Environment.ExpandEnvironmentVariables(@"%tmp%\\traceman_output.trm"), FileMode.Open))
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
                foreach (var evt in pair.Value.OfType<EventExceptionTraceData>())
                {
                    AddEvent(new TimeEvent() { time = evt.TimeStamp, duration = 1, threadId = pair.Key, text = evt.Type });
                }
            }

            this.PointerWheelChanged += DrawCanvas_PointerWheelChanged;
        }

        private void DrawCanvas_PointerWheelChanged(object? sender, Avalonia.Input.PointerWheelEventArgs e)
        {
            _scale += 0.1 * e.Delta.Y;
            _geometriesUpToDate = false;
            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (!_geometriesUpToDate)
                UpdateGeometries();

            for (int i = 0; i < _geometries.Count; i++)
            {
                context.DrawGeometry(new SolidColorBrush(Utils.ColorFromHSV(GetHue(_events[i].text), 0.8, 1)), null, _geometries[i]);
                //context.DrawText(_textBrush, new Point(rect.X, rect.Y), new FormattedText(evt.text, Typeface.Default, 10, TextAlignment.Left, TextWrapping.NoWrap, Size.Infinity));
            }
        }

        private double GetHue(string str)
        {
            double hue = 0;
            //for (int i = 0; i < str.Length; i++)
            //{
            //    hue += str[i];
            //}
            hue = str.GetHashCode();
            hue %= 20;
            hue *= 0.05d;
            return hue;
        }

        private double _scale = 0.5d;

        private void UpdateGeometries()
        {
            _minTime = DateTime.MaxValue;
            //_maxTime = DateTime.MinValue;

            Dictionary<int, int> _threadIdToRow = new Dictionary<int, int>();

            foreach (var evt in _events)
            {
                if (evt.time < _minTime)
                    _minTime = evt.time;

                _threadIdToRow.TryAdd(evt.threadId, _threadIdToRow.Count);
            }

            _geometries.Clear();

            foreach (var evt in _events)
            {
                Rect rect = new Rect(_scale * (evt.time - _minTime).TotalMilliseconds, 20 * _threadIdToRow[evt.threadId], 1 /*_scale * evt.duration*/, 15);
                _geometries.Add(new RectangleGeometry(rect));
            }

            _geometriesUpToDate = true;
        }

        public void AddEvent(TimeEvent timeEvent)
        {
            _events.Add(timeEvent);
            _geometriesUpToDate = false;
        }
    }

    public struct TimeEvent
    {
        public int threadId;
        public DateTime time;
        public double duration;
        public string text;
    }
}