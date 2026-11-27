using Vortex.Presentation.Wpf;

namespace TcoAteqMeasurement
{
    public class TcoAteqG6Pn_v_3_5_xServiceViewModel : RenderableViewModel
    {
        public TcoAteqG6Pn_v_3_5_xServiceViewModel() : base()
        {
            Component = new TcoAteqG6Pn_v_3_5_x();
        }

        public TcoAteqG6Pn_v_3_5_x Component { get; internal set; }

        public override object Model
        {
            get => Component;
            set
            {
                Component = value as TcoAteqG6Pn_v_3_5_x;
            }
        }
    }

    public class TcoAteqG6Pn_v_3_5_xViewModel : TcoAteqG6Pn_v_3_5_xServiceViewModel
    { }
}
