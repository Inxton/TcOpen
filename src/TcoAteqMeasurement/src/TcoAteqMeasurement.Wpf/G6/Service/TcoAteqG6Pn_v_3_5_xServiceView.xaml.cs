using System.ComponentModel;
using System.Windows.Controls;

namespace TcoAteqMeasurement
{
    public partial class TcoAteqG6Pn_v_3_5_xServiceView : UserControl
    {
        public TcoAteqG6Pn_v_3_5_xServiceView()
        {
            if (DesignerProperties.GetIsInDesignMode(this))
            {
                this.DataContext = new TcoAteqG6Pn_v_3_5_xServiceViewModel();
            }

            InitializeComponent();
        }
    }
}
