using System;
using System.Windows;
using System.Windows.Navigation;

namespace AutoCare
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // 1. System Administrator Button (Passcode: 0000)
        private void BtnAdmin_Click(object sender, RoutedEventArgs e)
        {
            // Popup Message in English
            string passcode = Microsoft.VisualBasic.Interaction.InputBox(
                "Do you want to log in to the System Administrator page? If so, please enter the passcode:",
                "Admin Login",
                "");

            if (passcode == "0000")
            {
                MessageBox.Show("Login Success!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                MainFrame.Navigate(new Uri("RoleBasedUI/SystemAdministrator.xaml", UriKind.Relative));
            }
            else if (!string.IsNullOrEmpty(passcode)) // Cancel බොත්තම එබුවහොත් කිසිවක් සිදු නොවේ
            {
                MessageBox.Show("Login Fail! Invalid Passcode.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 2. Receptionist Button (Passcode: 0001)
        private void BtnReceptionist_Click(object sender, RoutedEventArgs e)
        {
            // Popup Message in English
            string passcode = Microsoft.VisualBasic.Interaction.InputBox(
                "Do you want to log in to the Receptionist page? If so, please enter the passcode:",
                "Receptionist Login",
                "");

            if (passcode == "0001")
            {
                MessageBox.Show("Login Success!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                MainFrame.Navigate(new Uri("RoleBasedUI/Receptionist.xaml", UriKind.Relative));
            }
            else if (!string.IsNullOrEmpty(passcode))
            {
                MessageBox.Show("Login Fail! Invalid Passcode.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 3. Service Manager Button (Passcode: 0002)
        private void BtnServiceManager_Click(object sender, RoutedEventArgs e)
        {
            // Popup Message in English
            string passcode = Microsoft.VisualBasic.Interaction.InputBox(
                "Do you want to log in to the Service Manager page? If so, please enter the passcode:",
                "Service Manager Login",
                "");

            if (passcode == "0002")
            {
                MessageBox.Show("Login Success!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                MainFrame.Navigate(new Uri("RoleBasedUI/ServiceManager.xaml", UriKind.Relative));
            }
            else if (!string.IsNullOrEmpty(passcode))
            {
                MessageBox.Show("Login Fail! Invalid Passcode.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 4. Cashier Button (Passcode: 0003)
        private void BtnCashier_Click(object sender, RoutedEventArgs e)
        {
            // Popup Message in English
            string passcode = Microsoft.VisualBasic.Interaction.InputBox(
                "Do you want to log in to the Cashier page? If so, please enter the passcode:",
                "Cashier Login",
                "");

            if (passcode == "0003")
            {
                MessageBox.Show("Login Success!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                MainFrame.Navigate(new Uri("RoleBasedUI/casier.xaml", UriKind.Relative)); // ඔබ සාදා ඇති නම 'casier' නිසා [cite: 84]
            }
            else if (!string.IsNullOrEmpty(passcode))
            {
                MessageBox.Show("Login Fail! Invalid Passcode.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MainFrame_Navigated(object sender, NavigationEventArgs e)
        {
            // Navigation ක්‍රියාකාරීත්වය සඳහා
        }
    }
}