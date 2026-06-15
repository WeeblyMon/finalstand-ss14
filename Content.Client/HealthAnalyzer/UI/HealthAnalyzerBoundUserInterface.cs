using Content.Shared._Shitmed.Targeting;
using Content.Shared._Shitmed.Medical.HealthAnalyzer;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.HealthAnalyzer.UI
{
    [UsedImplicitly]
    public sealed class HealthAnalyzerBoundUserInterface : BoundUserInterface
    {
        [ViewVariables]
        private HealthAnalyzerWindow? _window;

        public HealthAnalyzerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
        }

        protected override void Open()
        {
            base.Open();

            _window = this.CreateWindow<HealthAnalyzerWindow>();
            _window.OnBodyPartSelected += SendBodyPartMessage;
            _window.OnModeChanged += SendModeMessage;
            _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
        }

        protected override void ReceiveMessage(BoundUserInterfaceMessage message)
        {
            if (_window == null)
                return;

            switch (message)
            {
                case HealthAnalyzerBodyMessage bodyMessage:
                    _window.Populate(bodyMessage);
                    break;
                case HealthAnalyzerOrgansMessage organsMessage:
                    _window.Populate(organsMessage);
                    break;
                case HealthAnalyzerChemicalsMessage chemicalsMessage:
                    _window.Populate(chemicalsMessage);
                    break;
            }
        }

        private void SendBodyPartMessage(TargetBodyPart? part, EntityUid target) =>
            SendMessage(new HealthAnalyzerPartSelectedMessage(EntMan.GetNetEntity(target), part));

        private void SendModeMessage(HealthAnalyzerMode mode, EntityUid target) =>
            SendMessage(new HealthAnalyzerModeSelectedMessage(EntMan.GetNetEntity(target), mode));

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing)
                return;

            if (_window != null)
                _window.OnBodyPartSelected -= SendBodyPartMessage;

            _window?.Dispose();
        }
    }
}
