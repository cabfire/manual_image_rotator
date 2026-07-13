using ManualImageRotator.NINA.Services;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ManualImageRotator.NINA.Equipment {
    public sealed class ManualImageRotatorDriver : BaseINPC, IRotator {
        private readonly ManualRotationSession session;
        private readonly ManualImageRotatorSettings settings;
        private CancellationTokenSource moveCts;
        private Task moveTask;
        private TaskCompletionSource<bool> moveCompletion;
        private ManualImageRotatorMoveWindow moveWindow;
        private float measuredPosition;
        private float targetPosition;
        private bool connected;
        private bool isMoving;
        private bool reverse;
        private bool synced;
        private bool acceptCurrentPositionRequested;
        private int restorePositionVersion;

        public ManualImageRotatorDriver(ManualRotationSession session)
            : this(session, ManualImageRotatorSettings.Load()) {
        }

        public ManualImageRotatorDriver(ManualRotationSession session, ManualImageRotatorSettings settings) {
            this.session = session;
            this.settings = settings ?? ManualImageRotatorSettings.Load();
            reverse = this.settings.Reverse;
            session.StateChanged += OnSessionStateChanged;
        }

        public bool HasSetupDialog => true;
        public string Id => "manual-image-rotator";
        public string Name => "Manual Image Rotator";
        public string DisplayName => Name;
        public string Category => "Rotator";
        public bool Connected => connected;
        public string Description => "Guided manual camera rotation using live image measurements.";
        public string DriverInfo => "Manual Image Rotator for N.I.N.A.";
        public string DriverVersion => "0.2.0";
        public IList<string> SupportedActions => new List<string>();

        public bool CanReverse => true;

        public bool Reverse {
            get => reverse;
            set {
                if (reverse == value) {
                    return;
                }

                var displayedPosition = Position;
                reverse = value;
                settings.Reverse = value;
                settings.Save();
                measuredPosition = ToMeasuredPosition(displayedPosition);
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(Position));
                RaisePropertyChanged(nameof(MechanicalPosition));
            }
        }

        public bool IsMoving {
            get => isMoving;
            private set {
                isMoving = value;
                RaisePropertyChanged();
            }
        }

        public bool Synced => synced;

        public float Position {
            get => ToDisplayPosition(measuredPosition);
        }

        public float TargetPosition {
            get => targetPosition;
            private set {
                targetPosition = Normalize360(value);
                RaisePropertyChanged();
            }
        }

        public float MechanicalPosition => Position;
        public float StepSize => 0.01f;

        public Task<bool> Connect(CancellationToken token) {
            connected = true;
            RaisePropertyChanged(nameof(Connected));
            return Task.FromResult(true);
        }

        public void Disconnect() {
            Halt();
            connected = false;
            RaisePropertyChanged(nameof(Connected));
        }

        public void Sync(float skyAngle) {
            SetMeasuredPosition(ToMeasuredPosition(skyAngle));
            synced = true;
            RaisePropertyChanged(nameof(Synced));
        }

        public Task<bool> Move(float positionDelta, CancellationToken ct) {
            var target = Normalize360(Position + positionDelta);
            return MoveAbsolute(target, ct);
        }

        public Task<bool> MoveAbsolute(float targetPosition, CancellationToken ct) {
            return StartMove(targetPosition, ct);
        }

        public Task<bool> MoveAbsoluteMechanical(float targetPosition, CancellationToken ct) {
            return StartMove(targetPosition, ct);
        }

        public void Halt() {
            CompleteMove(false);
            var cts = moveCts;
            cts?.Cancel();
            session.Cancel();
            CloseMoveWindow();
            PublishStoppedState();
        }

        public void SetupDialog() {
            if (IsMoving) {
                return;
            }

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess()) {
                dispatcher.Invoke(SetupDialog);
                return;
            }

            var window = new ManualImageRotatorSetupWindow(settings, ResetCurrentPosition);
            var owner = System.Windows.Application.Current?.MainWindow;
            if (owner != null && owner.IsVisible) {
                window.Owner = owner;
            }

            window.ShowDialog();
        }

        public string Action(string actionName, string actionParameters) {
            return string.Empty;
        }

        public string SendCommandString(string command, bool raw = true) {
            throw new NotSupportedException();
        }

        public bool SendCommandBool(string command, bool raw = true) {
            throw new NotSupportedException();
        }

        public void SendCommandBlind(string command, bool raw = true) {
            throw new NotSupportedException();
        }

        private Task<bool> StartMove(float targetPosition, CancellationToken ct) {
            if (!Connected) {
                return Task.FromResult(false);
            }

            Halt();
            restorePositionVersion++;
            moveCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var runCts = moveCts;
            moveCompletion = new TaskCompletionSource<bool>();
            acceptCurrentPositionRequested = false;
            TargetPosition = targetPosition;
            IsMoving = true;
            Logger.Info($"ManualImageRotator: move requested current={Position:0.000} target={TargetPosition:0.000} exposure={settings.ExposureSeconds:0.###}s refresh={settings.RefreshIntervalSeconds:0.###}s tolerance={settings.ToleranceDegrees:0.###} stars={settings.DetectedStars} exclusion={settings.CentralExclusionPercent:0.#}% minQuality={settings.MinimumQuality:0.###} minMatched={settings.MinimumMatchedStars} maxJump={settings.MaximumAngleJumpDegrees:0.###}");
            ShowMoveWindow(Position, TargetPosition, "Capturing reference");

            moveTask = RunMoveAsync(TargetPosition, runCts);
            return moveCompletion.Task;
        }

        private async Task RunMoveAsync(float requestedTargetPosition, CancellationTokenSource runCts) {
            var completed = false;

            try {
                var options = new ManualRotationOptions {
                    TargetAngle = ToMeasuredPosition(requestedTargetPosition),
                    InitialAngle = measuredPosition,
                    ToleranceDegrees = settings.ToleranceDegrees,
                    ExposureSeconds = settings.ExposureSeconds,
                    MinimumQuality = settings.MinimumQuality,
                    MinimumMatchedStars = settings.MinimumMatchedStars,
                    MaximumAngleJumpDegrees = settings.MaximumAngleJumpDegrees,
                    RefreshInterval = settings.RefreshInterval
                };

                var result = await session.RunAsync(options, runCts.Token);
                SetMeasuredPosition((float)result.CurrentAngle);
                if (acceptCurrentPositionRequested && !result.TargetReached) {
                    TargetPosition = Position;
                    UpdateMoveWindow(Position, Position, "Position accepted", true);
                    completed = true;
                    CompleteMove(true);
                    return;
                }

                UpdateMoveWindow(Position, requestedTargetPosition, result.TargetReached ? "Target reached" : "Cancelled", result.TargetReached);
                completed = result.TargetReached;
                CompleteMove(result.TargetReached);
            } catch (OperationCanceledException) {
                completed = acceptCurrentPositionRequested;
                CompleteMove(acceptCurrentPositionRequested);
            } catch (Exception ex) {
                Logger.Error($"ManualImageRotator: move failed{Environment.NewLine}{ex}");
                CompleteMove(false);
            } finally {
                if (ReferenceEquals(moveCts, runCts)) {
                    moveCts = null;
                }

                if (!completed) {
                    TargetPosition = Position;
                }

                PublishStoppedState();
                runCts.Dispose();
            }
        }

        private void OnSessionStateChanged(object sender, ManualRotationState state) {
            if (acceptCurrentPositionRequested) {
                return;
            }

            SetMeasuredPosition((float)state.CurrentAngle);
            UpdateMoveWindow(
                ToDisplayPosition((float)state.CurrentAngle),
                ToDisplayPosition((float)state.TargetAngle),
                state.Status,
                string.Equals(state.Status, "Target reached", StringComparison.Ordinal),
                state.MatchedStars,
                state.RmsPixels,
                state.Quality,
                state.TranslationX,
                state.TranslationY,
                state.Scale);
        }

        private void SetMeasuredPosition(float value) {
            measuredPosition = Normalize360(value);
            RaisePropertyChanged(nameof(Position));
            RaisePropertyChanged(nameof(MechanicalPosition));
        }

        private void ResetCurrentPosition() {
            Halt();
            restorePositionVersion++;
            SetMeasuredPosition(0f);
            TargetPosition = 0f;
            synced = false;
            RaisePropertyChanged(nameof(Synced));
            PublishDeviceStateChanged();
        }

        private void PublishStoppedState() {
            IsMoving = false;
            TargetPosition = Position;
            PublishDeviceStateChanged();
            _ = PublishDeviceStateChangedSoonAsync();
        }

        private void CompleteMove(bool success) {
            moveCompletion?.TrySetResult(success);
        }

        private async Task PublishDeviceStateChangedSoonAsync() {
            await Task.Delay(250);
            PublishDeviceStateChanged();
        }

        private void PublishDeviceStateChanged() {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess()) {
                dispatcher.BeginInvoke(new Action(PublishDeviceStateChanged));
                return;
            }

            RaisePropertyChanged(nameof(IsMoving));
            RaisePropertyChanged(nameof(Position));
            RaisePropertyChanged(nameof(MechanicalPosition));
            RaisePropertyChanged(nameof(TargetPosition));
            RaisePropertyChanged(nameof(Connected));
            RaisePropertyChanged(nameof(Reverse));
            RaisePropertyChanged(nameof(Synced));
            RaisePropertyChanged(nameof(StepSize));
        }

        private void AcceptCurrentPosition() {
            acceptCurrentPositionRequested = true;
            var realMeasuredPosition = measuredPosition;
            var acceptedTarget = TargetPosition;
            var currentRestoreVersion = ++restorePositionVersion;
            SetMeasuredPosition(ToMeasuredPosition(acceptedTarget));
            TargetPosition = acceptedTarget;
            if (settings.DebugLogging) {
                Logger.Info($"ManualImageRotator: accepting current position early. Real measured={realMeasuredPosition:0.000}, fake mechanical={MechanicalPosition:0.000}, target={TargetPosition:0.000}");
            }
            var cts = moveCts;
            cts?.Cancel();
            session.Cancel();
            CloseMoveWindow();
            PublishStoppedState();
            CompleteMove(true);
            _ = RestoreMeasuredPositionSoonAsync(realMeasuredPosition, currentRestoreVersion);
        }

        private async Task RestoreMeasuredPositionSoonAsync(float realMeasuredPosition, int expectedRestoreVersion) {
            await Task.Delay(10000);

            if (expectedRestoreVersion != restorePositionVersion || IsMoving) {
                return;
            }

            SetMeasuredPosition(realMeasuredPosition);
            TargetPosition = Position;
            if (settings.DebugLogging) {
                Logger.Info($"ManualImageRotator: restored measured position. Mechanical={MechanicalPosition:0.000}, target={TargetPosition:0.000}");
            }
            PublishDeviceStateChanged();
        }

        private float ToMeasuredPosition(float displayedPosition) {
            return reverse ? Normalize360(-displayedPosition) : Normalize360(displayedPosition);
        }

        private float ToDisplayPosition(float measuredPosition) {
            return reverse ? Normalize360(-measuredPosition) : Normalize360(measuredPosition);
        }

        private void ShowMoveWindow(float currentPosition, float targetPosition, string status) {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null) {
                return;
            }

            dispatcher.BeginInvoke(new Action(() => {
                CloseMoveWindowOnCurrentThread();

                moveWindow = new ManualImageRotatorMoveWindow();
                var owner = System.Windows.Application.Current?.MainWindow;
                if (owner != null && owner.IsVisible) {
                    moveWindow.Owner = owner;
                }

                moveWindow.OkRequested += (sender, args) => AcceptCurrentPosition();
                moveWindow.Closed += (sender, args) => {
                    if (ReferenceEquals(moveWindow, sender)) {
                        moveWindow = null;
                    }
                };
                moveWindow.Update(currentPosition, targetPosition, status, false);
                moveWindow.Show();
            }));
        }

        private void UpdateMoveWindow(float currentPosition, float targetPosition, string status, bool targetReached) {
            UpdateMoveWindow(
                currentPosition,
                targetPosition,
                status,
                targetReached,
                0,
                double.NaN,
                double.NaN,
                double.NaN,
                double.NaN,
                double.NaN);
        }

        private void UpdateMoveWindow(
            float currentPosition,
            float targetPosition,
            string status,
            bool targetReached,
            int matchedStars,
            double rmsPixels,
            double quality,
            double translationX,
            double translationY,
            double scale) {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null) {
                return;
            }

            dispatcher.BeginInvoke(new Action(() => {
                moveWindow?.Update(
                    currentPosition,
                    targetPosition,
                    status,
                    targetReached,
                    matchedStars,
                    rmsPixels,
                    quality,
                    translationX,
                    translationY,
                    scale);
            }));
        }

        private void CloseMoveWindow() {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null) {
                return;
            }

            dispatcher.BeginInvoke(new Action(CloseMoveWindowOnCurrentThread));
        }

        private void CloseMoveWindowOnCurrentThread() {
            if (moveWindow == null) {
                return;
            }

            var window = moveWindow;
            moveWindow = null;
            window.Close();
        }

        private static float Normalize360(float angle) {
            angle %= 360f;
            if (angle < 0f) {
                angle += 360f;
            }

            return angle;
        }
    }
}
