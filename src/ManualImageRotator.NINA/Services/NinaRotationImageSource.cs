using ManualImageRotator.NINA.Imaging;
using NINA.Core.Model;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Model;
using NINA.Image.Interfaces;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ManualImageRotator.NINA.Services {
    public sealed class NinaRotationImageSource : IRotationImageSource {
        private readonly IImagingMediator imagingMediator;
        private readonly IManualRotationLogger logger;

        public NinaRotationImageSource(IImagingMediator imagingMediator)
            : this(imagingMediator, NullManualRotationLogger.Instance) {
        }

        public NinaRotationImageSource(IImagingMediator imagingMediator, IManualRotationLogger logger) {
            this.imagingMediator = imagingMediator;
            this.logger = logger ?? NullManualRotationLogger.Instance;
        }

        public async Task<RotationFrame> CaptureAsync(double exposureSeconds, CancellationToken token) {
            logger.Info($"Capture start exposure={exposureSeconds:F3}s");

            var sequence = new CaptureSequence {
                ExposureTime = exposureSeconds,
                ImageType = CaptureSequence.ImageTypes.SNAPSHOT,
                TotalExposureCount = 1,
                Gain = -1,
                Offset = -1
            };

            var progress = new Progress<ApplicationStatus>();
            var exposure = await imagingMediator.CaptureImage(sequence, token, progress, "Manual Image Rotator");
            var imageData = await exposure.ToImageData(progress, token);

            var frame = ToRotationFrame(imageData);
            logger.Info($"Capture complete width={frame.Width} height={frame.Height} pixels={frame.Pixels.Length}");
            return frame;
        }

        private static RotationFrame ToRotationFrame(IImageData imageData) {
            if (imageData == null) {
                throw new InvalidOperationException("NINA returned no image data.");
            }

            var width = imageData.Properties.Width;
            var height = imageData.Properties.Height;
            var pixels = imageData.Data?.FlatArray;

            if (width <= 0 || height <= 0) {
                throw new InvalidOperationException("NINA image has invalid dimensions.");
            }

            if (pixels == null || pixels.Length == 0) {
                throw new InvalidOperationException("NINA image contains no pixel data.");
            }

            var pixelCount = width * height;
            if (pixels.Length == pixelCount) {
                return new RotationFrame(width, height, pixels.ToArray());
            }

            if (pixels.Length == pixelCount * 3) {
                return new RotationFrame(width, height, ToLuminance(pixels, pixelCount));
            }

            throw new InvalidOperationException(
                $"Unexpected NINA pixel buffer length {pixels.Length} for image {width}x{height}.");
        }

        private static ushort[] ToLuminance(ushort[] rgbPixels, int pixelCount) {
            var pixels = new ushort[pixelCount];

            for (var i = 0; i < pixelCount; i++) {
                var offset = i * 3;
                var r = rgbPixels[offset];
                var g = rgbPixels[offset + 1];
                var b = rgbPixels[offset + 2];
                pixels[i] = (ushort)Math.Min(
                    ushort.MaxValue,
                    Math.Round((0.299 * r) + (0.587 * g) + (0.114 * b)));
            }

            return pixels;
        }
    }
}
