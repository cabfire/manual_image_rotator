using ManualImageRotator.NINA.Imaging;
using ManualImageRotator.NINA.Services;
using NINA.Equipment.Interfaces;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Interfaces.ViewModel;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace ManualImageRotator.NINA.Equipment {
    [Export(typeof(IEquipmentProvider))]
    public sealed class ManualImageRotatorProvider : IEquipmentProvider<IRotator> {
        private readonly IImagingMediator imagingMediator;

        [ImportingConstructor]
        public ManualImageRotatorProvider(IImagingMediator imagingMediator) {
            this.imagingMediator = imagingMediator;
        }

        public string Name => "Manual Image Rotator";

        public IList<IRotator> GetEquipment() {
            var settings = ManualImageRotatorSettings.Load();
            var logger = new NinaManualRotationLogger(() => settings.DebugLogging);
            var imageSource = new NinaRotationImageSource(imagingMediator, logger);
            var detector = new StarCentroidDetector(
                () => settings.DetectedStars,
                () => settings.CentralExclusionPercent / 100.0);
            var estimator = new RotationEstimator(detector);
            var session = new ManualRotationSession(imageSource, estimator, logger);

            return new List<IRotator> {
                new ManualImageRotatorDriver(session, settings)
            };
        }
    }
}
