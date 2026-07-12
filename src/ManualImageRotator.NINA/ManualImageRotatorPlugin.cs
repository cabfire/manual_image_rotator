using ManualImageRotator.NINA.Equipment;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace ManualImageRotator.NINA {
    [Export(typeof(IPluginManifest))]
    public sealed class ManualImageRotatorPlugin : PluginBase, INotifyPropertyChanged {
        private readonly ManualImageRotatorSettings settings;

        public ManualImageRotatorPlugin() {
            settings = ManualImageRotatorSettings.Load();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public double ExposureSeconds {
            get => settings.ExposureSeconds;
            set => SetAndSave(value, v => settings.ExposureSeconds = v);
        }

        public double RefreshIntervalSeconds {
            get => settings.RefreshIntervalSeconds;
            set => SetAndSave(value, v => settings.RefreshIntervalSeconds = v);
        }

        public double ToleranceDegrees {
            get => settings.ToleranceDegrees;
            set => SetAndSave(value, v => settings.ToleranceDegrees = v);
        }

        public double CentralExclusionPercent {
            get => settings.CentralExclusionPercent;
            set => SetAndSave(value, v => settings.CentralExclusionPercent = v);
        }

        public int DetectedStars {
            get => settings.DetectedStars;
            set {
                if (settings.DetectedStars == value) {
                    return;
                }

                settings.DetectedStars = value;
                SaveAndNotify();
            }
        }

        public bool DebugLogging {
            get => settings.DebugLogging;
            set {
                if (settings.DebugLogging == value) {
                    return;
                }

                settings.DebugLogging = value;
                SaveAndNotify();
            }
        }

        public override Task Initialize() {
            return base.Initialize();
        }

        public override Task Teardown() {
            return base.Teardown();
        }

        private void SetAndSave(double value, System.Action<double> setter, [CallerMemberName] string propertyName = null) {
            setter(value);
            SaveAndNotify(propertyName);
        }

        private void SaveAndNotify([CallerMemberName] string propertyName = null) {
            settings.Validate();
            settings.Save();
            OnPropertyChanged(propertyName);
            OnPropertyChanged(nameof(ExposureSeconds));
            OnPropertyChanged(nameof(RefreshIntervalSeconds));
            OnPropertyChanged(nameof(ToleranceDegrees));
            OnPropertyChanged(nameof(CentralExclusionPercent));
            OnPropertyChanged(nameof(DetectedStars));
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
