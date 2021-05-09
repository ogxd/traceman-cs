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

            string output = Environment.ExpandEnvironmentVariables(@"%tmp%\\traceman_output.bin");

            TraceReader reader = new TraceReader();
            reader.Read(output, this);

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

            foreach (var geometry in _geometries)
            {
                context.DrawGeometry(_brush, _pen, geometry);
                //context.DrawText(_textBrush, new Point(rect.X, rect.Y), new FormattedText(evt.text, Typeface.Default, 10, TextAlignment.Left, TextWrapping.NoWrap, Size.Infinity));
            }
        }

        private double _scale = 0.5d;

        private void UpdateGeometries()
        {
            _minTime = DateTime.MaxValue;
            //_maxTime = DateTime.MinValue;

            Dictionary<int, int> _threadIdToRow = new Dictionary<int, int>();

            foreach (var evt in _events)
            {
                //if (evt.time < _minTime)
                //    _minTime = evt.time;

                _threadIdToRow.TryAdd(evt.threadId, _threadIdToRow.Count);
            }

            _geometries.Clear();

            foreach (var evt in _events)
            {
                Rect rect = new Rect(_scale * evt.time, 20 * _threadIdToRow[evt.threadId], _scale * evt.duration, 15);
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
        public double time;
        public double duration;
        public string text;
    }
}
