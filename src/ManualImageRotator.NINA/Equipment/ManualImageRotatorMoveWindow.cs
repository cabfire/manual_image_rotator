using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ManualImageRotator.NINA.Equipment {
    public sealed class ManualImageRotatorMoveWindow : Window {
        private readonly TextBlock currentPositionTextBlock;
        private readonly TextBlock targetPositionTextBlock;
        private readonly TextBlock deltaTextBlock;
        private readonly TextBlock directionTextBlock;
        private readonly TextBlock statusTextBlock;
        private readonly TextBlock matchedStarsValueTextBlock;
        private readonly TextBlock qualityValueTextBlock;
        private readonly Button actionButton;
        private readonly Line measuredNeedle;
        private readonly Line targetNeedle;
        private readonly Rectangle cameraBody;
        private readonly RotateTransform cameraRotation;
        private readonly Brush foregroundBrush;
        private readonly Brush mutedBrush;
        private readonly Brush targetBrush;
        private readonly Brush reachedBrush;
        private readonly Brush warningBrush;
        private readonly Brush badBrush;
        private bool targetReached;

        public event EventHandler OkRequested;

        public ManualImageRotatorMoveWindow() {
            var backgroundBrush = Brush(25, 27, 31);
            var panelBrush = Brush(29, 31, 36);
            var borderBrush = Brush(70, 75, 86);
            foregroundBrush = Brush(244, 245, 250);
            mutedBrush = Brush(155, 160, 170);
            var blueBrush = Brush(31, 111, 255);
            targetBrush = Brush(225, 228, 235);
            reachedBrush = Brush(55, 210, 120);
            warningBrush = Brush(245, 178, 66);
            badBrush = Brush(240, 86, 86);
            Title = "Camera rotation required";
            Width = 800;
            Height = 600;
            MinWidth = 640;
            MinHeight = 480;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = backgroundBrush;
            Foreground = foregroundBrush;

            var root = new Grid {
                Background = backgroundBrush
            };
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(238) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var positionsGrid = new Grid {
                Background = panelBrush
            };
            positionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            positionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(positionsGrid, 0);
            root.Children.Add(positionsGrid);

            var currentPanel = CreatePositionPanel("Current position", out currentPositionTextBlock, foregroundBrush, mutedBrush);
            Grid.SetColumn(currentPanel, 0);
            positionsGrid.Children.Add(currentPanel);

            var targetPanel = CreatePositionPanel("Target position", out targetPositionTextBlock, foregroundBrush, mutedBrush);
            Grid.SetColumn(targetPanel, 1);
            positionsGrid.Children.Add(targetPanel);

            var separator = new Border {
                Width = 2,
                Background = borderBrush,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(separator, 0);
            positionsGrid.Children.Add(separator);

            var canvas = new Canvas {
                Background = backgroundBrush,
                ClipToBounds = true
            };
            Grid.SetRow(canvas, 1);
            root.Children.Add(canvas);

            cameraRotation = new RotateTransform(0, 0, 0);
            cameraBody = new Rectangle {
                Width = 52,
                Height = 92,
                Stroke = targetBrush,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 8, 6 },
                Fill = Brushes.Transparent,
                RenderTransform = cameraRotation,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            canvas.Children.Add(cameraBody);

            targetNeedle = new Line {
                Stroke = targetBrush,
                StrokeThickness = 3,
                StrokeDashArray = new DoubleCollection { 8, 6 },
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            canvas.Children.Add(targetNeedle);

            measuredNeedle = new Line {
                Stroke = blueBrush,
                StrokeThickness = 7,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            canvas.Children.Add(measuredNeedle);

            deltaTextBlock = new TextBlock {
                FontSize = 22,
                Foreground = foregroundBrush,
                TextAlignment = TextAlignment.Center
            };
            canvas.Children.Add(deltaTextBlock);

            directionTextBlock = new TextBlock {
                FontSize = 22,
                Foreground = foregroundBrush,
                TextAlignment = TextAlignment.Center
            };
            canvas.Children.Add(directionTextBlock);

            statusTextBlock = new TextBlock {
                FontSize = 14,
                Foreground = mutedBrush,
                TextAlignment = TextAlignment.Center
            };
            canvas.Children.Add(statusTextBlock);

            canvas.SizeChanged += (sender, args) => LayoutAngleCanvas(canvas);

            var bottom = new Grid {
                Margin = new Thickness(8, 8, 8, 8)
            };
            bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetRow(bottom, 2);
            root.Children.Add(bottom);

            var metricsPanel = new StackPanel {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 16, 0)
            };
            Grid.SetColumn(metricsPanel, 0);
            bottom.Children.Add(metricsPanel);

            metricsPanel.Children.Add(new TextBlock {
                Text = "Matched Stars: ",
                FontSize = 14,
                Foreground = mutedBrush,
                VerticalAlignment = VerticalAlignment.Center
            });

            matchedStarsValueTextBlock = new TextBlock {
                Text = "--",
                FontSize = 14,
                Foreground = foregroundBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            metricsPanel.Children.Add(matchedStarsValueTextBlock);

            metricsPanel.Children.Add(new TextBlock {
                Text = "    Quality: ",
                FontSize = 14,
                Foreground = mutedBrush,
                VerticalAlignment = VerticalAlignment.Center
            });

            qualityValueTextBlock = new TextBlock {
                Text = "--",
                FontSize = 14,
                Foreground = mutedBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            metricsPanel.Children.Add(qualityValueTextBlock);

            actionButton = new Button {
                Content = "OK",
                Width = 200,
                Height = 48,
                FontSize = 24,
                Foreground = blueBrush,
                Background = Brush(56, 61, 70),
                BorderBrush = Brush(56, 61, 70),
                IsDefault = true
            };
            actionButton.Click += OnActionButtonClick;
            Grid.SetColumn(actionButton, 1);
            bottom.Children.Add(actionButton);

            Content = root;
        }

        public void Update(double currentPosition, double targetPosition, string status, bool targetReached) {
            Update(currentPosition, targetPosition, status, targetReached, 0, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);
        }

        public void Update(
            double currentPosition,
            double targetPosition,
            string status,
            bool targetReached,
            int matchedStars,
            double rmsPixels,
            double quality,
            double translationX,
            double translationY,
            double scale) {
            var current = Normalize360(currentPosition);
            var target = Normalize360(targetPosition);
            var delta = NormalizeSigned(target - current);
            this.targetReached = targetReached;

            currentPositionTextBlock.Text = FormatAngle(current);
            targetPositionTextBlock.Text = FormatAngle(target);
            deltaTextBlock.Text = FormatAngle(Math.Abs(delta));
            directionTextBlock.Text = delta >= 0.0 ? "Clockwise" : "Anticlockwise";
            statusTextBlock.Text = status ?? string.Empty;
            statusTextBlock.Foreground = targetReached ? reachedBrush : mutedBrush;
            matchedStarsValueTextBlock.Text = FormatMatchedStars(matchedStars);
            qualityValueTextBlock.Text = FormatQuality(quality);
            qualityValueTextBlock.Foreground = QualityBrush(quality);
            actionButton.Content = "OK";

            var targetStateBrush = targetReached ? reachedBrush : targetBrush;
            targetPositionTextBlock.Foreground = targetStateBrush;
            targetNeedle.Stroke = targetStateBrush;
            cameraBody.Stroke = targetStateBrush;
            deltaTextBlock.Foreground = targetReached ? reachedBrush : foregroundBrush;
            directionTextBlock.Foreground = targetReached ? reachedBrush : foregroundBrush;

            UpdateNeedle(measuredNeedle, current, 84.0);
            UpdateNeedle(targetNeedle, target, 74.0);
            cameraRotation.Angle = target;
        }

        public void Update(double currentPosition, double targetPosition, string status) {
            Update(currentPosition, targetPosition, status, false);
        }

        private string FormatMatchedStars(int matchedStars) {
            return matchedStars > 0
                ? matchedStars.ToString(CultureInfo.InvariantCulture)
                : "--";
        }

        private string FormatQuality(double quality) {
            return double.IsNaN(quality) || double.IsInfinity(quality)
                ? "--"
                : quality.ToString("0.000", CultureInfo.InvariantCulture);
        }

        private Brush QualityBrush(double quality) {
            if (double.IsNaN(quality) || double.IsInfinity(quality)) {
                return mutedBrush;
            }

            if (quality >= 0.70) {
                return reachedBrush;
            }

            if (quality >= 0.35) {
                return warningBrush;
            }

            return badBrush;
        }

        private void OnActionButtonClick(object sender, RoutedEventArgs e) {
            if (targetReached) {
                Close();
                return;
            }

            OkRequested?.Invoke(this, EventArgs.Empty);
        }

        private static Grid CreatePositionPanel(
            string title,
            out TextBlock valueTextBlock,
            Brush foregroundBrush,
            Brush mutedBrush) {
            var grid = new Grid {
                Margin = new Thickness(18, 0, 18, 0)
            };
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(62) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var titleBlock = new TextBlock {
                Text = title,
                FontSize = 22,
                Foreground = foregroundBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(titleBlock, 0);
            grid.Children.Add(titleBlock);

            valueTextBlock = new TextBlock {
                Text = "0.00°",
                FontSize = 60,
                Foreground = foregroundBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(valueTextBlock, 1);
            grid.Children.Add(valueTextBlock);

            return grid;
        }

        private void LayoutAngleCanvas(Canvas canvas) {
            var centerX = canvas.ActualWidth / 2.0;
            var centerY = Math.Max(110.0, canvas.ActualHeight * 0.26);

            Canvas.SetLeft(cameraBody, centerX - (cameraBody.Width / 2.0));
            Canvas.SetTop(cameraBody, centerY - (cameraBody.Height / 2.0));

            Canvas.SetLeft(deltaTextBlock, centerX - 120.0);
            Canvas.SetTop(deltaTextBlock, centerY + 118.0);
            deltaTextBlock.Width = 240.0;

            Canvas.SetLeft(directionTextBlock, centerX - 160.0);
            Canvas.SetTop(directionTextBlock, centerY + 150.0);
            directionTextBlock.Width = 320.0;

            Canvas.SetLeft(statusTextBlock, centerX - 180.0);
            Canvas.SetTop(statusTextBlock, centerY + 188.0);
            statusTextBlock.Width = 360.0;

            UpdateNeedle(measuredNeedle, ExtractNeedleAngle(measuredNeedle), 84.0);
            UpdateNeedle(targetNeedle, ExtractNeedleAngle(targetNeedle), 74.0);
        }

        private void UpdateNeedle(Line needle, double angleDegrees, double length) {
            if (Content == null) {
                return;
            }

            var canvas = needle.Parent as Canvas;
            if (canvas == null || canvas.ActualWidth <= 0.0 || canvas.ActualHeight <= 0.0) {
                needle.Tag = angleDegrees;
                return;
            }

            var centerX = canvas.ActualWidth / 2.0;
            var centerY = Math.Max(110.0, canvas.ActualHeight * 0.26);
            var radians = angleDegrees * Math.PI / 180.0;
            var dx = Math.Sin(radians) * length;
            var dy = -Math.Cos(radians) * length;

            needle.X1 = centerX;
            needle.Y1 = centerY;
            needle.X2 = centerX + dx;
            needle.Y2 = centerY + dy;
            needle.Tag = angleDegrees;
        }

        private static double ExtractNeedleAngle(Line needle) {
            return needle.Tag is double angle ? angle : 0.0;
        }

        private static string FormatAngle(double angle) {
            return Normalize360(angle).ToString("0.00", CultureInfo.InvariantCulture) + "°";
        }

        private static string FormatSignedAngle(double angle) {
            return angle.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture) + "°";
        }

        private static double Normalize360(double angle) {
            angle %= 360.0;
            if (angle < 0.0) {
                angle += 360.0;
            }

            return angle;
        }

        private static double NormalizeSigned(double angle) {
            angle = (angle + 180.0) % 360.0;
            if (angle < 0.0) {
                angle += 360.0;
            }

            return angle - 180.0;
        }

        private static SolidColorBrush Brush(byte r, byte g, byte b) {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
    }
}
