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
    public partial class Receptionist : Page
    {
        public Receptionist()
        {
            InitializeComponent();
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. MainWindow එක අලුත් Object එකක් විදිහට නිර්මාණය කර විවෘත කිරීම
            MainWindow newMainWindow = new MainWindow();
            newMainWindow.Show();

            // 2. දැනට මේ පිටුව විවෘත වෙලා තියෙන පැරණි Window එක වසා දැමීම
            Window parentWindow = Window.GetWindow(this);
            if (parentWindow != null)
            {
                parentWindow.Close();
            }
        }
    }
}