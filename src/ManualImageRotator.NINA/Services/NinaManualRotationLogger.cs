using NINA.Core.Utility;
using System;

namespace ManualImageRotator.NINA.Services {
    public sealed class NinaManualRotationLogger : IManualRotationLogger {
        private const string Prefix = "ManualImageRotator: ";
        private readonly Func<bool> isDebugEnabled;

        public NinaManualRotationLogger()
            : this(() => false) {
        }

        public NinaManualRotationLogger(Func<bool> isDebugEnabled) {
            this.isDebugEnabled = isDebugEnabled ?? (() => false);
        }

        public void Info(string message) {
            if (isDebugEnabled()) {
                Logger.Info(Prefix + message);
            }
        }

        public void Warning(string message) {
            if (isDebugEnabled()) {
                Logger.Warning(Prefix + message);
            }
        }

        public void Error(string message, Exception exception) {
            Logger.Error(Prefix + message + Environment.NewLine + exception);
        }
    }
}
