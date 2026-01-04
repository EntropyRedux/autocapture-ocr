using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AutoCaptureOCR.App.Models;

public class MetadataField : INotifyPropertyChanged
{
    private string key = "";
    private string value = "";

    public string Key
    {
        get => key;
        set
        {
            key = value;
            OnPropertyChanged();
        }
    }

    public string Value
    {
        get => this.value;
        set
        {
            this.value = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
