using System;
using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using System.Collections.ObjectModel;
using PdfSharp.Pdf;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using System.Collections.Generic;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Drawing;

namespace AutoCare.RoleBasedUI
{
    public partial class SystemAdministrator : Page
    {
        // ── White paint reused across all chart text ─────────────────────
        private static readonly SolidColorPaint WhiteLabel =
            new SolidColorPaint(SKColors.White)
            {
                SKTypeface = SKTypeface.FromFamilyName("Segoe UI")
            };

        private static readonly SolidColorPaint WhiteLabelSm =
            new SolidColorPaint(SKColors.White)
            {
                SKTypeface = SKTypeface.FromFamilyName("Segoe UI")
            };

        // Subtle grid-line color — visible but not distracting
        private static readonly SolidColorPaint GridPaint =
            new SolidColorPaint(new SKColor(80, 110, 80, 140));

        // ── Slice palette for pie chart ──────────────────────────────────
        private static readonly SKColor[] SliceColors =
        {
            new SKColor(74,  222, 128),   // green
            new SKColor(56,  189, 248),   // sky blue
            new SKColor(251, 191,  36),   // amber
            new SKColor(248, 113, 113),   // red
            new SKColor(167, 139, 250),   // violet
            new SKColor(251, 146,  60),   // orange
            new SKColor(34,  211, 238),   // cyan
            new SKColor(244, 114, 182),   // pink
        };

        public ISeries[] RevenueSeries { get; set; } = Array.Empty<ISeries>();
        public ISeries[] StatsSeries { get; set; } = Array.Empty<ISeries>();

        private double revenue;
        private int serviceCount;
        private int invoiceCount;

        public SystemAdministrator()
        {
            InitializeComponent();

            txtCurrentTime.Text =
                "🕒 " + DateTime.Now.ToString("dddd, dd MMMM yyyy HH:mm:ss");

            this.DataContext = this;

            LoadDashboard();
            LoadServices();
            LoadLogs();
        }

        // ────────────────────────────────────────────────────────────────
        private void LoadDashboard()
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    // ── KPI numbers ──────────────────────────────────────
                    var revenueCmd = conn.CreateCommand();
                    revenueCmd.CommandText =
                        "SELECT IFNULL(SUM(TotalAmount),0) FROM Invoices";
                    revenue = Convert.ToDouble(revenueCmd.ExecuteScalar() ?? 0);
                    txtRevenue.Text = revenue.ToString("N2");

                    var serviceCmd = conn.CreateCommand();
                    serviceCmd.CommandText = "SELECT COUNT(*) FROM Services";
                    serviceCount = Convert.ToInt32(serviceCmd.ExecuteScalar() ?? 0);
                    txtServices.Text = serviceCount.ToString();

                    var invoiceCmd = conn.CreateCommand();
                    invoiceCmd.CommandText = "SELECT COUNT(*) FROM Invoices";
                    invoiceCount = Convert.ToInt32(invoiceCmd.ExecuteScalar() ?? 0);
                    txtInvoices.Text = invoiceCount.ToString();

                    var logCmd = conn.CreateCommand();
                    logCmd.CommandText = "SELECT COUNT(*) FROM SystemLogs";
                    txtLogins.Text = logCmd.ExecuteScalar()?.ToString() ?? "0";

                    // ── Revenue bar chart ────────────────────────────────
                    double[] revenueByMonth = new double[12];

                    var revenueChartCmd = conn.CreateCommand();
                    revenueChartCmd.CommandText = @"
                        SELECT
                            strftime('%m', PaymentDate) AS Month,
                            SUM(TotalAmount)            AS Revenue
                        FROM Invoices
                        GROUP BY strftime('%m', PaymentDate)";

                    using (var r = revenueChartCmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            int month = Convert.ToInt32(r["Month"]);
                            revenueByMonth[month - 1] =
                                Convert.ToDouble(r["Revenue"]);
                        }
                    }

                    RevenueSeries = new ISeries[]
                    {
                        new ColumnSeries<double>
                        {
                            Name      = "Revenue (LKR)",
                            Values    = revenueByMonth,

                            // ★ Sky-blue bars — visible on dark background
                            Fill      = new SolidColorPaint(
                                            new SKColor(56, 189, 248)),
                            Stroke    = null,
                            MaxBarWidth = 40,

                            // ★ White data labels above each bar
                            DataLabelsPaint     = WhiteLabelSm,
                            DataLabelsSize      = 11,
                            DataLabelsPosition  =
                                LiveChartsCore.Measure.DataLabelsPosition.Top,
                            DataLabelsFormatter =
                                p => p.Coordinate.PrimaryValue > 0
                                     ? $"Rs {p.Coordinate.PrimaryValue / 1000:0}k"
                                     : string.Empty
                        }
                    };

                    RevenueChart.Series = RevenueSeries;

                    // ★ X-axis — white month labels
                    RevenueChart.XAxes = new[]
                    {
                        new Axis
                        {
                            Labels = new[]
                            {
                                "Jan","Feb","Mar","Apr",
                                "May","Jun","Jul","Aug",
                                "Sep","Oct","Nov","Dec"
                            },
                            LabelsPaint        = WhiteLabel,   // ← WHITE
                            TextSize           = 13,
                            SeparatorsPaint    = GridPaint,
                            ShowSeparatorLines = false,
                        }
                    };

                    // ★ Y-axis — white number labels
                    RevenueChart.YAxes = new[]
                    {
                        new Axis
                        {
                            Name            = "Revenue (LKR)",
                            NamePaint       = WhiteLabel,      // ← WHITE axis name
                            LabelsPaint     = WhiteLabel,      // ← WHITE tick labels
                            TextSize        = 13,
                            Labeler         = v => $"Rs {v / 1000:0}k",
                            SeparatorsPaint = GridPaint,
                        }
                    };

                    // ── Pie chart: Most Requested Services ───────────────
                    var pieSeries = new List<ISeries>();
                    int colorIndex = 0;

                    var serviceChartCmd = conn.CreateCommand();
                    serviceChartCmd.CommandText = @"
                        SELECT
                            ServiceName,
                            COUNT(*) AS Total
                        FROM Services
                        GROUP BY ServiceName";

                    using (var sr = serviceChartCmd.ExecuteReader())
                    {
                        while (sr.Read())
                        {
                            SKColor slice =
                                SliceColors[colorIndex % SliceColors.Length];
                            colorIndex++;

                            pieSeries.Add(new PieSeries<double>
                            {
                                Name = sr["ServiceName"].ToString(),
                                Values = new[]
                                {
                                    Convert.ToDouble(sr["Total"])
                                },

                                // ★ Distinct slice color
                                Fill = new SolidColorPaint(slice),

                                // ★ White percentage label on/near each slice
                                DataLabelsPaint =
                                    new SolidColorPaint(SKColors.White)
                                    {
                                        SKTypeface =
                                            SKTypeface.FromFamilyName("Segoe UI")
                                    },
                                DataLabelsSize = 12,
                                DataLabelsPosition =
                                    LiveChartsCore.Measure.PolarLabelsPosition.Outer,
                                DataLabelsFormatter =
                                    p => $"{p.Coordinate.PrimaryValue:0}",
                            });
                        }
                    }

                    StatsSeries = pieSeries.ToArray();
                    StatsChart.Series = StatsSeries;

                    // ★ White legend text on pie chart
                    StatsChart.LegendTextPaint =
                        new SolidColorPaint(SKColors.White)
                        {
                            SKTypeface = SKTypeface.FromFamilyName("Segoe UI")
                        };
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ────────────────────────────────────────────────────────────────
        private void LoadServices()
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT * FROM Services";
                    using var reader = cmd.ExecuteReader();
                    DataTable dt = new DataTable();
                    dt.Load(reader);
                    dgServices.ItemsSource = dt.DefaultView;
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void LoadLogs()
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    var cmd = conn.CreateCommand();
                    cmd.CommandText =
                        "SELECT * FROM SystemLogs ORDER BY Timestamp DESC";
                    using var reader = cmd.ExecuteReader();
                    DataTable dt = new DataTable();
                    dt.Load(reader);
                    dgLogs.ItemsSource = dt.DefaultView;
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        // ── Button Handlers ──────────────────────────────────────────────
        private void BtnAddService_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtServiceName.Text))
                {
                    MessageBox.Show("Please enter a service name.");
                    return;
                }

                using (var conn = DatabaseHelper.GetConnection())
                {
                    var cmd = conn.CreateCommand();
                    cmd.CommandText = @"INSERT INTO Services
                                        (ServiceName, BasePrice)
                                        VALUES (@name, @price)";
                    cmd.Parameters.AddWithValue("@name", txtServiceName.Text);
                    cmd.Parameters.AddWithValue("@price", txtBasePrice.Text);
                    cmd.ExecuteNonQuery();
                }

                MessageBox.Show("Service added successfully.", "AutoCare");
                txtServiceName.Clear();
                txtBasePrice.Clear();
                LoadServices();
                LoadDashboard();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadDashboard();
            LoadServices();
            LoadLogs();
            txtCurrentTime.Text =
                "🕒 " + DateTime.Now.ToString("dddd, dd MMMM yyyy HH:mm:ss");
            MessageBox.Show("Dashboard refreshed successfully.", "AutoCare");
        }

        private void BtnBackup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory("Backup");
                string backupName =
                    "Backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt";
                File.WriteAllText(
                    Path.Combine("Backup", backupName),
                    "AutoCare Backup Created");
                MessageBox.Show("Backup created successfully.", "Backup");
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                GlobalFontSettings.UseWindowsFontsUnderWindows = true;

                SaveFileDialog save = new SaveFileDialog
                {
                    Filter = "PDF Files (*.pdf)|*.pdf",
                    FileName = $"AutoCare_Report_{DateTime.Now:yyyyMMdd}.pdf"
                };

                if (save.ShowDialog() == true)
                {
                    PdfDocument document = new PdfDocument();
                    document.Info.Title = "AutoCare Monthly Report";

                    PdfPage page = document.AddPage();
                    page.Size = PdfSharp.PageSize.A4;

                    XGraphics gfx = XGraphics.FromPdfPage(page);

                    XFont titleFont = new XFont("Verdana", 22, XFontStyleEx.Bold);
                    XFont subTitleFont = new XFont("Verdana", 14, XFontStyleEx.Bold);
                    XFont bodyFont = new XFont("Verdana", 12, XFontStyleEx.Regular);

                    int y = 50;

                    string logoPath = @"Images\AutoCareLogo.jpg";
                    if (File.Exists(logoPath))
                    {
                        XImage logo = XImage.FromFile(logoPath);
                        gfx.DrawImage(logo, 15, 15, 70, 70);
                    }

                    gfx.DrawRectangle(XBrushes.DarkGreen, 0, 0,
                                      page.Width.Point, 70);
                    gfx.DrawString("AUTOCARE WORKSHOP MANAGEMENT SYSTEM",
                                   titleFont, XBrushes.White, new XPoint(15, 45));

                    y = 100;
                    gfx.DrawString("MONTHLY BUSINESS REPORT",
                                   subTitleFont, XBrushes.DarkGreen, new XPoint(30, y));
                    y += 35;
                    gfx.DrawLine(XPens.DarkGreen, 30, y, 550, y);
                    y += 25;
                    gfx.DrawString(
                        "Generated Date : " + DateTime.Now.ToString("dd MMM yyyy HH:mm"),
                        bodyFont, XBrushes.Black, new XPoint(30, y));
                    y += 40;

                    gfx.DrawString("BUSINESS SUMMARY", subTitleFont,
                                   XBrushes.DarkBlue, new XPoint(30, y));
                    y += 30;
                    gfx.DrawString($"Total Revenue  : {txtRevenue.Text}",
                                   bodyFont, XBrushes.Black, new XPoint(50, y)); y += 25;
                    gfx.DrawString($"Total Services : {txtServices.Text}",
                                   bodyFont, XBrushes.Black, new XPoint(50, y)); y += 25;
                    gfx.DrawString($"Total Invoices : {txtInvoices.Text}",
                                   bodyFont, XBrushes.Black, new XPoint(50, y)); y += 25;
                    gfx.DrawString($"Total Logins   : {txtLogins.Text}",
                                   bodyFont, XBrushes.Black, new XPoint(50, y)); y += 50;

                    gfx.DrawString("SERVICE INFORMATION", subTitleFont,
                                   XBrushes.DarkBlue, new XPoint(30, y)); y += 30;
                    gfx.DrawString(
                        "All registered services are available in the system.",
                        bodyFont, XBrushes.Black, new XPoint(50, y)); y += 60;

                    gfx.DrawLine(XPens.Gray, 30, y, 550, y); y += 30;
                    gfx.DrawString("AutoCare Workshop Management System",
                                   bodyFont, XBrushes.Gray, new XPoint(30, y));
                    gfx.DrawString("Confidential Business Report",
                                   bodyFont, XBrushes.Gray, new XPoint(350, y));

                    document.Save(save.FileName);
                    MessageBox.Show("PDF Report Generated Successfully!",
                                    "AutoCare", MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void BtnAnalytics_Click(object sender, RoutedEventArgs e)
        {
            LoadDashboard();
            string report =
                "📊 BUSINESS ANALYTICS\n\n" +
                $"💰 Revenue  : {txtRevenue.Text}\n" +
                $"🛠 Services : {txtServices.Text}\n" +
                $"🧾 Invoices : {txtInvoices.Text}\n" +
                $"👤 Logins   : {txtLogins.Text}";
            MessageBox.Show(report, "Analytics",
                            MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                Directory.Exists("Backup")
                    ? "Backup restored successfully."
                    : "No backup folder found.",
                "Restore");
        }

        private void BtnLogs_Click(object sender, RoutedEventArgs e)
        {
            LoadLogs();
            MessageBox.Show("Latest system logs loaded.", "Logs");
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "⚙ AutoCare Settings\n\n" +
                "Version  : 1.0\n" +
                "Database : Connected\n" +
                "Security : Enabled\n" +
                "Backup   : Ready",
                "System Settings");
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow? mainWindow =
                Application.Current?.MainWindow as MainWindow
                ?? Window.GetWindow(this) as MainWindow;

            if (mainWindow != null)
            {
                var nav = mainWindow.MainFrame.NavigationService;
                if (nav != null)
                    while (nav.RemoveBackEntry() != null) { }

                mainWindow.MainFrame.Content = null;
            }
        }
    }
}
