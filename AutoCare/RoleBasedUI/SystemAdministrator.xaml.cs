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
    public partial class SystemAdministrator : Page
    {
        public SystemAdministrator()
        {
            InitializeComponent();
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow mainWindow = Application.Current?.MainWindow as MainWindow ?? Window.GetWindow(this) as MainWindow;
            if (mainWindow != null)
            {
                var nav = mainWindow.MainFrame.NavigationService;
                if (nav != null)
                {
                    while (nav.RemoveBackEntry() != null) { }
                }

                mainWindow.MainFrame.Content = null;
            }
        }
    }
}