using ManualImageRotator.NINA.Imaging;
using ManualImageRotator.NINA.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ManualImageRotator.Harness {
    internal static class Program {
        private const int Width = 1024;
        private const int Height = 768;

        private static int Main(string[] args) {
            try {
                var options = HarnessOptions.Parse(args);
                if (options.ShowHelp) {
                    PrintUsage();
                    return 0;
                }

                if (options.HasImageInputs) {
                    RunImageFileTest(options.ReferencePath, options.CurrentPath, options.ExpectedAngle);
                    Console.WriteLine("OK - image file test completed.");
                    return 0;
                }

                var testAngle = options.ExpectedAngle ?? 12.5;
                RunEstimatorTest(testAngle);
                RunTranslatedEstimatorTest(testAngle);
                RunSessionTestAsync().GetAwaiter().GetResult();
                Console.WriteLine("OK - harness completed.");
                return 0;
            } catch (Exception ex) {
                Console.Error.WriteLine("FAILED - " + ex.Message);
                return 1;
            }
        }

        private static void PrintUsage() {
            Console.WriteLine("Usage:");
            Console.WriteLine("  ManualImageRotator.Harness.exe");
            Console.WriteLine("  ManualImageRotator.Harness.exe 12.5");
            Console.WriteLine("  ManualImageRotator.Harness.exe --reference starfield.png --current starfield_rotated.png --expected -12.5");
            Console.WriteLine();
            Console.WriteLine("Note: Pillow's positive image rotation is usually reported as a negative angle by this estimator.");
        }

        private static void RunImageFileTest(string referencePath, string currentPath, double? expectedAngle) {
            var reference = LoadImage(referencePath);
            var current = LoadImage(currentPath);

            if (reference.Width != current.Width || reference.Height != current.Height) {
                throw new InvalidOperationException("Reference and current images must have the same dimensions.");
            }

            var estimator = new RotationEstimator(new StarCentroidDetector());
            var measurement = estimator.Measure(reference, current);

            Console.WriteLine("Image file test");
            Console.WriteLine("  reference      : {0}", referencePath);
            Console.WriteLine("  current        : {0}", currentPath);
            if (expectedAngle.HasValue) {
                var error = NormalizeSigned(measurement.AngleDegrees - expectedAngle.Value);
                Console.WriteLine("  expected angle : {0:0.000} deg", expectedAngle.Value);
                Console.WriteLine("  measured angle : {0:0.000} deg", measurement.AngleDegrees);
                Console.WriteLine("  error          : {0:0.000} deg", error);

                if (Math.Abs(error) > 1.0) {
                    throw new InvalidOperationException("Image file estimator error is too high.");
                }
            } else {
                Console.WriteLine("  measured angle : {0:0.000} deg", measurement.AngleDegrees);
            }

            Console.WriteLine("  matched stars  : {0}", measurement.MatchedStars);
            Console.WriteLine("  RMS            : {0:0.000} px", measurement.RmsPixels);
            Console.WriteLine("  quality        : {0:0.000}", measurement.Quality);

            if (measurement.MatchedStars < 3 || measurement.Quality < 0.10) {
                throw new InvalidOperationException("Image file estimator quality is too low.");
            }
        }

        private static RotationFrame LoadImage(string path) {
            if (!File.Exists(path)) {
                throw new FileNotFoundException("Image not found.", path);
            }

            using (var bitmap = new Bitmap(path)) {
                var pixels = new ushort[bitmap.Width * bitmap.Height];

                for (var y = 0; y < bitmap.Height; y++) {
                    for (var x = 0; x < bitmap.Width; x++) {
                        var color = bitmap.GetPixel(x, y);
                        var luminance = (0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B);
                        pixels[(y * bitmap.Width) + x] = (ushort)Math.Round(luminance * 257.0);
                    }
                }

                return new RotationFrame(bitmap.Width, bitmap.Height, pixels);
            }
        }

        private static void RunEstimatorTest(double expectedAngle) {
            var field = SyntheticStarField.Create(Width, Height);
            var reference = field.Render(0.0);
            var current = field.Render(expectedAngle);
            var estimator = new RotationEstimator(new StarCentroidDetector());
            var measurement = estimator.Measure(reference, current);
            var error = NormalizeSigned(measurement.AngleDegrees - expectedAngle);

            Console.WriteLine("Estimator test");
            Console.WriteLine("  expected angle : {0:0.000} deg", expectedAngle);
            Console.WriteLine("  measured angle : {0:0.000} deg", measurement.AngleDegrees);
            Console.WriteLine("  error          : {0:0.000} deg", error);
            Console.WriteLine("  matched stars  : {0}", measurement.MatchedStars);
            Console.WriteLine("  RMS            : {0:0.000} px", measurement.RmsPixels);
            Console.WriteLine("  quality        : {0:0.000}", measurement.Quality);

            if (Math.Abs(error) > 0.35) {
                throw new InvalidOperationException("Estimator error is too high.");
            }

            if (measurement.MatchedStars < 8 || measurement.Quality < 0.35) {
                throw new InvalidOperationException("Estimator quality is too low.");
            }
        }

        private static void RunTranslatedEstimatorTest(double expectedAngle) {
            var field = SyntheticStarField.Create(Width, Height);
            var reference = field.Render(0.0);
            var current = field.Render(expectedAngle, 28.0, -17.0);
            var estimator = new RotationEstimator(new StarCentroidDetector());
            var measurement = estimator.Measure(reference, current);
            var error = NormalizeSigned(measurement.AngleDegrees - expectedAngle);

            Console.WriteLine("Translated estimator test");
            Console.WriteLine("  expected angle : {0:0.000} deg", expectedAngle);
            Console.WriteLine("  measured angle : {0:0.000} deg", measurement.AngleDegrees);
            Console.WriteLine("  error          : {0:0.000} deg", error);
            Console.WriteLine("  matched stars  : {0}", measurement.MatchedStars);
            Console.WriteLine("  RMS            : {0:0.000} px", measurement.RmsPixels);
            Console.WriteLine("  quality        : {0:0.000}", measurement.Quality);
            Console.WriteLine("  translation    : {0:0.0}, {1:0.0} px", measurement.TranslationX, measurement.TranslationY);

            if (Math.Abs(error) > 0.35) {
                throw new InvalidOperationException("Translated estimator error is too high.");
            }

            if (measurement.MatchedStars < 8 || measurement.Quality < 0.35) {
                throw new InvalidOperationException("Translated estimator quality is too low.");
            }
        }

        private static async Task RunSessionTestAsync() {
            var field = SyntheticStarField.Create(Width, Height);
            var frames = new[] {
                field.Render(0.0),
                field.Render(15.0),
                field.Render(30.0),
                field.Render(44.9)
            };

            var source = new SequenceImageSource(frames);
            var estimator = new RotationEstimator(new StarCentroidDetector());
            var session = new ManualRotationSession(source, estimator);
            ManualRotationState lastState = null;

            session.StateChanged += (sender, state) => {
                lastState = state;
                Console.WriteLine(
                    "  state: angle={0:0.000} target={1:0.000} delta={2:0.000} status={3}",
                    state.CurrentAngle,
                    state.TargetAngle,
                    state.Delta,
                    state.Status);
            };

            Console.WriteLine("Session test");
            var result = await session.RunAsync(new ManualRotationOptions {
                InitialAngle = 0.0,
                TargetAngle = 45.0,
                ToleranceDegrees = 0.25,
                ExposureSeconds = 0.01,
                MinimumQuality = 0.35,
                RefreshInterval = TimeSpan.FromMilliseconds(1)
            }, CancellationToken.None);

            if (!result.TargetReached || result.Cancelled) {
                throw new InvalidOperationException("Session did not reach the target.");
            }

            if (lastState == null || Math.Abs(NormalizeSigned(lastState.CurrentAngle - 45.0)) > 0.35) {
                throw new InvalidOperationException("Session final angle is not close to target.");
            }
        }

        private static double NormalizeSigned(double angle) {
            angle = (angle + 180.0) % 360.0;
            if (angle < 0.0) {
                angle += 360.0;
            }

            return angle - 180.0;
        }

        private sealed class SequenceImageSource : IRotationImageSource {
            private readonly IReadOnlyList<RotationFrame> frames;
            private int index;

            public SequenceImageSource(IReadOnlyList<RotationFrame> frames) {
                this.frames = frames;
            }

            public Task<RotationFrame> CaptureAsync(double exposureSeconds, CancellationToken token) {
                token.ThrowIfCancellationRequested();
                var frame = frames[Math.Min(index, frames.Count - 1)];
                index++;
                return Task.FromResult(frame);
            }
        }

        private sealed class SyntheticStarField {
            private readonly int width;
            private readonly int height;
            private readonly IReadOnlyList<SyntheticStar> stars;

            private SyntheticStarField(int width, int height, IReadOnlyList<SyntheticStar> stars) {
                this.width = width;
                this.height = height;
                this.stars = stars;
            }

            public static SyntheticStarField Create(int width, int height) {
                var random = new Random(12345);
                var centerX = width / 2.0;
                var centerY = height / 2.0;
                var stars = new List<SyntheticStar>();

                for (var i = 0; i < 24; i++) {
                    var radius = 70.0 + (i * 10.5);
                    var angle = random.NextDouble() * Math.PI * 2.0;
                    stars.Add(new SyntheticStar {
                        X = centerX + (Math.Cos(angle) * radius),
                        Y = centerY + (Math.Sin(angle) * radius),
                        Flux = 42000 + (i * 700)
                    });
                }

                return new SyntheticStarField(width, height, stars);
            }

            public RotationFrame Render(double rotationDegrees) {
                return Render(rotationDegrees, 0.0, 0.0);
            }

            public RotationFrame Render(double rotationDegrees, double translateX, double translateY) {
                var pixels = new ushort[width * height];
                for (var i = 0; i < pixels.Length; i++) {
                    pixels[i] = 1000;
                }

                var centerX = width / 2.0;
                var centerY = height / 2.0;
                var radians = rotationDegrees * Math.PI / 180.0;
                var cos = Math.Cos(radians);
                var sin = Math.Sin(radians);

                foreach (var star in stars) {
                    var dx = star.X - centerX;
                    var dy = star.Y - centerY;
                    var x = centerX + (dx * cos) - (dy * sin) + translateX;
                    var y = centerY + (dx * sin) + (dy * cos) + translateY;
                    DrawStar(pixels, x, y, star.Flux);
                }

                return new RotationFrame(width, height, pixels);
            }

            private void DrawStar(ushort[] pixels, double cx, double cy, double flux) {
                for (var y = (int)Math.Floor(cy) - 3; y <= (int)Math.Floor(cy) + 3; y++) {
                    if (y < 0 || y >= height) {
                        continue;
                    }

                    for (var x = (int)Math.Floor(cx) - 3; x <= (int)Math.Floor(cx) + 3; x++) {
                        if (x < 0 || x >= width) {
                            continue;
                        }

                        var dx = x - cx;
                        var dy = y - cy;
                        var signal = flux * Math.Exp(-((dx * dx) + (dy * dy)) / 2.0);
                        var index = (y * width) + x;
                        var value = Math.Min(65535.0, pixels[index] + signal);
                        pixels[index] = (ushort)value;
                    }
                }
            }
        }

        private struct SyntheticStar {
            public double X { get; set; }
            public double Y { get; set; }
            public double Flux { get; set; }
        }

        private sealed class HarnessOptions {
            public string ReferencePath { get; private set; }
            public string CurrentPath { get; private set; }
            public double? ExpectedAngle { get; private set; }
            public bool ShowHelp { get; private set; }

            public bool HasImageInputs {
                get { return !string.IsNullOrWhiteSpace(ReferencePath) || !string.IsNullOrWhiteSpace(CurrentPath); }
            }

            public static HarnessOptions Parse(string[] args) {
                var options = new HarnessOptions();

                if (args.Length == 1 && !args[0].StartsWith("--", StringComparison.Ordinal)) {
                    options.ExpectedAngle = double.Parse(args[0], CultureInfo.InvariantCulture);
                    return options;
                }

                for (var i = 0; i < args.Length; i++) {
                    switch (args[i]) {
                        case "--help":
                        case "-h":
                            options.ShowHelp = true;
                            break;
                        case "--reference":
                            options.ReferencePath = RequireValue(args, ref i, "--reference");
                            break;
                        case "--current":
                            options.CurrentPath = RequireValue(args, ref i, "--current");
                            break;
                        case "--expected":
                            options.ExpectedAngle = double.Parse(RequireValue(args, ref i, "--expected"), CultureInfo.InvariantCulture);
                            break;
                        default:
                            throw new ArgumentException("Unknown argument: " + args[i]);
                    }
                }

                if (options.HasImageInputs && (string.IsNullOrWhiteSpace(options.ReferencePath) || string.IsNullOrWhiteSpace(options.CurrentPath))) {
                    throw new ArgumentException("--reference and --current must be provided together.");
                }

                return options;
            }

            private static string RequireValue(string[] args, ref int index, string name) {
                if (index + 1 >= args.Length) {
                    throw new ArgumentException(name + " requires a value.");
                }

                index++;
                return args[index];
            }
        }
    }
}
