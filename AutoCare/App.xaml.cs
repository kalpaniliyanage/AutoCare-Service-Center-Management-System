using System;
using System.Windows;

namespace AutoCare
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // 🚀 මෙන්න මේ පේළියෙන් තමයි අර cs ෆයිල් එකට කතා කරලා ටේබල් 7 සහ දත්ත 50 ඔටෝ හදන්නේ!
                DatabaseHelper.InitializeDatabase();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Issue occurred: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}