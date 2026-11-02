using System.Windows;
using System.Windows.Input;
using UserControl = System.Windows.Controls.UserControl;

namespace NStCut.App.Controls;

public partial class LabeledPathPicker : UserControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(LabeledPathPicker), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty PathValueProperty =
        DependencyProperty.Register(nameof(PathValue), typeof(string), typeof(LabeledPathPicker),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty ButtonTextProperty =
        DependencyProperty.Register(nameof(ButtonText), typeof(string), typeof(LabeledPathPicker), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty BrowseCommandProperty =
        DependencyProperty.Register(nameof(BrowseCommand), typeof(ICommand), typeof(LabeledPathPicker));

    public LabeledPathPicker()
    {
        InitializeComponent();
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string PathValue
    {
        get => (string)GetValue(PathValueProperty);
        set => SetValue(PathValueProperty, value);
    }

    public string ButtonText
    {
        get => (string)GetValue(ButtonTextProperty);
        set => SetValue(ButtonTextProperty, value);
    }

    public ICommand? BrowseCommand
    {
        get => (ICommand?)GetValue(BrowseCommandProperty);
        set => SetValue(BrowseCommandProperty, value);
    }
}
