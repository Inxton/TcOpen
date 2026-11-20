using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Vortex.Connector;
using Vortex.Presentation.Wpf;


namespace TcoAbbRobotics
{
    public class TcoAbbRW_v_8_2_xServiceViewModel : TcoAbbRoboticsBaseServiceViewModel<TcoAbbRW_v_8_2_x>
    {

        public TcoAbbRW_v_8_2_xServiceViewModel():base()
        {
           

        }
      
    }

    public class TcoAbbRW_v_8_2_xViewModel : TcoAbbRW_v_8_2_xServiceViewModel
    { }
}
