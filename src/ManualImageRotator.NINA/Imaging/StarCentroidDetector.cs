using System;
using System.Collections.Generic;
using System.Linq;

namespace ManualImageRotator.NINA.Imaging {
    public sealed class StarCentroidDetector {
        private const int DefaultMaxStars = 16;
        private const double DefaultCentralExclusionRatio = 0.20;
        private const double MinimumStarSeparationPixels = 12.0;

        private readonly Func<int> maxStarsProvider;
        private readonly Func<double> centralExclusionRatioProvider;

        public StarCentroidDetector()
            : this(null, null) {
        }

        public StarCentroidDetector(Func<int> maxStarsProvider, Func<double> centralExclusionRatioProvider) {
            this.maxStarsProvider = maxStarsProvider;
            this.centralExclusionRatioProvider = centralExclusionRatioProvider;
        }

        public int MaxStars { get; set; } = DefaultMaxStars;
        public double CentralExclusionRatio { get; set; } = DefaultCentralExclusionRatio;

        public IReadOnlyList<StarCentroid> Detect(RotationFrame frame, int maxStars = 0) {
            maxStars = Math.Max(3, maxStars > 0 ? maxStars : EffectiveMaxStars);
            var stats = EstimateBackground(frame.Pixels);
            var threshold = stats.mean + (4.0 * stats.sigma);
            var stars = new List<StarCentroid>();
            var centerX = (frame.Width - 1) / 2.0;
            var centerY = (frame.Height - 1) / 2.0;
            var outerRadius = Math.Min(frame.Width, frame.Height) / 2.0;
            var innerRatio = Math.Max(0.0, Math.Min(0.80, EffectiveCentralExclusionRatio));
            var innerRadius = outerRadius * innerRatio;
            var outerRadiusSquared = outerRadius * outerRadius;
            var innerRadiusSquared = innerRadius * innerRadius;

            for (var y = 2; y < frame.Height - 2; y++) {
                for (var x = 2; x < frame.Width - 2; x++) {
                    var value = Pixel(frame, x, y);
                    if (value < threshold || !IsLocalMaximum(frame, x, y, value)) {
                        continue;
                    }

                    var star = Centroid(frame, x, y, stats.mean);
                    if (IsInsideCentralAnnulus(star, centerX, centerY, innerRadiusSquared, outerRadiusSquared)) {
                        stars.Add(star);
                    }
                }
            }

            return SelectSeparatedBrightestStars(stars, maxStars);
        }

        private int EffectiveMaxStars => maxStarsProvider?.Invoke() ?? MaxStars;

        private double EffectiveCentralExclusionRatio => centralExclusionRatioProvider?.Invoke() ?? CentralExclusionRatio;

        private static IReadOnlyList<StarCentroid> SelectSeparatedBrightestStars(
            IEnumerable<StarCentroid> stars,
            int maxStars) {
            var selected = new List<StarCentroid>();
            var minimumDistanceSquared = MinimumStarSeparationPixels * MinimumStarSeparationPixels;

            foreach (var star in stars.OrderByDescending(s => s.Flux)) {
                var tooClose = false;
                foreach (var existing in selected) {
                    var dx = star.X - existing.X;
                    var dy = star.Y - existing.Y;
                    if (((dx * dx) + (dy * dy)) < minimumDistanceSquared) {
                        tooClose = true;
                        break;
                    }
                }

                if (tooClose) {
                    continue;
                }

                selected.Add(star);
                if (selected.Count >= maxStars) {
                    break;
                }
            }

            return selected;
        }

        private static bool IsInsideCentralAnnulus(
            StarCentroid star,
            double centerX,
            double centerY,
            double innerRadiusSquared,
            double outerRadiusSquared) {
            var dx = star.X - centerX;
            var dy = star.Y - centerY;
            var distanceSquared = (dx * dx) + (dy * dy);
            return distanceSquared >= innerRadiusSquared && distanceSquared <= outerRadiusSquared;
        }

        private static (double mean, double sigma) EstimateBackground(ushort[] pixels) {
            var step = Math.Max(1, pixels.Length / 10000);
            var samples = new List<double>();

            for (var i = 0; i < pixels.Length; i += step) {
                samples.Add(pixels[i]);
            }

            var mean = samples.Average();
            var variance = samples.Select(v => (v - mean) * (v - mean)).Average();
            return (mean, Math.Sqrt(variance));
        }

        private static bool IsLocalMaximum(RotationFrame frame, int cx, int cy, ushort value) {
            for (var y = cy - 1; y <= cy + 1; y++) {
                for (var x = cx - 1; x <= cx + 1; x++) {
                    if ((x != cx || y != cy) && Pixel(frame, x, y) >= value) {
                        return false;
                    }
                }
            }

            return true;
        }

        private static StarCentroid Centroid(RotationFrame frame, int cx, int cy, double background) {
            double sum = 0;
            double sx = 0;
            double sy = 0;

            for (var y = cy - 2; y <= cy + 2; y++) {
                for (var x = cx - 2; x <= cx + 2; x++) {
                    var signal = Math.Max(0.0, Pixel(frame, x, y) - background);
                    sum += signal;
                    sx += x * signal;
                    sy += y * signal;
                }
            }

            if (sum <= 0) {
                return new StarCentroid { X = cx, Y = cy, Flux = 0 };
            }

            return new StarCentroid {
                X = sx / sum,
                Y = sy / sum,
                Flux = sum
            };
        }

        private static ushort Pixel(RotationFrame frame, int x, int y) {
            return frame.Pixels[(y * frame.Width) + x];
        }
    }
}
