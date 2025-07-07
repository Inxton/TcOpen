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
using RelayCommand = TcOpen.Inxton.Input.RelayCommand;
using TcOpen.Inxton.TcoDrivesBeckhoff.Wpf.Properties;
using Microsoft.Win32;

namespace TcoDrivesBeckhoff
{
    public class TcoInterpolatorServiceViewModel : RenderableViewModel
    {
     

        public TcoInterpolatorServiceViewModel()
        {
             

        }

       
      
        public TcoInterpolator Component { get; private set; }

       
        public override object Model { get => this.Component; set { this.Component = value as TcoInterpolator; } }

    }

    public class TcoInterpolatorViewModel : TcoMultiAxisServiceViewModel
    { }
}
