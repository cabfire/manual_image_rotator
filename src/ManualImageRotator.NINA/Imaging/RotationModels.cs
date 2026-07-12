using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ManualImageRotator.NINA.Imaging {
    public interface IRotationImageSource {
        Task<RotationFrame> CaptureAsync(double exposureSeconds, CancellationToken token);
    }

    public sealed class RotationFrame {
        public RotationFrame(int width, int height, ushort[] pixels) {
            Width = width;
            Height = height;
            Pixels = pixels;
        }

        public int Width { get; }
        public int Height { get; }
        public ushort[] Pixels { get; }
    }

    public struct StarCentroid {
        public double X { get; set; }
        public double Y { get; set; }
        public double Flux { get; set; }
    }

    public sealed class RotationMeasurement {
        public double AngleDegrees { get; set; }
        public int MatchedStars { get; set; }
        public double RmsPixels { get; set; }
        public double Quality { get; set; }
        public double TranslationX { get; set; }
        public double TranslationY { get; set; }
        public double Scale { get; set; } = 1.0;
        public IReadOnlyList<StarCentroid> ReferenceStars { get; set; }
        public IReadOnlyList<StarCentroid> CurrentStars { get; set; }
    }
}
