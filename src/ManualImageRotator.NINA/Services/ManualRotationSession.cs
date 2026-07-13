using ManualImageRotator.NINA.Imaging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ManualImageRotator.NINA.Services {
    public interface IManualRotationLogger {
        void Info(string message);
        void Warning(string message);
        void Error(string message, Exception exception);
    }

    public sealed class NullManualRotationLogger : IManualRotationLogger {
        public static readonly NullManualRotationLogger Instance = new NullManualRotationLogger();

        private NullManualRotationLogger() {
        }

        public void Info(string message) {
        }

        public void Warning(string message) {
        }

        public void Error(string message, Exception exception) {
        }
    }

    public sealed class ManualRotationSession {
        private readonly IRotationImageSource imageSource;
        private readonly RotationEstimator estimator;
        private readonly IManualRotationLogger logger;
        private CancellationTokenSource cancelSource;

        public ManualRotationSession(IRotationImageSource imageSource, RotationEstimator estimator)
            : this(imageSource, estimator, NullManualRotationLogger.Instance) {
        }

        public ManualRotationSession(
            IRotationImageSource imageSource,
            RotationEstimator estimator,
            IManualRotationLogger logger) {
            this.imageSource = imageSource;
            this.estimator = estimator;
            this.logger = logger ?? NullManualRotationLogger.Instance;
        }

        public event EventHandler<ManualRotationState> StateChanged;

        public async Task<ManualRotationResult> RunAsync(ManualRotationOptions options, CancellationToken token) {
            cancelSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            var ct = cancelSource.Token;
            var currentAngle = options.InitialAngle;

            try {
                logger.Info(
                    $"Session start target={options.TargetAngle:F3} initial={options.InitialAngle:F3} " +
                    $"tolerance={options.ToleranceDegrees:F3} exposure={options.ExposureSeconds:F3}s " +
                    $"refresh={options.RefreshInterval.TotalSeconds:F3}s minQuality={options.MinimumQuality:F3} " +
                    $"minMatched={options.MinimumMatchedStars} maxJump={options.MaximumAngleJumpDegrees:F3}");

                var reference = await imageSource.CaptureAsync(options.ExposureSeconds, ct);
                logger.Info($"Reference frame captured width={reference.Width} height={reference.Height}");

                while (!ct.IsCancellationRequested) {
                    var current = await imageSource.CaptureAsync(options.ExposureSeconds, ct);
                    var measurement = estimator.Measure(reference, current);
                    var measuredAngle = Normalize360(options.InitialAngle + measurement.AngleDegrees);
                    var angleJump = Math.Abs(NormalizeSigned(measuredAngle - currentAngle));

                    logger.Info(
                        $"Measurement angle={measurement.AngleDegrees:F3} position={currentAngle:F3} " +
                        $"delta={NormalizeSigned(options.TargetAngle - currentAngle):F3} " +
                        $"matched={measurement.MatchedStars} rms={measurement.RmsPixels:F3} " +
                        $"quality={measurement.Quality:F3} tx={measurement.TranslationX:F1} " +
                        $"ty={measurement.TranslationY:F1} scale={measurement.Scale:F4} refStars={Count(measurement.ReferenceStars)} " +
                        $"curStars={Count(measurement.CurrentStars)} jump={angleJump:F3}");

                    var rejectionReason = GetRejectionReason(options, measurement, angleJump);
                    if (rejectionReason != null) {
                        logger.Warning(
                            $"Measurement rejected reason={rejectionReason} candidate={measuredAngle:F3} " +
                            $"lastAccepted={currentAngle:F3} jump={angleJump:F3} matched={measurement.MatchedStars} " +
                            $"quality={measurement.Quality:F3}");
                        Publish(options, currentAngle, measurement, $"Rejected - {rejectionReason}");
                        await Task.Delay(options.RefreshInterval, ct);
                        continue;
                    }

                    currentAngle = measuredAngle;
                    Publish(options, currentAngle, measurement, "Accepted");

                    var delta = NormalizeSigned(options.TargetAngle - currentAngle);
                    if (Math.Abs(delta) <= options.ToleranceDegrees) {
                        logger.Info(
                            $"Target reached position={currentAngle:F3} target={options.TargetAngle:F3} delta={delta:F3}");
                        Publish(options, currentAngle, measurement, "Target reached");
                        return ManualRotationResult.Reached(currentAngle);
                    }

                    await Task.Delay(options.RefreshInterval, ct);
                }
            } catch (OperationCanceledException) {
                logger.Info($"Session cancelled position={currentAngle:F3}");
                return ManualRotationResult.CancelledAt(currentAngle);
            } catch (Exception ex) {
                logger.Error($"Session failed position={currentAngle:F3}", ex);
                throw;
            }

            return ManualRotationResult.CancelledAt(currentAngle);
        }

        public void Cancel() {
            cancelSource?.Cancel();
        }

        private void Publish(ManualRotationOptions options, double currentAngle, RotationMeasurement measurement, string status) {
            StateChanged?.Invoke(this, new ManualRotationState {
                TargetAngle = options.TargetAngle,
                CurrentAngle = currentAngle,
                Delta = NormalizeSigned(options.TargetAngle - currentAngle),
                Direction = NormalizeSigned(options.TargetAngle - currentAngle) >= 0 ? "Clockwise" : "Anti-clockwise",
                MatchedStars = measurement.MatchedStars,
                RmsPixels = measurement.RmsPixels,
                Quality = measurement.Quality,
                TranslationX = measurement.TranslationX,
                TranslationY = measurement.TranslationY,
                Scale = measurement.Scale,
                FrameWidth = measurement.FrameWidth,
                FrameHeight = measurement.FrameHeight,
                CentralExclusionRatio = options.CentralExclusionRatio,
                CurrentStars = measurement.CurrentStars,
                Status = status
            });
        }

        private static string GetRejectionReason(ManualRotationOptions options, RotationMeasurement measurement, double angleJump) {
            if (measurement.MatchedStars < options.MinimumMatchedStars) {
                return "too few stars";
            }

            if (measurement.Quality < options.MinimumQuality) {
                return "low quality";
            }

            if (angleJump > options.MaximumAngleJumpDegrees) {
                return "angle jump";
            }

            return null;
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

        private static int Count<T>(System.Collections.Generic.IReadOnlyCollection<T> items) {
            return items?.Count ?? 0;
        }
    }

    public sealed class ManualRotationOptions {
        public double TargetAngle { get; set; }
        public double InitialAngle { get; set; }
        public double ToleranceDegrees { get; set; } = 0.25;
        public double ExposureSeconds { get; set; } = 3.0;
        public double MinimumQuality { get; set; } = 0.25;
        public int MinimumMatchedStars { get; set; } = 4;
        public double MaximumAngleJumpDegrees { get; set; } = 60.0;
        public double CentralExclusionRatio { get; set; } = 0.20;
        public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromSeconds(1);
    }

    public sealed class ManualRotationState {
        public double TargetAngle { get; set; }
        public double CurrentAngle { get; set; }
        public double Delta { get; set; }
        public string Direction { get; set; }
        public int MatchedStars { get; set; }
        public double RmsPixels { get; set; }
        public double Quality { get; set; }
        public double TranslationX { get; set; }
        public double TranslationY { get; set; }
        public double Scale { get; set; } = 1.0;
        public int FrameWidth { get; set; }
        public int FrameHeight { get; set; }
        public double CentralExclusionRatio { get; set; }
        public System.Collections.Generic.IReadOnlyList<StarCentroid> CurrentStars { get; set; }
        public string Status { get; set; }
    }

    public sealed class ManualRotationResult {
        private ManualRotationResult(double currentAngle, bool targetReached, bool cancelled) {
            CurrentAngle = currentAngle;
            TargetReached = targetReached;
            Cancelled = cancelled;
        }

        public double CurrentAngle { get; }
        public bool TargetReached { get; }
        public bool Cancelled { get; }

        public static ManualRotationResult Reached(double currentAngle) {
            return new ManualRotationResult(currentAngle, true, false);
        }

        public static ManualRotationResult CancelledAt(double currentAngle) {
            return new ManualRotationResult(currentAngle, false, true);
        }
    }
}
