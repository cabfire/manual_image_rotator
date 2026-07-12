using System;
using System.Globalization;
using System.IO;

namespace ManualImageRotator.NINA.Equipment {
    public sealed class ManualImageRotatorSettings {
        private const double DefaultExposureSeconds = 0.05;
        private const double DefaultRefreshIntervalSeconds = 1.0;
        private const double DefaultToleranceDegrees = 0.25;
        private const double DefaultCentralExclusionPercent = 20.0;
        private const int DefaultDetectedStars = 16;

        public double ExposureSeconds { get; set; } = DefaultExposureSeconds;
        public double RefreshIntervalSeconds { get; set; } = DefaultRefreshIntervalSeconds;
        public double ToleranceDegrees { get; set; } = DefaultToleranceDegrees;
        public double CentralExclusionPercent { get; set; } = DefaultCentralExclusionPercent;
        public int DetectedStars { get; set; } = DefaultDetectedStars;
        public bool Reverse { get; set; }
        public bool DebugLogging { get; set; }

        public TimeSpan RefreshInterval => TimeSpan.FromSeconds(RefreshIntervalSeconds);

        public static ManualImageRotatorSettings Load() {
            var settings = new ManualImageRotatorSettings();
            var path = SettingsPath;

            if (!File.Exists(path)) {
                return settings;
            }

            foreach (var line in File.ReadAllLines(path)) {
                var parts = line.Split(new[] { '=' }, 2);
                if (parts.Length != 2) {
                    continue;
                }

                if (parts[0] == nameof(ExposureSeconds)) {
                    if (double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) {
                        settings.ExposureSeconds = value;
                    }
                } else if (parts[0] == nameof(RefreshIntervalSeconds)) {
                    if (double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) {
                        settings.RefreshIntervalSeconds = value;
                    }
                } else if (parts[0] == nameof(ToleranceDegrees)) {
                    if (double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) {
                        settings.ToleranceDegrees = value;
                    }
                } else if (parts[0] == nameof(CentralExclusionPercent)) {
                    if (double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) {
                        settings.CentralExclusionPercent = value;
                    }
                } else if (parts[0] == nameof(DetectedStars)) {
                    if (int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) {
                        settings.DetectedStars = value;
                    }
                } else if (parts[0] == nameof(Reverse)) {
                    if (bool.TryParse(parts[1], out var value)) {
                        settings.Reverse = value;
                    }
                } else if (parts[0] == nameof(DebugLogging)) {
                    if (bool.TryParse(parts[1], out var value)) {
                        settings.DebugLogging = value;
                    }
                }
            }

            settings.Validate();
            return settings;
        }

        public void Save() {
            Validate();
            Directory.CreateDirectory(SettingsDirectory);
            File.WriteAllLines(SettingsPath, new[] {
                $"{nameof(ExposureSeconds)}={ExposureSeconds.ToString(CultureInfo.InvariantCulture)}",
                $"{nameof(RefreshIntervalSeconds)}={RefreshIntervalSeconds.ToString(CultureInfo.InvariantCulture)}",
                $"{nameof(ToleranceDegrees)}={ToleranceDegrees.ToString(CultureInfo.InvariantCulture)}",
                $"{nameof(CentralExclusionPercent)}={CentralExclusionPercent.ToString(CultureInfo.InvariantCulture)}",
                $"{nameof(DetectedStars)}={DetectedStars.ToString(CultureInfo.InvariantCulture)}",
                $"{nameof(Reverse)}={Reverse}",
                $"{nameof(DebugLogging)}={DebugLogging}"
            });
        }

        public void Validate() {
            ExposureSeconds = Clamp(ExposureSeconds, 0.001, 600.0);
            RefreshIntervalSeconds = Clamp(RefreshIntervalSeconds, 0.1, 600.0);
            ToleranceDegrees = Clamp(ToleranceDegrees, 0.01, 10.0);
            CentralExclusionPercent = Clamp(CentralExclusionPercent, 0.0, 80.0);
            DetectedStars = Math.Max(3, Math.Min(100, DetectedStars));
        }

        private static double Clamp(double value, double min, double max) {
            if (double.IsNaN(value) || double.IsInfinity(value)) {
                return min;
            }

            return Math.Max(min, Math.Min(max, value));
        }

        private static string SettingsDirectory {
            get {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "NINA",
                    "ManualImageRotator");
            }
        }

        private static string SettingsPath => Path.Combine(SettingsDirectory, "settings.txt");
    }
}
