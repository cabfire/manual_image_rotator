using System.ComponentModel.Composition;
using System.Windows;

namespace ManualImageRotator.NINA {
    [Export(typeof(ResourceDictionary))]
    public partial class Options : ResourceDictionary {
        public Options() {
            InitializeComponent();
        }
    }
}
