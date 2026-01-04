using System.Windows;
using System.Windows.Input;

namespace AutoCaptureOCR.App.Views;

public partial class CreateSessionDialog : Window
{
    public string SessionName => SessionNameTextBox.Text.Trim();

    public CreateSessionDialog(string projectName, string defaultName = "")
    {
        InitializeComponent();

        ProjectInfoTextBlock.Text = $"Project: {projectName}";

        if (!string.IsNullOrWhiteSpace(defaultName))
        {
            SessionNameTextBox.Text = defaultName;
            SessionNameTextBox.SelectAll();
        }

        SessionNameTextBox.Focus();
    }

    private void TextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        CreateButton.IsEnabled = !string.IsNullOrWhiteSpace(SessionNameTextBox.Text);
    }

    private void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }
        else if (e.Key == Key.Enter && CreateButton.IsEnabled)
        {
            DialogResult = true;
            Close();
        }
    }
}
