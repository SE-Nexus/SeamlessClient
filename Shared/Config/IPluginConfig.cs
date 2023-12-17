using System.ComponentModel;

namespace Shared.Config
{
    public interface IPluginConfig: INotifyPropertyChanged
    {
        bool Enabled { get; set; }
        bool GPSEtaEnabled { get; set; }
    }
}