using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Traceman.Explorer
{
    public class DrawCanvas : Canvas
    {
        private SolidColorBrush _brush;
        private SolidColorBrush _textBrush;
        private List<TimeEvent> _events;

        private bool _calibrated = false;

        private DateTime _minTime;

        public DrawCanvas()
        {
            _brush = new SolidColorBrush(Color.Parse("red"));
            _textBrush = new SolidColorBrush(Color.Parse("white"));
            _events = new List<TimeEvent>();

            string output = Environment.ExpandEnvironmentVariables(@"%tmp%\\traceman_output.bin");

            TraceReader reader = new TraceReader();
            reader.Read(output);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (!_calibrated)
                Calibrate();

            foreach (var evt in _events)
            {
                Rect rect = new Rect(0, 0, 100, 20);
                context.DrawRectangle(_brush, null, rect);
                context.DrawText(_textBrush, new Point(rect.X, rect.Y), new FormattedText(evt.text, Typeface.Default, 10, TextAlignment.Left, TextWrapping.NoWrap, Size.Infinity));
            }
        }

        private void Calibrate()
        {
            _minTime = DateTime.MaxValue;

            foreach (var evt in _events)
            {
                if (evt.time < _minTime)
                    _minTime = evt.time;
            }

            _calibrated = true;
        }

        public void AddEvent(DateTime time, string evt)
        {
            var geometry = new RectangleGeometry();
        }

        internal struct TimeEvent
        {
            public DateTime time;
            public string text;
        }
    }
}
