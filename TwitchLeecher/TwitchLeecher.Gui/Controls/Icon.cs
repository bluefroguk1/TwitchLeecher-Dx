using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace TwitchLeecher.Gui.Controls
{
    /// <summary>
    /// Draws a Font Awesome icon by name, e.g. <c>fa-solid fa-download</c>, in the current text colour.
    /// Like text, it is sized by FontSize: the icon is fitted into a FontSize square and centred.
    /// The shapes live in <see cref="IconData"/>; add an entry there to use a new icon.
    /// </summary>
    public class Icon : Control
    {
        public static readonly StyledProperty<string> ValueProperty =
            AvaloniaProperty.Register<Icon, string>(nameof(Value), string.Empty);

        public static readonly StyledProperty<IBrush> ForegroundProperty =
            TextElement.ForegroundProperty.AddOwner<Icon>();

        public static readonly StyledProperty<double> FontSizeProperty =
            TextElement.FontSizeProperty.AddOwner<Icon>();

        static Icon()
        {
            AffectsRender<Icon>(ValueProperty, ForegroundProperty);
            AffectsMeasure<Icon>(FontSizeProperty);
        }

        public string Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public IBrush Foreground
        {
            get => GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        public double FontSize
        {
            get => GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        protected override Size MeasureOverride(Size availableSize) => new Size(FontSize, FontSize);

        public override void Render(DrawingContext context)
        {
            if (string.IsNullOrEmpty(Value))
            {
                return;
            }

            if (!IconData.TryGet(Value, out IconData.Shape shape))
            {
                Debug.Fail($"Unknown icon '{Value}': add it to IconData.");
                return;
            }

            double scale = FontSize / System.Math.Max(shape.Width, shape.Height);
            double x = (Bounds.Width - shape.Width * scale) / 2;
            double y = (Bounds.Height - shape.Height * scale) / 2;

            using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(x, y)))
            {
                context.DrawGeometry(Foreground ?? Brushes.Black, null, shape.Geometry);
            }
        }
    }
}
