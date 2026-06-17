using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace AutoCare
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // Set live date in nav bar
            txtDate.Text = DateTime.Now.ToString("dddd, d MMMM yyyy");
        }

        // ── Shared login helper ───────────────────────────────────────────
        // Shows a styled dark-green passcode dialog.
        // Returns the string the user typed, or null if they cancelled.
        private string? ShowPasscodeDialog(string roleName)
        {
            // ── Outer window ─────────────────────────────────────────────
            var dialog = new Window
            {
                Title = $"{roleName} Login",
                Width = 420,
                Height = 280,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.None,          // frameless
                Background = System.Windows.Media.Brushes.Transparent,
                AllowsTransparency = true,
            };

            // ── Root border (rounded, dark) ──────────────────────────────
            var root = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(
                                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter
                                    .ConvertFromString("#111F11")),
                CornerRadius = new CornerRadius(18),
                BorderBrush = new System.Windows.Media.SolidColorBrush(
                                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter
                                    .ConvertFromString("#2A4A2A")),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(32, 28, 32, 28),
            };

            var stack = new StackPanel();
            root.Child = stack;
            dialog.Content = root;

            // ── Icon + Title row ─────────────────────────────────────────
            var titleRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 6)
            };

            var iconBadge = new Border
            {
                Width = 42,
                Height = 42,
                CornerRadius = new CornerRadius(12),
                Background = new System.Windows.Media.SolidColorBrush(
                                      (System.Windows.Media.Color)System.Windows.Media.ColorConverter
                                      .ConvertFromString("#0D2B1A")),
                BorderBrush = new System.Windows.Media.SolidColorBrush(
                                      (System.Windows.Media.Color)System.Windows.Media.ColorConverter
                                      .ConvertFromString("#166534")),
                BorderThickness = new Thickness(1.5),
                Margin = new Thickness(0, 0, 14, 0),
                Child = new TextBlock
                {
                    Text = "🔐",
                    FontSize = 22,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                }
            };
            titleRow.Children.Add(iconBadge);

            var titleBlock = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titleBlock.Children.Add(new TextBlock
            {
                Text = roleName,
                FontSize = 17,
                FontWeight = FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush(
                                 (System.Windows.Media.Color)System.Windows.Media.ColorConverter
                                 .ConvertFromString("#F0FDF4")),
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            });
            titleBlock.Children.Add(new TextBlock
            {
                Text = "Enter your passcode to continue",
                FontSize = 12,
                Foreground = new System.Windows.Media.SolidColorBrush(
                                 (System.Windows.Media.Color)System.Windows.Media.ColorConverter
                                 .ConvertFromString("#86EFAC")),
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                Margin = new Thickness(0, 3, 0, 0),
            });
            titleRow.Children.Add(titleBlock);
            stack.Children.Add(titleRow);

            // ── Divider ──────────────────────────────────────────────────
            stack.Children.Add(new Border
            {
                Height = 1,
                Margin = new Thickness(0, 14, 0, 18),
                Background = new System.Windows.Media.LinearGradientBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4ADE80"),
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#111F11"),
                    0.0)
            });

            // ── Label ────────────────────────────────────────────────────
            stack.Children.Add(new TextBlock
            {
                Text = "PASSCODE",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush(
                                 (System.Windows.Media.Color)System.Windows.Media.ColorConverter
                                 .ConvertFromString("#4ADE80")),
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                Margin = new Thickness(0, 0, 0, 6),
            });

            // ── PasswordBox ──────────────────────────────────────────────
            var pwBox = new PasswordBox
            {
                Height = 44,
                FontSize = 18,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                Foreground = new System.Windows.Media.SolidColorBrush(
                                  (System.Windows.Media.Color)System.Windows.Media.ColorConverter
                                  .ConvertFromString("#F0FDF4")),
                Background = new System.Windows.Media.SolidColorBrush(
                                  (System.Windows.Media.Color)System.Windows.Media.ColorConverter
                                  .ConvertFromString("#0A1A0A")),
                BorderBrush = new System.Windows.Media.SolidColorBrush(
                                  (System.Windows.Media.Color)System.Windows.Media.ColorConverter
                                  .ConvertFromString("#2A4A2A")),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 0, 12, 0),
                PasswordChar = '●',
                MaxLength = 10,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20),
            };
            stack.Children.Add(pwBox);

            // ── Button row ───────────────────────────────────────────────
            var btnRow = new Grid();
            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Cancel button
            var btnCancel = new Button
            {
                Content = "Cancel",
                Height = 42,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                Foreground = new System.Windows.Media.SolidColorBrush(
                                      (System.Windows.Media.Color)System.Windows.Media.ColorConverter
                                      .ConvertFromString("#86EFAC")),
                Background = new System.Windows.Media.SolidColorBrush(
                                      (System.Windows.Media.Color)System.Windows.Media.ColorConverter
                                      .ConvertFromString("#0D2B1A")),
                BorderBrush = new System.Windows.Media.SolidColorBrush(
                                      (System.Windows.Media.Color)System.Windows.Media.ColorConverter
                                      .ConvertFromString("#2A4A2A")),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            btnCancel.Click += (_, _) =>
            {
                dialog.Tag = null;
                dialog.Close();
            };
            Grid.SetColumn(btnCancel, 0);
            btnRow.Children.Add(btnCancel);

            // Login button
            var btnLogin = new Button
            {
                Content = "🔓  Login",
                Height = 42,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                Foreground = new System.Windows.Media.SolidColorBrush(
                                      (System.Windows.Media.Color)System.Windows.Media.ColorConverter
                                      .ConvertFromString("#052E16")),
                Background = new System.Windows.Media.SolidColorBrush(
                                      (System.Windows.Media.Color)System.Windows.Media.ColorConverter
                                      .ConvertFromString("#4ADE80")),
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            btnLogin.Click += (_, _) =>
            {
                dialog.Tag = pwBox.Password;
                dialog.Close();
            };
            // Also allow Enter key in password box
            pwBox.KeyDown += (_, ke) =>
            {
                if (ke.Key == System.Windows.Input.Key.Enter)
                {
                    dialog.Tag = pwBox.Password;
                    dialog.Close();
                }
            };
            Grid.SetColumn(btnLogin, 2);
            btnRow.Children.Add(btnLogin);

            stack.Children.Add(btnRow);

            // Allow dragging the frameless window
            root.MouseLeftButtonDown += (_, me) => dialog.DragMove();

            dialog.ShowDialog();

            return dialog.Tag as string;
        }

        // ── Shared navigate helper ────────────────────────────────────────
        private void TryLogin(string roleName,
                              string correctCode,
                              string xamlPath)
        {
            string? entered = ShowPasscodeDialog(roleName);

            if (entered == null)
                return;   // user cancelled — do nothing

            if (entered == correctCode)
            {
                ShowToast("✅  Login successful — welcome!");
                MainFrame.Navigate(new Uri(xamlPath, UriKind.Relative));
            }
            else
            {
                MessageBox.Show(
                    "Incorrect passcode. Please try again.",
                    "Access Denied",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        // ── Lightweight toast using a styled MessageBox ───────────────────
        private static void ShowToast(string message)
        {
            MessageBox.Show(
                message,
                "AutoCare",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // ── Button handlers ───────────────────────────────────────────────

        private void BtnAdmin_Click(object sender, RoutedEventArgs e)
            => TryLogin("System Administrator", "0000",
                        "RoleBasedUI/SystemAdministrator.xaml");

        private void BtnReceptionist_Click(object sender, RoutedEventArgs e)
            => TryLogin("Receptionist", "0001",
                        "RoleBasedUI/Receptionist.xaml");

        private void BtnServiceManager_Click(object sender, RoutedEventArgs e)
            => TryLogin("Service Manager", "0002",
                        "RoleBasedUI/ServiceManager.xaml");

        private void BtnCashier_Click(object sender, RoutedEventArgs e)
            => TryLogin("Cashier / Accountant", "0003",
                        "RoleBasedUI/casier.xaml");

        private void MainFrame_Navigated(object sender, NavigationEventArgs e)
        {
            // Clear back-stack so users can't navigate back with browser buttons
            while (MainFrame.NavigationService.RemoveBackEntry() != null) { }
        }
    }
}
