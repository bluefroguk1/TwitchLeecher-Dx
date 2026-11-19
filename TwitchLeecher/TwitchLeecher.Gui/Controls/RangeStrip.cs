using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;

namespace TwitchLeecher.Gui.Controls
{
    // Filmstrip of preview frames across the whole video with two draggable handles (Start/End, in seconds).
    // With no frames it draws a plain range bar. The area outside the selection is dimmed.
    public class RangeStrip : Control
    {
        // matches DownloadParameters' "at least 5s long" crop rule, so dragging never produces an invalid range
        private const double MinLength = 5;

        public static readonly StyledProperty<IReadOnlyList<IImage>> FramesProperty =
            AvaloniaProperty.Register<RangeStrip, IReadOnlyList<IImage>>(nameof(Frames));

        public static readonly StyledProperty<double> DurationProperty =
            AvaloniaProperty.Register<RangeStrip, double>(nameof(Duration));

        public static readonly StyledProperty<double> StartProperty =
            AvaloniaProperty.Register<RangeStrip, double>(nameof(Start), defaultBindingMode: BindingMode.TwoWay);

        public static readonly StyledProperty<double> EndProperty =
            AvaloniaProperty.Register<RangeStrip, double>(nameof(End), defaultBindingMode: BindingMode.TwoWay);

        // theme brushes, set from XAML with DynamicResource so a theme switch re-renders
        public static readonly StyledProperty<IBrush> TrackBrushProperty =
            AvaloniaProperty.Register<RangeStrip, IBrush>(nameof(TrackBrush));

        public static readonly StyledProperty<IBrush> AccentBrushProperty =
            AvaloniaProperty.Register<RangeStrip, IBrush>(nameof(AccentBrush));

        public static readonly StyledProperty<IBrush> GapBrushProperty =
            AvaloniaProperty.Register<RangeStrip, IBrush>(nameof(GapBrush));

        // translucent overlay, readable on every theme
        private static readonly IBrush DimBrush = new SolidColorBrush(Color.FromArgb(184, 15, 15, 18));

        private bool? _draggingStart;

        static RangeStrip()
        {
            AffectsRender<RangeStrip>(FramesProperty, DurationProperty, StartProperty, EndProperty,
                TrackBrushProperty, AccentBrushProperty, GapBrushProperty);
        }

        public IReadOnlyList<IImage> Frames
        {
            get => GetValue(FramesProperty);
            set => SetValue(FramesProperty, value);
        }

        public double Duration
        {
            get => GetValue(DurationProperty);
            set => SetValue(DurationProperty, value);
        }

        public double Start
        {
            get => GetValue(StartProperty);
            set => SetValue(StartProperty, value);
        }

        public double End
        {
            get => GetValue(EndProperty);
            set => SetValue(EndProperty, value);
        }

        public IBrush TrackBrush
        {
            get => GetValue(TrackBrushProperty);
            set => SetValue(TrackBrushProperty, value);
        }

        public IBrush AccentBrush
        {
            get => GetValue(AccentBrushProperty);
            set => SetValue(AccentBrushProperty, value);
        }

        public IBrush GapBrush
        {
            get => GetValue(GapBrushProperty);
            set => SetValue(GapBrushProperty, value);
        }

        private double ToX(double seconds) => Duration > 0 ? Math.Clamp(seconds / Duration, 0, 1) * Bounds.Width : 0;

        public override void Render(DrawingContext context)
        {
            Rect bounds = new Rect(Bounds.Size);
            double width = bounds.Width;
            double height = bounds.Height;
            double x1 = ToX(Start);
            double x2 = ToX(End);
            IPen selectionPen = new Pen(AccentBrush, 2);
            IPen tileGapPen = new Pen(GapBrush, 1);

            using (context.PushClip(new RoundedRect(bounds, 6)))
            {
                if (TrackBrush != null)
                {
                    context.FillRectangle(TrackBrush, bounds);
                }

                IReadOnlyList<IImage> frames = Frames;

                if (frames is { Count: > 0 } && height > 0)
                {
                    // 16:9 tiles, each showing the frame at the start of its slice of the video
                    int tiles = Math.Max(1, (int)Math.Ceiling(width / (height * 16 / 9)));
                    double tileWidth = width / tiles;

                    for (int i = 0; i < tiles; i++)
                    {
                        IImage frame = frames[Math.Min(frames.Count - 1, i * frames.Count / tiles)];
                        context.DrawImage(frame, new Rect(frame.Size), new Rect(i * tileWidth, 0, tileWidth, height));
                        context.DrawLine(tileGapPen, new Point((i + 1) * tileWidth, 0), new Point((i + 1) * tileWidth, height));
                    }
                }

                context.FillRectangle(DimBrush, new Rect(0, 0, x1, height));
                context.FillRectangle(DimBrush, new Rect(x2, 0, Math.Max(0, width - x2), height));
            }

            context.DrawRectangle(null, selectionPen, new Rect(x1, 1, Math.Max(0, x2 - x1), Math.Max(0, height - 2)), 4, 4);
            DrawHandle(context, AccentBrush, x1, width, height);
            DrawHandle(context, AccentBrush, x2, width, height);
        }

        private static void DrawHandle(DrawingContext context, IBrush brush, double x, double width, double height)
        {
            double handleHeight = Math.Min(28, height);
            double left = Math.Clamp(x - 5, 0, Math.Max(0, width - 10));
            if (brush == null)
            {
                return;
            }

            context.FillRectangle(brush, new Rect(left, (height - handleHeight) / 2, 10, handleHeight), 3);
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (Duration <= 0)
            {
                return;
            }

            double x = e.GetPosition(this).X;
            _draggingStart = Math.Abs(x - ToX(Start)) <= Math.Abs(x - ToX(End));
            e.Pointer.Capture(this);
            MoveHandle(x);
            e.Handled = true;
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            if (_draggingStart != null)
            {
                MoveHandle(e.GetPosition(this).X);
            }
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            _draggingStart = null;
            e.Pointer.Capture(null);
        }

        protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
        {
            base.OnPointerCaptureLost(e);
            _draggingStart = null;
        }

        private void MoveHandle(double x)
        {
            double seconds = Math.Round(Math.Clamp(x / Math.Max(1, Bounds.Width), 0, 1) * Duration);

            if (_draggingStart == true)
            {
                Start = Math.Min(seconds, Math.Max(0, End - MinLength));
            }
            else
            {
                End = Math.Max(seconds, Math.Min(Duration, Start + MinLength));
            }
        }
    }
}
