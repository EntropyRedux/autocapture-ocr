using System.Windows;
using System.Windows.Input;

namespace AutoCaptureOCR.App.Views;

public partial class CreateProjectDialog : Window
{
    public string ProjectName => ProjectNameTextBox.Text.Trim();
    public string Description => DescriptionTextBox.Text.Trim();

    public CreateProjectDialog(string defaultName = "")
    {
        InitializeComponent();

        if (!string.IsNullOrWhiteSpace(defaultName))
        {
            ProjectNameTextBox.Text = defaultName;
            ProjectNameTextBox.SelectAll();
        }

        ProjectNameTextBox.Focus();
    }

    private void TextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        CreateButton.IsEnabled = !string.IsNullOrWhiteSpace(ProjectNameTextBox.Text);
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
