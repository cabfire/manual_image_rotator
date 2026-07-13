using ManualImageRotator.NINA.Imaging;
using System;
using System.Collections.Generic;
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
        private readonly TextBlock statusValueTextBlock;
        private readonly TextBlock matchedStarsValueTextBlock;
        private readonly TextBlock qualityValueTextBlock;
        private readonly Canvas diagnosticCanvas;
        private readonly Button actionButton;
        private readonly Path clockwiseArrow;
        private readonly Path anticlockwiseArrow;
        private readonly Polygon clockwiseArrowHead;
        private readonly Polygon anticlockwiseArrowHead;
        private readonly Line measuredNeedle;
        private readonly Line targetNeedle;
        private readonly Rectangle cameraBody;
        private readonly RotateTransform cameraRotation;
        private readonly Brush foregroundBrush;
        private readonly Brush mutedBrush;
        private readonly Brush measuredBrush;
        private readonly Brush targetBrush;
        private readonly Brush detectionZoneBrush;
        private readonly Brush reachedBrush;
        private readonly Brush warningBrush;
        private readonly Brush badBrush;
        private bool targetReached;
        private IReadOnlyList<StarCentroid> diagnosticStars;
        private int diagnosticFrameWidth;
        private int diagnosticFrameHeight;
        private int diagnosticMatchedStars;
        private double diagnosticCentralExclusionRatio;
        private bool diagnosticRejected;

        public event EventHandler OkRequested;

        public ManualImageRotatorMoveWindow() {
            var backgroundBrush = Brush(25, 27, 31);
            var panelBrush = Brush(29, 31, 36);
            var borderBrush = Brush(70, 75, 86);
            foregroundBrush = Brush(244, 245, 250);
            mutedBrush = Brush(155, 160, 170);
            measuredBrush = Brush(31, 111, 255);
            targetBrush = Brush(225, 228, 235);
            detectionZoneBrush = Brush(255, 225, 0);
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
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(150) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var positionsGrid = new Grid {
                Background = panelBrush
            };
            positionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            positionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(positionsGrid, 0);
            root.Children.Add(positionsGrid);

            var currentPanel = CreatePositionPanel("Current position", out currentPositionTextBlock, foregroundBrush, measuredBrush, false);
            Grid.SetColumn(currentPanel, 0);
            positionsGrid.Children.Add(currentPanel);

            var targetPanel = CreatePositionPanel("Target position", out targetPositionTextBlock, foregroundBrush, detectionZoneBrush, true);
            Grid.SetColumn(targetPanel, 1);
            positionsGrid.Children.Add(targetPanel);

            var separator = new Border {
                Width = 2,
                Background = borderBrush,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(separator, 0);
            positionsGrid.Children.Add(separator);

            var topHorizontalSeparator = new Border {
                Background = borderBrush,
                Opacity = 0.55
            };
            Grid.SetRow(topHorizontalSeparator, 1);
            root.Children.Add(topHorizontalSeparator);

            var contentGrid = new Grid {
                Background = backgroundBrush
            };
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(contentGrid, 2);
            root.Children.Add(contentGrid);

            var diagnosticBorder = new Border {
                Margin = new Thickness(18, 16, 16, 16),
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                Background = Brush(14, 16, 20),
                CornerRadius = new CornerRadius(4)
            };
            Grid.SetColumn(diagnosticBorder, 0);
            contentGrid.Children.Add(diagnosticBorder);

            diagnosticCanvas = new Canvas {
                ClipToBounds = true,
                Background = Brush(14, 16, 20)
            };
            diagnosticCanvas.SizeChanged += (sender, args) => RenderDiagnosticCanvas();
            diagnosticBorder.Child = diagnosticCanvas;

            var contentSeparator = new Border {
                Background = borderBrush,
                Opacity = 0.65
            };
            Grid.SetColumn(contentSeparator, 1);
            contentGrid.Children.Add(contentSeparator);

            var canvas = new Canvas {
                Background = backgroundBrush,
                ClipToBounds = true
            };
            Grid.SetColumn(canvas, 2);
            contentGrid.Children.Add(canvas);

            cameraRotation = new RotateTransform(0, 0, 0);
            cameraBody = new Rectangle {
                Width = 52,
                Height = 92,
                Stroke = targetBrush,
                StrokeThickness = 1.4,
                StrokeDashArray = new DoubleCollection { 8, 6 },
                Fill = Brushes.Transparent,
                RenderTransform = cameraRotation,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            canvas.Children.Add(cameraBody);

            targetNeedle = new Line {
                Stroke = targetBrush,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 8, 6 },
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            canvas.Children.Add(targetNeedle);

            measuredNeedle = new Line {
                Stroke = measuredBrush,
                StrokeThickness = 4,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            canvas.Children.Add(measuredNeedle);

            anticlockwiseArrow = CreateDirectionArrow();
            canvas.Children.Add(anticlockwiseArrow);

            clockwiseArrow = CreateDirectionArrow();
            canvas.Children.Add(clockwiseArrow);

            anticlockwiseArrowHead = CreateDirectionArrowHead();
            canvas.Children.Add(anticlockwiseArrowHead);

            clockwiseArrowHead = CreateDirectionArrowHead();
            canvas.Children.Add(clockwiseArrowHead);

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

            var bottomHorizontalSeparator = new Border {
                Background = borderBrush,
                Opacity = 0.55
            };
            Grid.SetRow(bottomHorizontalSeparator, 3);
            root.Children.Add(bottomHorizontalSeparator);

            var bottom = new Grid {
                Margin = new Thickness(8, 8, 8, 8)
            };
            bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetRow(bottom, 4);
            root.Children.Add(bottom);

            var metricsPanel = new Grid {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(12, 0, 16, 0)
            };
            metricsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            metricsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
            metricsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            metricsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
            metricsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(metricsPanel, 0);
            bottom.Children.Add(metricsPanel);

            var statusMetric = new StackPanel {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(statusMetric, 0);
            metricsPanel.Children.Add(statusMetric);

            statusMetric.Children.Add(new TextBlock {
                Text = "Status: ",
                FontSize = 14,
                Foreground = mutedBrush,
                VerticalAlignment = VerticalAlignment.Center
            });

            statusValueTextBlock = new TextBlock {
                Text = "--",
                FontSize = 14,
                Foreground = mutedBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            statusMetric.Children.Add(statusValueTextBlock);

            AddMetricSeparator(metricsPanel, 1, borderBrush);

            var matchedMetric = new StackPanel {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(matchedMetric, 2);
            metricsPanel.Children.Add(matchedMetric);

            matchedMetric.Children.Add(new TextBlock {
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
            matchedMetric.Children.Add(matchedStarsValueTextBlock);

            AddMetricSeparator(metricsPanel, 3, borderBrush);

            var qualityMetric = new StackPanel {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(qualityMetric, 4);
            metricsPanel.Children.Add(qualityMetric);

            qualityMetric.Children.Add(new TextBlock {
                Text = "Quality: ",
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
            qualityMetric.Children.Add(qualityValueTextBlock);

            actionButton = new Button {
                Content = "OK",
                Width = 200,
                Height = 48,
                FontSize = 24,
                Foreground = measuredBrush,
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
            Update(currentPosition, targetPosition, status, targetReached, 0, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, 0, 0, 0, null);
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
            double scale,
            int frameWidth,
            int frameHeight,
            double centralExclusionRatio,
            IReadOnlyList<StarCentroid> currentStars) {
            var current = Normalize360(currentPosition);
            var target = Normalize360(targetPosition);
            var delta = NormalizeSigned(target - current);
            this.targetReached = targetReached;

            currentPositionTextBlock.Text = FormatAngle(current);
            targetPositionTextBlock.Text = FormatAngle(target);
            deltaTextBlock.Text = FormatAngle(Math.Abs(delta));
            directionTextBlock.Text = delta >= 0.0 ? "Clockwise" : "Anticlockwise";
            statusTextBlock.Text = status ?? string.Empty;
            statusTextBlock.Foreground = StatusBrush(status, targetReached);
            statusValueTextBlock.Text = FormatStatus(status);
            statusValueTextBlock.Foreground = StatusBrush(status, targetReached);
            matchedStarsValueTextBlock.Text = FormatMatchedStars(matchedStars);
            matchedStarsValueTextBlock.Foreground = MatchedStarsBrush(matchedStars);
            qualityValueTextBlock.Text = FormatQuality(quality);
            qualityValueTextBlock.Foreground = QualityBrush(quality);
            actionButton.Content = "OK";
            diagnosticFrameWidth = frameWidth;
            diagnosticFrameHeight = frameHeight;
            diagnosticCentralExclusionRatio = centralExclusionRatio;
            diagnosticMatchedStars = matchedStars;
            diagnosticStars = currentStars;
            diagnosticRejected = IsRejected(status);
            RenderDiagnosticCanvas();

            var targetStateBrush = targetReached ? reachedBrush : targetBrush;
            targetPositionTextBlock.Foreground = targetStateBrush;
            targetNeedle.Stroke = targetStateBrush;
            cameraBody.Stroke = targetStateBrush;
            deltaTextBlock.Foreground = targetReached ? reachedBrush : foregroundBrush;
            directionTextBlock.Foreground = targetReached ? reachedBrush : foregroundBrush;
            UpdateDirectionArrows(delta, targetReached);

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

        private Brush MatchedStarsBrush(int matchedStars) {
            if (matchedStars <= 0) {
                return mutedBrush;
            }

            if (matchedStars >= 8) {
                return reachedBrush;
            }

            if (matchedStars >= 4) {
                return warningBrush;
            }

            return badBrush;
        }

        private string FormatStatus(string status) {
            return string.IsNullOrWhiteSpace(status) ? "--" : status;
        }

        private Brush StatusBrush(string status, bool targetReached) {
            if (targetReached) {
                return reachedBrush;
            }

            if (IsRejected(status)) {
                return badBrush;
            }

            if (!string.IsNullOrEmpty(status) &&
                status.StartsWith("Accepted", StringComparison.OrdinalIgnoreCase)) {
                return reachedBrush;
            }

            return mutedBrush;
        }

        private void OnActionButtonClick(object sender, RoutedEventArgs e) {
            if (targetReached) {
                Close();
                return;
            }

            OkRequested?.Invoke(this, EventArgs.Empty);
        }

        private bool IsRejected(string status) {
            return !string.IsNullOrEmpty(status) &&
                status.StartsWith("Rejected", StringComparison.OrdinalIgnoreCase);
        }

        private static void AddMetricSeparator(Grid grid, int column, Brush brush) {
            var separator = new Border {
                Width = 1,
                Height = 22,
                Background = brush,
                Opacity = 0.65,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(separator, column);
            grid.Children.Add(separator);
        }

        private static Grid CreatePositionPanel(
            string title,
            out TextBlock valueTextBlock,
            Brush foregroundBrush,
            Brush accentBrush,
            bool targetIcon) {
            var grid = new Grid {
                Margin = new Thickness(18, 0, 18, 0)
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });

            var icon = CreateHeaderIcon(accentBrush, targetIcon);
            Grid.SetColumn(icon, 0);
            grid.Children.Add(icon);

            var textPanel = new StackPanel {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(textPanel, 1);
            grid.Children.Add(textPanel);

            var titleBlock = new TextBlock {
                Text = title,
                FontSize = 22,
                Foreground = foregroundBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, -5)
            };
            textPanel.Children.Add(titleBlock);

            valueTextBlock = new TextBlock {
                Text = "0.00°",
                FontSize = 60,
                Foreground = foregroundBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, -5, 0, 0)
            };
            textPanel.Children.Add(valueTextBlock);

            return grid;
        }

        private static Canvas CreateHeaderIcon(Brush accentBrush, bool targetIcon) {
            var canvas = new Canvas {
                Width = 76,
                Height = 76,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            AddIconCircle(canvas, 7, 7, 62, accentBrush, 1.2);

            if (targetIcon) {
                AddIconLine(canvas, 18, 38, 58, 38, accentBrush, 1.4);
                AddIconLine(canvas, 38, 18, 38, 58, accentBrush, 1.4);
                AddIconCircle(canvas, 28, 28, 20, accentBrush, 1.2);
                AddIconCircle(canvas, 35, 35, 6, accentBrush, 1.5);
            } else {
                var body = new Rectangle {
                    Width = 30,
                    Height = 24,
                    RadiusX = 3,
                    RadiusY = 3,
                    Stroke = accentBrush,
                    StrokeThickness = 1.4,
                    Fill = Brushes.Transparent
                };
                Canvas.SetLeft(body, 23);
                Canvas.SetTop(body, 30);
                canvas.Children.Add(body);

                var top = new Rectangle {
                    Width = 13,
                    Height = 5,
                    RadiusX = 2,
                    RadiusY = 2,
                    Stroke = accentBrush,
                    StrokeThickness = 1.2,
                    Fill = Brushes.Transparent
                };
                Canvas.SetLeft(top, 31.5);
                Canvas.SetTop(top, 25);
                canvas.Children.Add(top);

                AddIconCircle(canvas, 33, 36, 10, accentBrush, 1.2);
                AddIconLine(canvas, 21, 22, 15, 16, accentBrush, 1.0);
                AddIconLine(canvas, 55, 22, 61, 16, accentBrush, 1.0);
            }

            return canvas;
        }

        private static void AddIconCircle(Canvas canvas, double left, double top, double size, Brush stroke, double strokeThickness) {
            var circle = new Ellipse {
                Width = size,
                Height = size,
                Stroke = stroke,
                StrokeThickness = strokeThickness,
                Fill = Brushes.Transparent
            };
            Canvas.SetLeft(circle, left);
            Canvas.SetTop(circle, top);
            canvas.Children.Add(circle);
        }

        private static void AddIconLine(Canvas canvas, double x1, double y1, double x2, double y2, Brush stroke, double strokeThickness) {
            canvas.Children.Add(new Line {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                Stroke = stroke,
                StrokeThickness = strokeThickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            });
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

            LayoutDirectionArrows(canvas, centerX, centerY);
            UpdateNeedle(measuredNeedle, ExtractNeedleAngle(measuredNeedle), 84.0);
            UpdateNeedle(targetNeedle, ExtractNeedleAngle(targetNeedle), 74.0);
        }

        private void RenderDiagnosticCanvas() {
            if (diagnosticCanvas == null) {
                return;
            }

            diagnosticCanvas.Children.Clear();
            var width = diagnosticCanvas.ActualWidth;
            var height = diagnosticCanvas.ActualHeight;
            if (width <= 0.0 || height <= 0.0) {
                return;
            }

            var frameWidth = diagnosticFrameWidth > 0 ? diagnosticFrameWidth : 1000;
            var frameHeight = diagnosticFrameHeight > 0 ? diagnosticFrameHeight : 750;
            var padding = 14.0;
            var scale = Math.Min((width - (padding * 2.0)) / frameWidth, (height - (padding * 2.0)) / frameHeight);
            if (scale <= 0.0 || double.IsNaN(scale) || double.IsInfinity(scale)) {
                return;
            }

            var viewWidth = frameWidth * scale;
            var viewHeight = frameHeight * scale;
            var left = (width - viewWidth) / 2.0;
            var top = (height - viewHeight) / 2.0;
            var centerX = left + (viewWidth / 2.0);
            var centerY = top + (viewHeight / 2.0);
            var visualFrameGap = 5.0;
            var outerRadius = Math.Min(viewWidth, viewHeight) / 2.0;
            var innerRatio = Math.Max(0.0, Math.Min(0.80, diagnosticCentralExclusionRatio));
            var innerRadius = outerRadius * innerRatio;
            var frameBrush = diagnosticRejected ? badBrush : Brush(70, 75, 86);

            AddRectangle(diagnosticCanvas, left - visualFrameGap, top - visualFrameGap, viewWidth + (visualFrameGap * 2.0), viewHeight + (visualFrameGap * 2.0), frameBrush, 1.2);
            AddCircle(diagnosticCanvas, centerX, centerY, outerRadius, detectionZoneBrush, 1.2);
            AddCircle(diagnosticCanvas, centerX, centerY, innerRadius, detectionZoneBrush, 1.6);

            if (diagnosticStars == null || diagnosticStars.Count == 0) {
                return;
            }

            for (var i = 0; i < diagnosticStars.Count; i++) {
                var star = diagnosticStars[i];
                var x = left + (star.X * scale);
                var y = top + (star.Y * scale);
                var brush = i < diagnosticMatchedStars && !diagnosticRejected ? reachedBrush : badBrush;
                AddFilledCircle(diagnosticCanvas, x, y, 3.2, brush);
            }
        }

        private static void AddRectangle(Canvas canvas, double left, double top, double width, double height, Brush stroke, double strokeThickness) {
            var rectangle = new Rectangle {
                Width = width,
                Height = height,
                Fill = Brushes.Transparent,
                Stroke = stroke,
                StrokeThickness = strokeThickness
            };
            Canvas.SetLeft(rectangle, left);
            Canvas.SetTop(rectangle, top);
            canvas.Children.Add(rectangle);
        }

        private static void AddCircle(Canvas canvas, double centerX, double centerY, double radius, Brush stroke, double strokeThickness) {
            var circle = new Ellipse {
                Width = radius * 2.0,
                Height = radius * 2.0,
                Fill = Brushes.Transparent,
                Stroke = stroke,
                StrokeThickness = strokeThickness
            };
            Canvas.SetLeft(circle, centerX - radius);
            Canvas.SetTop(circle, centerY - radius);
            canvas.Children.Add(circle);
        }

        private static void AddFilledCircle(Canvas canvas, double centerX, double centerY, double radius, Brush fill) {
            var circle = new Ellipse {
                Width = radius * 2.0,
                Height = radius * 2.0,
                Fill = fill,
                Stroke = Brushes.Transparent
            };
            Canvas.SetLeft(circle, centerX - radius);
            Canvas.SetTop(circle, centerY - radius);
            canvas.Children.Add(circle);
        }

        private Path CreateDirectionArrow() {
            return new Path {
                StrokeThickness = 4,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Opacity = 0.45
            };
        }

        private Polygon CreateDirectionArrowHead() {
            return new Polygon {
                Stroke = Brushes.Transparent,
                Opacity = 1.0
            };
        }

        private void LayoutDirectionArrows(Canvas canvas, double centerX, double centerY) {
            var radius = 68.0;
            clockwiseArrow.Data = CreateArc(centerX + 78.0, centerY + 5.0, radius, -76.0, 38.0, SweepDirection.Clockwise);
            anticlockwiseArrow.Data = CreateArc(centerX - 78.0, centerY + 5.0, radius, -104.0, -218.0, SweepDirection.Counterclockwise);
            SetArrowHead(clockwiseArrowHead, centerX + 78.0, centerY + 5.0, radius, 38.0, 1.0);
            SetArrowHead(anticlockwiseArrowHead, centerX - 78.0, centerY + 5.0, radius, -218.0, -1.0);
        }

        private void UpdateDirectionArrows(double delta, bool targetReached) {
            var activeBrush = targetReached ? reachedBrush : measuredBrush;
            var inactiveBrush = mutedBrush;
            var clockwiseActive = delta >= 0.0;
            clockwiseArrow.Stroke = clockwiseActive ? activeBrush : inactiveBrush;
            clockwiseArrow.Opacity = 1.0;
            clockwiseArrowHead.Fill = clockwiseActive ? activeBrush : inactiveBrush;
            clockwiseArrowHead.Opacity = 1.0;
            anticlockwiseArrow.Stroke = clockwiseActive ? inactiveBrush : activeBrush;
            anticlockwiseArrow.Opacity = 1.0;
            anticlockwiseArrowHead.Fill = clockwiseActive ? inactiveBrush : activeBrush;
            anticlockwiseArrowHead.Opacity = 1.0;
        }

        private static PathGeometry CreateArc(double centerX, double centerY, double radius, double startAngle, double endAngle, SweepDirection sweepDirection) {
            var start = PointOnCircle(centerX, centerY, radius, startAngle);
            var end = PointOnCircle(centerX, centerY, radius, endAngle);
            var segment = new ArcSegment {
                Point = end,
                Size = new Size(radius, radius),
                SweepDirection = sweepDirection,
                IsLargeArc = Math.Abs(endAngle - startAngle) > 180.0
            };
            var figure = new PathFigure {
                StartPoint = start,
                IsClosed = false
            };
            figure.Segments.Add(segment);
            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            return geometry;
        }

        private static void SetArrowHead(Polygon arrowHead, double centerX, double centerY, double radius, double angle, double direction) {
            var tip = PointOnCircle(centerX, centerY, radius, angle);
            var tangent = (angle + (direction * 90.0)) * Math.PI / 180.0;
            var normal = tangent + (Math.PI / 2.0);
            var length = 18.0;
            var width = 9.0;
            var tipOvershoot = 8.0;
            var visibleTip = new Point(tip.X + (Math.Cos(tangent) * tipOvershoot), tip.Y + (Math.Sin(tangent) * tipOvershoot));
            var baseCenter = new Point(tip.X - (Math.Cos(tangent) * length), tip.Y - (Math.Sin(tangent) * length));
            arrowHead.Points = new PointCollection {
                visibleTip,
                new Point(baseCenter.X + (Math.Cos(normal) * width), baseCenter.Y + (Math.Sin(normal) * width)),
                new Point(baseCenter.X - (Math.Cos(normal) * width), baseCenter.Y - (Math.Sin(normal) * width))
            };
        }

        private static Point PointOnCircle(double centerX, double centerY, double radius, double angleDegrees) {
            var radians = angleDegrees * Math.PI / 180.0;
            return new Point(centerX + (Math.Cos(radians) * radius), centerY + (Math.Sin(radians) * radius));
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

            if (Math.Abs(angle) < 0.0005 || Math.Abs(angle - 360.0) < 0.0005) {
                return 0.0;
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
