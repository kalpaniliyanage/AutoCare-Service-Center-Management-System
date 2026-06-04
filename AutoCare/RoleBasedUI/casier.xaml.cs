using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AutoCare.RoleBasedUI
{
    public partial class casier : Page
    {
        public casier()
        {
            InitializeComponent();
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            // Try Application.Current.MainWindow first, then fallback to Window.GetWindow(this)
            MainWindow mainWindow = Application.Current?.MainWindow as MainWindow ?? Window.GetWindow(this) as MainWindow;
            if (mainWindow != null)
            {
                // Clear navigation history if any
                var nav = mainWindow.MainFrame.NavigationService;
                if (nav != null)
                {
                    while (nav.RemoveBackEntry() != null) { }
                }

                // Clear the frame content
                mainWindow.MainFrame.Content = null;
            }
        }
    }
}
