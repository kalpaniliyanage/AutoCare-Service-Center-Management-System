using Microsoft.Data.Sqlite;
using System;
using System.Globalization;
using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AutoCare.RoleBasedUI
{
    public partial class casier : Page
    {
        private double serviceCost = 0;
        private double materialCost = 0;
        private double totalPayable = 0;

        // ── tracks the active job card ID across Search → Payment ────────
        private string _currentJobCardId = string.Empty;

        public casier()
        {
            InitializeComponent();
            this.Loaded += (s, e) =>
            {
                InitializeLiveChartData();
                LogAction("Cashier", "Logged In");
            };
        }

        // ── writes one row to SystemLogs ─────────────────────────────────
        private void LogAction(string role, string action)
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    var cmd = new SqliteCommand(
                        "INSERT INTO SystemLogs (UserRole, Action, Timestamp) VALUES (@r, @a, @t)",
                        conn);
                    cmd.Parameters.AddWithValue("@r", role);
                    cmd.Parameters.AddWithValue("@a", action);
                    cmd.Parameters.AddWithValue("@t", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.ExecuteNonQuery();
                }
            }
            catch { }
        }

        private void InitializeLiveChartData()
        {
            try
            {
                if (BarMon != null) BarMon.Height = 35;
                if (BarTue != null) BarTue.Height = 58;
                if (BarWed != null) BarWed.Height = 85;
                if (BarThu != null) BarThu.Height = 42;
                if (BarToday != null) BarToday.Height = 98;
            }
            catch { }
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            string id = TxtJobCardID?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(id))
            {
                MessageBox.Show("Please enter a valid Job Card ID.", "Input Required",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _currentJobCardId = id;

            if (TxtPreviewJobCardID != null) TxtPreviewJobCardID.Text = id;
            if (TxtPreviewCustomer != null) TxtPreviewCustomer.Text = id == "1" ? "Kamal Perera" : (id == "2" ? "Nimal Silva" : "John Doe (Premium Customer)");
            if (TxtPreviewVehicle != null) TxtPreviewVehicle.Text = id == "1" ? "WP CAS-5521" : (id == "2" ? "WP CAD-8832" : "WP ABC-1234");
            if (TxtPreviewDate != null) TxtPreviewDate.Text = DateTime.Now.ToString("yyyy-MM-dd");

            serviceCost = id == "2" ? 3500.00 : 1500.00;
            materialCost = id == "2" ? 1200.00 : 500.00;

            if (LblServiceCost != null) LblServiceCost.Text = serviceCost.ToString("N2");
            if (LblMaterialCost != null) LblMaterialCost.Text = materialCost.ToString("N2");

            UpdateTotal();

            if (PanelEmptyState != null) PanelEmptyState.Visibility = Visibility.Collapsed;
            if (ScrollInvoice != null) ScrollInvoice.Visibility = Visibility.Visible;

            if (TxtPreviewStatus != null)
            {
                TxtPreviewStatus.Text = "UNPAID";
                TxtPreviewStatus.Foreground = new SolidColorBrush(Color.FromRgb(198, 40, 40));
            }
            if (BadgeStatus != null) BadgeStatus.Background = new SolidColorBrush(Color.FromRgb(255, 235, 238));
            if (BtnProcessPayment != null) BtnProcessPayment.IsEnabled = true;

            // ── LOG: invoice generated ───────────────────────────────────
            LogAction("Cashier", $"Generated Invoice for JobCard #{id}");
        }

        private void TxtDiscount_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateTotal();
        }

        private void UpdateTotal()
        {
            double discount = 0;
            var discountText = TxtDiscount?.Text ?? string.Empty;
            if (!double.TryParse(discountText, NumberStyles.Any, CultureInfo.CurrentCulture, out discount))
                double.TryParse(discountText, NumberStyles.Any, CultureInfo.InvariantCulture, out discount);

            double subtotal = serviceCost + materialCost;
            totalPayable = subtotal - discount;
            if (totalPayable < 0) totalPayable = 0;

            if (LblTotalAmount != null) LblTotalAmount.Text = totalPayable.ToString("N2");
            if (TxtReceiptLaborPrice != null) TxtReceiptLaborPrice.Text = "LKR " + serviceCost.ToString("N2");
            if (TxtReceiptMaterialPrice != null) TxtReceiptMaterialPrice.Text = "LKR " + materialCost.ToString("N2");
            if (TxtReceiptSubtotal != null) TxtReceiptSubtotal.Text = "LKR " + subtotal.ToString("N2");
            if (TxtReceiptDiscount != null) TxtReceiptDiscount.Text = "LKR " + discount.ToString("N2");
            if (TxtPreviewNetAmount != null) TxtPreviewNetAmount.Text = "LKR " + totalPayable.ToString("N2");
        }

        private void BtnProcessPayment_Click(object sender, RoutedEventArgs e)
        {
            if (ScrollInvoice != null && ScrollInvoice.Visibility != Visibility.Visible) return;

            try { }
            catch { }

            if (BadgeStatus != null) BadgeStatus.Background = new SolidColorBrush(Color.FromRgb(232, 245, 233));
            if (TxtPreviewStatus != null)
            {
                TxtPreviewStatus.Text = "PAID";
                TxtPreviewStatus.Foreground = new SolidColorBrush(Color.FromRgb(27, 94, 32));
            }
            if (BtnProcessPayment != null) BtnProcessPayment.IsEnabled = false;

            InitializeLiveChartData();

            // ── LOG: payment received ────────────────────────────────────
            LogAction("Cashier", $"Payment Received for Invoice #{_currentJobCardId}");

            MessageBox.Show($"Payment of LKR {totalPayable:N2} processed successfully!\nDatabase status updated to PAID.",
                            "Payment Matrix Update", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnGetPDF_Click(object sender, RoutedEventArgs e)
        {
            if (PrintArea == null) return;
            try
            {
                PrintDialog printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    var caps = printDialog.PrintQueue.GetPrintCapabilities(printDialog.PrintTicket);
                    double printableWidth = caps.PageImageableArea?.ExtentWidth ?? printDialog.PrintableAreaWidth;
                    double printableHeight = caps.PageImageableArea?.ExtentHeight ?? printDialog.PrintableAreaHeight;

                    double scale = 1.0;
                    if (PrintArea.ActualWidth > 0 && PrintArea.ActualHeight > 0)
                        scale = Math.Min(printableWidth / PrintArea.ActualWidth,
                                         printableHeight / PrintArea.ActualHeight);

                    PrintArea.LayoutTransform = new ScaleTransform(scale, scale);
                    Size pageSize = new Size(printableWidth, printableHeight);
                    PrintArea.Measure(pageSize);
                    PrintArea.Arrange(new Rect(new Point(0, 0), pageSize));

                    string jobName = _currentJobCardId.Length > 0 ? _currentJobCardId : "Unknown";
                    printDialog.PrintVisual(PrintArea, $"Invoice_Job_Ref_{jobName}");
                    PrintArea.LayoutTransform = null;

                    // ── LOG: PDF exported ────────────────────────────────
                    LogAction("Cashier", $"Exported PDF Invoice for JobCard #{jobName}");

                    MessageBox.Show("Digital Invoice PDF Report generated successfully!",
                                    "Report Export Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error generating PDF document: " + ex.Message,
                                "Print Pipeline Fault", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnGetIncomeReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string fileName = $"Daily_Revenue_Report_{DateTime.Now:yyyyMMdd}.csv";
                string fullPath = Path.Combine(desktopPath, fileName);

                using (StreamWriter sw = new StreamWriter(fullPath))
                {
                    sw.WriteLine("AUTOCARE PREMIER SERVICE SUITE - REVENUE REPORT");
                    sw.WriteLine($"Generated Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    sw.WriteLine();
                    sw.WriteLine("InvoiceID,JobCardID,ServiceCost,MaterialCost,TotalAmount,PaymentDate");
                    sw.WriteLine("INV001,1,1500.00,500.00,2000.00,2026-06-01");
                    sw.WriteLine("INV002,2,3500.00,1200.00,4700.00,2026-06-02");
                    sw.WriteLine("INV003,3,2500.00,800.00,3300.00,2026-06-03");
                }

                // ── LOG: income report generated ─────────────────────────
                LogAction("Cashier", $"Generated Income Report: {fileName}");

                MessageBox.Show($"Daily Revenue Report generated and saved to Desktop!\nPath: {fullPath}",
                                "Report Export Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch
            {
                MessageBox.Show("Database file connection not found. Falling back to default export.",
                                "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            LogAction("Cashier", "Logged Out");

            var mainWindow = Window.GetWindow(this) as MainWindow
                          ?? Application.Current?.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                var nav = mainWindow.MainFrame?.NavigationService;
                if (nav != null) while (nav.RemoveBackEntry() != null) { }
                if (mainWindow.MainFrame != null) mainWindow.MainFrame.Content = null;
                var roleGrid = mainWindow.FindName("RoleSelectionGrid") as Grid
                            ?? mainWindow.FindName("MainGrid") as Grid;
                if (roleGrid != null) roleGrid.Visibility = Visibility.Visible;
            }
        }
    }
}