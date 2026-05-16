using System.Windows;
using System.Windows.Input;
using WpfButton = System.Windows.Controls.Button;
using WpfGrid = System.Windows.Controls.Grid;
using WpfRowDefinition = System.Windows.Controls.RowDefinition;
using WpfStackPanel = System.Windows.Controls.StackPanel;
using WpfTextBlock = System.Windows.Controls.TextBlock;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace VLIT;

public sealed class PromptDialog : Window
{
    private readonly WpfTextBox _valueBox;

    public string Value => _valueBox.Text;

    public PromptDialog(string title, string label, string defaultValue = "")
    {
        Title = title;
        Width = 420;
        Height = 160;
        MinWidth = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = Palette.Brush("#111820");

        var root = new WpfGrid { Margin = new Thickness(12) };
        root.RowDefinitions.Add(new WpfRowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new WpfRowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new WpfRowDefinition { Height = GridLength.Auto });

        root.Children.Add(new WpfTextBlock
        {
            Text = label,
            Foreground = Palette.Brush("#E8EEF4"),
            Margin = new Thickness(0, 0, 0, 6)
        });

        _valueBox = new WpfTextBox
        {
            Text = defaultValue,
            Margin = new Thickness(0, 0, 0, 12),
            MinHeight = 28
        };
        WpfGrid.SetRow(_valueBox, 1);
        root.Children.Add(_valueBox);

        var buttons = new WpfStackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
        };
        var ok = new WpfButton { Content = "OK", Width = 72, Margin = new Thickness(0, 0, 6, 0), IsDefault = true };
        ok.Click += (_, _) => { DialogResult = true; Close(); };
        var cancel = new WpfButton { Content = "Cancel", Width = 72, IsCancel = true };
        cancel.Click += (_, _) => { DialogResult = false; Close(); };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        WpfGrid.SetRow(buttons, 2);
        root.Children.Add(buttons);

        Content = root;
        Loaded += (_, _) =>
        {
            _valueBox.Focus();
            _valueBox.SelectAll();
        };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        };
    }
}
