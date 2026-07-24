using System.Windows.Controls;

namespace ModTogetherUniversal
{
    // A simple wrapper Page that hosts an external plugin Page inside a Frame.
    // This is needed because WPF.UI NavigationView requires a local Type for navigation;
    // it cannot navigate directly to a Page instance loaded from an external DLL.
    public partial class DynamicPluginPage : Page
    {
        public static Page? CurrentPluginPage { get; set; }

        public DynamicPluginPage()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                if (CurrentPluginPage != null)
                {
                    InnerFrame.Navigate(CurrentPluginPage);
                }
            };
        }
    }
}

