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
    /// <summary>
    /// Interaction logic for ServiceManager.xaml
    /// </summary>
    public partial class ServiceManager : Page
    {
        public ServiceManager()
        {
            InitializeComponent();
        }

        private void BtnAddItem_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Add item to inventory logic
            MessageBox.Show("Add Item button clicked!", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Import CSV logic
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Export CSV logic
        }
    }
}