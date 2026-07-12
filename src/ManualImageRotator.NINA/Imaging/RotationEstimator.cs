using System;
using System.Collections.Generic;

namespace ManualImageRotator.NINA.Imaging {
    public sealed class RotationEstimator {
        private const double MinimumPairLengthPixels = 30.0;
        private const double MaximumScaleChange = 0.20;
        private const double InlierTolerancePixels = 10.0;

        private readonly StarCentroidDetector detector;

        public RotationEstimator(StarCentroidDetector detector) {
            this.detector = detector;
        }

        public RotationMeasurement Measure(RotationFrame reference, RotationFrame current) {
            var referenceStars = detector.Detect(reference);
            var currentStars = detector.Detect(current);

            if (referenceStars.Count < 3 || currentStars.Count < 3) {
                return Poor(referenceStars, currentStars);
            }

            var match = MatchBySimilarity(referenceStars, currentStars);
            if (match.MatchedStars < 3) {
                return Poor(referenceStars, currentStars);
            }

            var quality = Math.Max(0.0, Math.Min(1.0, match.MatchedStars / 10.0)) *
                Math.Max(0.0, 1.0 - (match.RmsPixels / InlierTolerancePixels));

            return new RotationMeasurement {
                AngleDegrees = match.RotationRadians * 180.0 / Math.PI,
                MatchedStars = match.MatchedStars,
                RmsPixels = match.RmsPixels,
                Quality = quality,
                TranslationX = match.TranslationX,
                TranslationY = match.TranslationY,
                Scale = match.Scale,
                ReferenceStars = referenceStars,
                CurrentStars = currentStars
            };
        }

        private static SimilarityMatch MatchBySimilarity(
            IReadOnlyList<StarCentroid> referenceStars,
            IReadOnlyList<StarCentroid> currentStars) {
            var referencePairs = BuildPairs(referenceStars);
            var currentPairs = BuildPairs(currentStars);
            var best = SimilarityMatch.Empty;

            foreach (var referencePair in referencePairs) {
                foreach (var currentPair in currentPairs) {
                    var scale = currentPair.Length / referencePair.Length;
                    if (Math.Abs(scale - 1.0) > MaximumScaleChange) {
                        continue;
                    }

                    var lengthTolerance = Math.Max(12.0, referencePair.Length * 0.12);
                    if (Math.Abs(currentPair.Length - referencePair.Length) > lengthTolerance) {
                        continue;
                    }

                    TryCandidate(referenceStars, currentStars, referencePair, currentPair, false, ref best);
                    TryCandidate(referenceStars, currentStars, referencePair, currentPair, true, ref best);
                }
            }

            return best;
        }

        private static void TryCandidate(
            IReadOnlyList<StarCentroid> referenceStars,
            IReadOnlyList<StarCentroid> currentStars,
            StarPair referencePair,
            StarPair currentPair,
            bool swapCurrent,
            ref SimilarityMatch best) {
            var referenceA = referenceStars[referencePair.A];
            var referenceB = referenceStars[referencePair.B];
            var currentA = currentStars[swapCurrent ? currentPair.B : currentPair.A];
            var currentB = currentStars[swapCurrent ? currentPair.A : currentPair.B];
            var scale = currentPair.Length / referencePair.Length;
            var rotation = NormalizeRadians(Math.Atan2(currentB.Y - currentA.Y, currentB.X - currentA.X) -
                Math.Atan2(referenceB.Y - referenceA.Y, referenceB.X - referenceA.X));
            var cos = Math.Cos(rotation);
            var sin = Math.Sin(rotation);
            var translationX = currentA.X - (scale * ((cos * referenceA.X) - (sin * referenceA.Y)));
            var translationY = currentA.Y - (scale * ((sin * referenceA.X) + (cos * referenceA.Y)));
            var candidate = ScoreTransform(referenceStars, currentStars, rotation, scale, translationX, translationY);

            if (candidate.MatchedStars > best.MatchedStars ||
                (candidate.MatchedStars == best.MatchedStars && candidate.RmsPixels < best.RmsPixels)) {
                best = candidate;
            }
        }

        private static SimilarityMatch ScoreTransform(
            IReadOnlyList<StarCentroid> referenceStars,
            IReadOnlyList<StarCentroid> currentStars,
            double rotation,
            double scale,
            double translationX,
            double translationY) {
            var used = new bool[currentStars.Count];
            var toleranceSquared = InlierTolerancePixels * InlierTolerancePixels;
            var cos = Math.Cos(rotation);
            var sin = Math.Sin(rotation);
            var count = 0;
            var sumSquared = 0.0;

            foreach (var reference in referenceStars) {
                var predictedX = (scale * ((cos * reference.X) - (sin * reference.Y))) + translationX;
                var predictedY = (scale * ((sin * reference.X) + (cos * reference.Y))) + translationY;
                var bestIndex = -1;
                var bestSquared = double.PositiveInfinity;

                for (var i = 0; i < currentStars.Count; i++) {
                    if (used[i]) {
                        continue;
                    }

                    var current = currentStars[i];
                    var dx = current.X - predictedX;
                    var dy = current.Y - predictedY;
                    var squared = (dx * dx) + (dy * dy);
                    if (squared < bestSquared) {
                        bestSquared = squared;
                        bestIndex = i;
                    }
                }

                if (bestIndex < 0 || bestSquared > toleranceSquared) {
                    continue;
                }

                used[bestIndex] = true;
                count++;
                sumSquared += bestSquared;
            }

            if (count == 0) {
                return SimilarityMatch.Empty;
            }

            return new SimilarityMatch {
                RotationRadians = rotation,
                TranslationX = translationX,
                TranslationY = translationY,
                Scale = scale,
                MatchedStars = count,
                RmsPixels = Math.Sqrt(sumSquared / count)
            };
        }

        private static List<StarPair> BuildPairs(IReadOnlyList<StarCentroid> stars) {
            var pairs = new List<StarPair>();

            for (var a = 0; a < stars.Count - 1; a++) {
                for (var b = a + 1; b < stars.Count; b++) {
                    var dx = stars[b].X - stars[a].X;
                    var dy = stars[b].Y - stars[a].Y;
                    var length = Math.Sqrt((dx * dx) + (dy * dy));
                    if (length < MinimumPairLengthPixels) {
                        continue;
                    }

                    pairs.Add(new StarPair {
                        A = a,
                        B = b,
                        Length = length
                    });
                }
            }

            return pairs;
        }

        private static RotationMeasurement Poor(IReadOnlyList<StarCentroid> referenceStars, IReadOnlyList<StarCentroid> currentStars) {
            return new RotationMeasurement {
                AngleDegrees = 0,
                MatchedStars = 0,
                RmsPixels = double.PositiveInfinity,
                Quality = 0,
                Scale = 1.0,
                ReferenceStars = referenceStars,
                CurrentStars = currentStars
            };
        }

        private static double NormalizeRadians(double angle) {
            while (angle > Math.PI) {
                angle -= 2.0 * Math.PI;
            }

            while (angle <= -Math.PI) {
                angle += 2.0 * Math.PI;
            }

            return angle;
        }

        private struct StarPair {
            public int A { get; set; }
            public int B { get; set; }
            public double Length { get; set; }
        }

        private struct SimilarityMatch {
            public static readonly SimilarityMatch Empty = new SimilarityMatch {
                RmsPixels = double.PositiveInfinity,
                Scale = 1.0
            };

            public double RotationRadians { get; set; }
            public double TranslationX { get; set; }
            public double TranslationY { get; set; }
            public double Scale { get; set; }
            public int MatchedStars { get; set; }
            public double RmsPixels { get; set; }
        }
    }
}
