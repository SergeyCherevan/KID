using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KID.Services.Initialize.Interfaces
{
    public interface IWindowConfigurationService
    {
        public WindowConfigurationData Settings { get; set; }
        public void SetConfigurationFromFile();
        public void SetDefaultCode();
    }
}
