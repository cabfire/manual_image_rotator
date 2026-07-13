using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ManualImageRotator.NINA.Equipment {
    public sealed class ManualImageRotatorSetupWindow : Window {
        private static readonly Brush BackgroundBrush = new SolidColorBrush(Color.FromRgb(30, 33, 38));
        private static readonly Brush PanelBrush = new SolidColorBrush(Color.FromRgb(39, 43, 49));
        private static readonly Brush ForegroundBrush = new SolidColorBrush(Color.FromRgb(238, 241, 246));
        private static readonly Brush MutedForegroundBrush = new SolidColorBrush(Color.FromRgb(190, 198, 210));
        private static readonly Brush BorderBrushColor = new SolidColorBrush(Color.FromRgb(91, 99, 113));

        private readonly TextBox exposureSecondsTextBox;
        private readonly TextBox refreshIntervalSecondsTextBox;
        private readonly TextBox toleranceDegreesTextBox;
        private readonly TextBox centralExclusionPercentTextBox;
        private readonly TextBox detectedStarsTextBox;
        private readonly TextBox minimumQualityTextBox;
        private readonly TextBox minimumMatchedStarsTextBox;
        private readonly TextBox maximumAngleJumpDegreesTextBox;
        private readonly CheckBox debugLoggingCheckBox;
        private readonly ManualImageRotatorSettings settings;
        private readonly Action reinitCurrentPosition;

        public ManualImageRotatorSetupWindow(ManualImageRotatorSettings settings)
            : this(settings, null) {
        }

        public ManualImageRotatorSetupWindow(ManualImageRotatorSettings settings, Action reinitCurrentPosition) {
            this.settings = settings;
            this.reinitCurrentPosition = reinitCurrentPosition;

            Title = "Manual Image Rotator";
            Width = 430;
            Height = 560;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = BackgroundBrush;
            Foreground = ForegroundBrush;

            var root = new Grid {
                Margin = new Thickness(18),
                Background = BackgroundBrush
            };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            exposureSecondsTextBox = AddField(
                root,
                0,
                "Exposure seconds",
                settings.ExposureSeconds.ToString(CultureInfo.InvariantCulture));

            refreshIntervalSecondsTextBox = AddField(
                root,
                1,
                "Refresh interval seconds",
                settings.RefreshIntervalSeconds.ToString(CultureInfo.InvariantCulture));

            toleranceDegreesTextBox = AddField(
                root,
                2,
                "Tolerance degrees",
                settings.ToleranceDegrees.ToString(CultureInfo.InvariantCulture));

            centralExclusionPercentTextBox = AddField(
                root,
                3,
                "Central exclusion %",
                settings.CentralExclusionPercent.ToString(CultureInfo.InvariantCulture));

            detectedStarsTextBox = AddField(
                root,
                4,
                "Detected stars",
                settings.DetectedStars.ToString(CultureInfo.InvariantCulture));

            minimumQualityTextBox = AddField(
                root,
                5,
                "Minimum quality",
                settings.MinimumQuality.ToString(CultureInfo.InvariantCulture));

            minimumMatchedStarsTextBox = AddField(
                root,
                6,
                "Minimum matched stars",
                settings.MinimumMatchedStars.ToString(CultureInfo.InvariantCulture));

            maximumAngleJumpDegreesTextBox = AddField(
                root,
                7,
                "Maximum angle jump degrees",
                settings.MaximumAngleJumpDegrees.ToString(CultureInfo.InvariantCulture));

            debugLoggingCheckBox = AddCheckBox(
                root,
                8,
                "Debug logging",
                settings.DebugLogging);

            var reinitButton = new Button {
                Content = "Reinit current position",
                Height = 34,
                Margin = new Thickness(0, 8, 0, 8),
                Foreground = ForegroundBrush,
                Background = PanelBrush,
                BorderBrush = BorderBrushColor,
                Padding = new Thickness(8, 4, 8, 4)
            };
            reinitButton.Click += OnReinitCurrentPosition;
            Grid.SetRow(reinitButton, 9);
            Grid.SetColumnSpan(reinitButton, 2);
            root.Children.Add(reinitButton);

            var buttons = new StackPanel {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetRow(buttons, 11);
            Grid.SetColumnSpan(buttons, 2);

            var okButton = new Button {
                Content = "OK",
                Width = 80,
                Margin = new Thickness(0, 12, 8, 0),
                IsDefault = true,
                Foreground = ForegroundBrush,
                Background = PanelBrush,
                BorderBrush = BorderBrushColor,
                Padding = new Thickness(8, 4, 8, 4)
            };
            okButton.Click += OnOk;

            var cancelButton = new Button {
                Content = "Cancel",
                Width = 80,
                Margin = new Thickness(0, 12, 0, 0),
                IsCancel = true,
                Foreground = ForegroundBrush,
                Background = PanelBrush,
                BorderBrush = BorderBrushColor,
                Padding = new Thickness(8, 4, 8, 4)
            };

            buttons.Children.Add(okButton);
            buttons.Children.Add(cancelButton);
            root.Children.Add(buttons);

            Content = root;
        }

        private static TextBox AddField(Grid root, int row, string label, string value) {
            var textBlock = new TextBlock {
                Text = label,
                Foreground = MutedForegroundBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 8)
            };
            Grid.SetRow(textBlock, row);
            Grid.SetColumn(textBlock, 0);
            root.Children.Add(textBlock);

            var textBox = new TextBox {
                Text = value,
                Foreground = ForegroundBrush,
                Background = PanelBrush,
                BorderBrush = BorderBrushColor,
                CaretBrush = ForegroundBrush,
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(textBox, row);
            Grid.SetColumn(textBox, 1);
            root.Children.Add(textBox);

            return textBox;
        }

        private static CheckBox AddCheckBox(Grid root, int row, string label, bool value) {
            var textBlock = new TextBlock {
                Text = label,
                Foreground = MutedForegroundBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 8)
            };
            Grid.SetRow(textBlock, row);
            Grid.SetColumn(textBlock, 0);
            root.Children.Add(textBlock);

            var checkBox = new CheckBox {
                Content = "Enabled",
                IsChecked = value,
                Foreground = ForegroundBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(checkBox, row);
            Grid.SetColumn(checkBox, 1);
            root.Children.Add(checkBox);

            return checkBox;
        }

        private void OnOk(object sender, RoutedEventArgs e) {
            if (!TryParsePositive(exposureSecondsTextBox.Text, out var exposureSeconds)) {
                MessageBox.Show(this, "Exposure seconds must be a positive number.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                exposureSecondsTextBox.Focus();
                return;
            }

            if (!TryParsePositive(refreshIntervalSecondsTextBox.Text, out var refreshIntervalSeconds)) {
                MessageBox.Show(this, "Refresh interval seconds must be a positive number.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                refreshIntervalSecondsTextBox.Focus();
                return;
            }

            if (!TryParsePositive(toleranceDegreesTextBox.Text, out var toleranceDegrees)) {
                MessageBox.Show(this, "Tolerance degrees must be a positive number.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                toleranceDegreesTextBox.Focus();
                return;
            }

            if (!TryParsePercent(centralExclusionPercentTextBox.Text, out var centralExclusionPercent)) {
                MessageBox.Show(this, "Central exclusion must be a number between 0 and 80.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                centralExclusionPercentTextBox.Focus();
                return;
            }

            if (!TryParseDetectedStars(detectedStarsTextBox.Text, out var detectedStars)) {
                MessageBox.Show(this, "Detected stars must be an integer between 3 and 100.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                detectedStarsTextBox.Focus();
                return;
            }

            if (!TryParseQuality(minimumQualityTextBox.Text, out var minimumQuality)) {
                MessageBox.Show(this, "Minimum quality must be a number between 0 and 1.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                minimumQualityTextBox.Focus();
                return;
            }

            if (!TryParseDetectedStars(minimumMatchedStarsTextBox.Text, out var minimumMatchedStars)) {
                MessageBox.Show(this, "Minimum matched stars must be an integer between 3 and 100.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                minimumMatchedStarsTextBox.Focus();
                return;
            }

            if (!TryParseAngleJump(maximumAngleJumpDegreesTextBox.Text, out var maximumAngleJumpDegrees)) {
                MessageBox.Show(this, "Maximum angle jump degrees must be a number between 1 and 180.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                maximumAngleJumpDegreesTextBox.Focus();
                return;
            }

            settings.ExposureSeconds = exposureSeconds;
            settings.RefreshIntervalSeconds = refreshIntervalSeconds;
            settings.ToleranceDegrees = toleranceDegrees;
            settings.CentralExclusionPercent = centralExclusionPercent;
            settings.DetectedStars = detectedStars;
            settings.MinimumQuality = minimumQuality;
            settings.MinimumMatchedStars = minimumMatchedStars;
            settings.MaximumAngleJumpDegrees = maximumAngleJumpDegrees;
            settings.DebugLogging = debugLoggingCheckBox.IsChecked == true;
            settings.Save();
            DialogResult = true;
            Close();
        }

        private static bool TryParsePositive(string text, out double value) {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && value > 0;
        }

        private static bool TryParsePercent(string text, out double value) {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && value >= 0
                && value <= 80;
        }

        private static bool TryParseDetectedStars(string text, out int value) {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                && value >= 3
                && value <= 100;
        }

        private static bool TryParseQuality(string text, out double value) {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && value >= 0
                && value <= 1;
        }

        private static bool TryParseAngleJump(string text, out double value) {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && value >= 1
                && value <= 180;
        }

        private void OnReinitCurrentPosition(object sender, RoutedEventArgs e) {
            var result = MessageBox.Show(
                this,
                "Set the current rotator position to 0 deg?",
                Title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) {
                return;
            }

            reinitCurrentPosition?.Invoke();
        }
    }
}
