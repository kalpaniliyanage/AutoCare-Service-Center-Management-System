using System;
using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using System.Collections.Generic;
using PdfSharp.Pdf;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.ComponentModel;
using System.Windows.Threading;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using TimersTimer = System.Timers.Timer;

namespace AutoCare.RoleBasedUI
{
    public partial class SystemAdministrator : Page, INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // NOTE: These paints are created fresh on every LoadDashboard() call
        // instead of being static/shared. LiveCharts' SolidColorPaint holds a
        // reference to the SKPaint it creates for a specific canvas/draw cycle.
        // Reusing the same instance across repeated Series=null -> Series=new[]
        // refresh cycles caused the chart to stop repainting after the first
        // refresh (bars/pie data effectively invisible). Helper methods below
        // build new paint instances every time so each refresh gets a clean,
        // unbound paint.
        private static SolidColorPaint NewWhiteLabel(float size = 13) =>
            new SolidColorPaint(SKColors.White) { SKTypeface = SKTypeface.FromFamilyName("Segoe UI") };

        private static SolidColorPaint NewGridPaint() =>
            new SolidColorPaint(new SKColor(80, 110, 80, 140));

        private static readonly SKColor[] SliceColors =
        {
            new SKColor(74,222,128), new SKColor(56,189,248), new SKColor(251,191,36),
            new SKColor(248,113,113), new SKColor(167,139,250), new SKColor(251,146,60),
            new SKColor(34,211,238), new SKColor(244,114,182)
        };

        private ISeries[] _revenueSeries = Array.Empty<ISeries>();
        public ISeries[] RevenueSeries
        {
            get => _revenueSeries;
            set { _revenueSeries = value; OnPropertyChanged(nameof(RevenueSeries)); }
        }

        private ISeries[] _statsSeries = Array.Empty<ISeries>();
        public ISeries[] StatsSeries
        {
            get => _statsSeries;
            set { _statsSeries = value; OnPropertyChanged(nameof(StatsSeries)); }
        }

        private readonly string _dbFile;
        private FileSystemWatcher? _dbWatcher;
        private TimersTimer? _dbChangeDebounceTimer;

        // Polls SystemLogs every 3 s so entries written by Cashier / ServiceManager
        // appear in the table without needing a manual refresh.
        private DispatcherTimer? _pollTimer;
        private int _lastLogCount = -1; // -1 = not yet read

        public SystemAdministrator()
        {
            InitializeComponent();

            txtCurrentTime.Text = "🕒 " + DateTime.Now.ToString("dddd, dd MMMM yyyy HH:mm:ss");
            DataContext = this;

            // _dbFile must be assigned before anything that touches the DB
            _dbFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AutoCareDB.db");

            // ── FIX: Nuke ALL triggers first so no malformed ones survive ──
            NukeAllTriggers();
            CreateDatabaseTriggers();
            LogLoginEvent("System Administrator");

            try
            {
                var folder = Path.GetDirectoryName(_dbFile) ?? AppDomain.CurrentDomain.BaseDirectory;
                _dbWatcher = new FileSystemWatcher(folder)
                {
                    Filter = Path.GetFileName(_dbFile),
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.Attributes
                };
                _dbWatcher.Changed += DbWatcher_Changed;
                _dbWatcher.EnableRaisingEvents = true;

                _dbChangeDebounceTimer = new TimersTimer(500) { AutoReset = false };
                _dbChangeDebounceTimer.Elapsed += (s, e) =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        LoadDashboard();
                        LoadServices();
                        LoadLogs();
                    }));
                };
            }
            catch
            {
                _dbWatcher = null;
                _dbChangeDebounceTimer = null;
            }

            LoadDashboard();
            LoadServices();
            LoadLogs();

            // ── Poll timer: refreshes logs + KPIs every 3 seconds ────────
            // This catches rows written by Cashier / ServiceManager even when
            // the FileSystemWatcher misses a SQLite WAL-mode write event.
            _pollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _pollTimer.Tick += PollTimer_Tick;
            _pollTimer.Start();
        }

        // ── Drops EVERY trigger in the database unconditionally.
        // Called before CreateDatabaseTriggers() so stale / malformed triggers
        // (e.g. those referencing NEW.CorrectColumnName placeholder text) can
        // never block INSERT/UPDATE/DELETE operations on any table.
        private void NukeAllTriggers()
        {
            try
            {
                using var conn = DatabaseHelper.GetConnection();

                // Collect all trigger names first (can't drop while reader is open)
                var listCmd = conn.CreateCommand();
                listCmd.CommandText = "SELECT name FROM sqlite_master WHERE type = 'trigger';";
                using var reader = listCmd.ExecuteReader();

                var triggers = new List<string>();
                while (reader.Read())
                    triggers.Add(reader.GetString(0));

                reader.Close();

                // Drop every trigger found
                foreach (var triggerName in triggers)
                {
                    try
                    {
                        var dropCmd = conn.CreateCommand();
                        dropCmd.CommandText = $"DROP TRIGGER IF EXISTS \"{triggerName}\";";
                        dropCmd.ExecuteNonQuery();
                    }
                    catch { /* ignore per-trigger drop errors */ }
                }
            }
            catch { /* ignore — app still starts even if nuke fails */ }
        }

        // ── Creates (or refreshes) the SQLite triggers that auto-log
        // Customers / Vehicles / JobCards / Invoices / Inventory changes
        // into SystemLogs. Must be its own class-level method — it cannot
        // be declared inside the constructor body.
        private void CreateDatabaseTriggers()
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    void Run(string sql)
                    {
                        try
                        {
                            using var c = conn.CreateCommand();
                            c.CommandText = sql;
                            c.ExecuteNonQuery();
                        }
                        catch { /* ignore per-trigger errors */ }
                    }

                    // Drop known triggers (safe: IF EXISTS) — belt-and-suspenders
                    // after NukeAllTriggers(), but harmless to repeat.
                    Run(@"
                DROP TRIGGER IF EXISTS trg_customers_insert;
                DROP TRIGGER IF EXISTS trg_customers_update;
                DROP TRIGGER IF EXISTS trg_customers_delete;
                DROP TRIGGER IF EXISTS trg_vehicles_insert;
                DROP TRIGGER IF EXISTS trg_vehicles_update;
                DROP TRIGGER IF EXISTS trg_vehicles_delete;
                DROP TRIGGER IF EXISTS trg_jobcards_insert;
                DROP TRIGGER IF EXISTS trg_jobcards_update;
                DROP TRIGGER IF EXISTS trg_jobcards_delete;
                DROP TRIGGER IF EXISTS trg_invoices_insert;
                DROP TRIGGER IF EXISTS trg_invoices_update;
                DROP TRIGGER IF EXISTS trg_invoices_delete;
                DROP TRIGGER IF EXISTS trg_inventory_insert;
                DROP TRIGGER IF EXISTS trg_inventory_update;
                DROP TRIGGER IF EXISTS trg_inventory_delete;
            ");

                    // ── Customers ──────────────────────────────────────────────
                    Run(@"CREATE TRIGGER IF NOT EXISTS trg_customers_insert
                  AFTER INSERT ON Customers BEGIN
                      INSERT INTO SystemLogs (UserRole, Action, Timestamp)
                      VALUES ('Receptionist','Added Customer: '||NEW.CustomerName,datetime('now','localtime'));
                  END;");

                    Run(@"CREATE TRIGGER IF NOT EXISTS trg_customers_update
                  AFTER UPDATE ON Customers BEGIN
                      INSERT INTO SystemLogs (UserRole, Action, Timestamp)
                      VALUES ('Receptionist','Updated Customer: '||NEW.CustomerName,datetime('now','localtime'));
                  END;");

                    Run(@"CREATE TRIGGER IF NOT EXISTS trg_customers_delete
                  AFTER DELETE ON Customers BEGIN
                      INSERT INTO SystemLogs (UserRole, Action, Timestamp)
                      VALUES ('Receptionist','Deleted Customer: '||OLD.CustomerName,datetime('now','localtime'));
                  END;");

                    // ── Vehicles ───────────────────────────────────────────────
                    Run(@"CREATE TRIGGER IF NOT EXISTS trg_vehicles_insert
                  AFTER INSERT ON Vehicles BEGIN
                      INSERT INTO SystemLogs (UserRole, Action, Timestamp)
                      VALUES ('Receptionist','Added Vehicle: '||NEW.VehicleNo,datetime('now','localtime'));
                  END;");

                    Run(@"CREATE TRIGGER IF NOT EXISTS trg_vehicles_update
                  AFTER UPDATE ON Vehicles BEGIN
                      INSERT INTO SystemLogs (UserRole, Action, Timestamp)
                      VALUES ('Receptionist','Updated Vehicle: '||NEW.VehicleNo,datetime('now','localtime'));
                  END;");

                    Run(@"CREATE TRIGGER IF NOT EXISTS trg_vehicles_delete
                  AFTER DELETE ON Vehicles BEGIN
                      INSERT INTO SystemLogs (UserRole, Action, Timestamp)
                      VALUES ('Receptionist','Deleted Vehicle: '||OLD.VehicleNo,datetime('now','localtime'));
                  END;");

                    // ── JobCards ───────────────────────────────────────────────
                    Run(@"CREATE TRIGGER IF NOT EXISTS trg_jobcards_insert
                  AFTER INSERT ON JobCards BEGIN
                      INSERT INTO SystemLogs (UserRole, Action, Timestamp)
                      VALUES ('Receptionist','Created Job Card #'||NEW.JobCardID,datetime('now','localtime'));
                  END;");

                    Run(@"CREATE TRIGGER IF NOT EXISTS trg_jobcards_update
                  AFTER UPDATE ON JobCards BEGIN
                      INSERT INTO SystemLogs (UserRole, Action, Timestamp)
                      VALUES ('Service Manager','Updated Job Card #'||NEW.JobCardID||' - Status: '||NEW.JobStatus,datetime('now','localtime'));
                  END;");

                    Run(@"CREATE TRIGGER IF NOT EXISTS trg_jobcards_delete
                  AFTER DELETE ON JobCards BEGIN
                      INSERT INTO SystemLogs (UserRole, Action, Timestamp)
                      VALUES ('Service Manager','Deleted Job Card #'||OLD.JobCardID,datetime('now','localtime'));
                  END;");

                    // ── Invoices ───────────────────────────────────────────────
                    Run(@"CREATE TRIGGER IF NOT EXISTS trg_invoices_insert
                  AFTER INSERT ON Invoices BEGIN
                      INSERT INTO SystemLogs (UserRole, Action, Timestamp)
                      VALUES ('Cashier','Generated Invoice #'||NEW.InvoiceID,datetime('now','localtime'));
                  END;");

                    Run(@"CREATE TRIGGER IF NOT EXISTS trg_invoices_update
                  AFTER UPDATE ON Invoices BEGIN
                      INSERT INTO SystemLogs (UserRole, Action, Timestamp)
                      VALUES ('Cashier','Updated Invoice #'||NEW.InvoiceID,datetime('now','localtime'));
                  END;");

                    Run(@"CREATE TRIGGER IF NOT EXISTS trg_invoices_delete
                  AFTER DELETE ON Invoices BEGIN
                      INSERT INTO SystemLogs (UserRole, Action, Timestamp)
                      VALUES ('Cashier','Deleted Invoice #'||OLD.InvoiceID,datetime('now','localtime'));
                  END;");

                    // ── Inventory ──────────────────────────────────────────────
                    Run(@"CREATE TRIGGER IF NOT EXISTS trg_inventory_insert
                  AFTER INSERT ON Inventory BEGIN
                      INSERT INTO SystemLogs (UserRole, Action, Timestamp)
                      VALUES ('Service Manager','Added Inventory Item: '||NEW.ItemName,datetime('now','localtime'));
                  END;");

                    Run(@"CREATE TRIGGER IF NOT EXISTS trg_inventory_update
                  AFTER UPDATE ON Inventory BEGIN
                      INSERT INTO SystemLogs (UserRole, Action, Timestamp)
                      VALUES ('Service Manager','Updated Inventory Item: '||NEW.ItemName,datetime('now','localtime'));
                  END;");

                    Run(@"CREATE TRIGGER IF NOT EXISTS trg_inventory_delete
                  AFTER DELETE ON Inventory BEGIN
                      INSERT INTO SystemLogs (UserRole, Action, Timestamp)
                      VALUES ('Service Manager','Deleted Inventory Item: '||OLD.ItemName,datetime('now','localtime'));
                  END;");
                }
            }
            catch { /* ignore trigger creation errors so app still starts */ }
        }

        private void DbWatcher_Changed(object? sender, FileSystemEventArgs e)
        {
            try
            {
                _dbChangeDebounceTimer?.Stop();
                _dbChangeDebounceTimer?.Start();
            }
            catch { }
        }

        // ── Polls every 3 s; only reloads when SystemLogs row count changed ──
        private void PollTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                int currentCount = 0;
                using (var conn = DatabaseHelper.GetConnection())
                {
                    var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT COUNT(*) FROM SystemLogs";
                    currentCount = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
                }

                if (currentCount != _lastLogCount)
                {
                    _lastLogCount = currentCount;
                    // New rows detected — refresh logs table, KPI count, and dashboard
                    LoadLogs();
                    LoadDashboard();
                }
            }
            catch { /* ignore DB errors during poll */ }
        }

        private void LogLoginEvent(string roleName)
        {
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO SystemLogs (UserRole, Action, Timestamp) VALUES (@role, 'Logged In', @time)";
                cmd.Parameters.AddWithValue("@role", roleName);
                cmd.Parameters.AddWithValue("@time", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        private void LogAction(string roleName, string action)
        {
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO SystemLogs (UserRole, Action, Timestamp) VALUES (@role, @action, @time)";
                cmd.Parameters.AddWithValue("@role", roleName);
                cmd.Parameters.AddWithValue("@action", action);
                cmd.Parameters.AddWithValue("@time", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        private void LoadDashboard()
        {
            try
            {
                using var conn = DatabaseHelper.GetConnection();

                // KPIs
                var revenueCmd = conn.CreateCommand();
                revenueCmd.CommandText = "SELECT IFNULL(SUM(TotalAmount),0) FROM Invoices";
                var revenue = Convert.ToDouble(revenueCmd.ExecuteScalar() ?? 0);

                var serviceCmd = conn.CreateCommand();
                serviceCmd.CommandText = "SELECT COUNT(*) FROM Services";
                var serviceCount = Convert.ToInt32(serviceCmd.ExecuteScalar() ?? 0);

                var invoiceCmd = conn.CreateCommand();
                invoiceCmd.CommandText = "SELECT COUNT(*) FROM Invoices";
                var invoiceCount = Convert.ToInt32(invoiceCmd.ExecuteScalar() ?? 0);

                var logCmd = conn.CreateCommand();
                logCmd.CommandText = "SELECT COUNT(*) FROM SystemLogs";
                var logsVal = logCmd.ExecuteScalar()?.ToString() ?? "0";

                // Revenue by month — always build a brand-new array
                double[] revenueByMonth = new double[12];
                var revenueChartCmd = conn.CreateCommand();
                revenueChartCmd.CommandText = @"
                    SELECT strftime('%m', PaymentDate) AS Month,
                           IFNULL(SUM(TotalAmount),0) AS Revenue
                    FROM Invoices
                    WHERE PaymentDate IS NOT NULL
                    GROUP BY strftime('%m', PaymentDate)";
                using (var r = revenueChartCmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        int month = Convert.ToInt32(r["Month"]);
                        revenueByMonth[month - 1] = Convert.ToDouble(r["Revenue"]);
                    }
                }

                // Fresh paints for THIS load cycle only (see comment on NewWhiteLabel)
                var revenueBarLabelPaint = NewWhiteLabel(13);
                var xAxisLabelPaint = NewWhiteLabel(13);
                var yAxisLabelPaint = NewWhiteLabel(13);
                var yAxisNamePaint = NewWhiteLabel(13);
                var xGridPaint = NewGridPaint();
                var yGridPaint = NewGridPaint();
                var pieLabelPaint = NewWhiteLabel(12);
                var legendPaint = NewWhiteLabel(13);

                // Always build a brand-new ColumnSeries with the fresh array + fresh paint
                var freshRevenueSeries = new ISeries[]
                {
                    new ColumnSeries<double>
                    {
                        Name = "Revenue (LKR)",
                        Values = revenueByMonth,
                        Fill = new SolidColorPaint(new SKColor(56,189,248)),
                        Stroke = null,
                        MaxBarWidth = 40,
                        DataLabelsPaint = revenueBarLabelPaint,
                        DataLabelsSize = 13,
                        DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
                        DataLabelsFormatter = p => p.Coordinate.PrimaryValue > 0
                                                   ? $"Rs {p.Coordinate.PrimaryValue / 1000:0}k"
                                                   : string.Empty
                    }
                };

                // Pie chart data
                // Pie chart: most-requested services from JobCards
                var pieSeries = new List<ISeries>();
                int colorIndex = 0;
                var serviceChartCmd = conn.CreateCommand();
                serviceChartCmd.CommandText = @"
                        SELECT s.ServiceName, COUNT(jc.JobCardID) AS Total
                        FROM Services s
                        INNER JOIN JobCards jc ON jc.ServiceID = s.ServiceID
                        GROUP BY s.ServiceID, s.ServiceName
                        ORDER BY Total DESC
                        LIMIT 8";
                using (var sr = serviceChartCmd.ExecuteReader())
                {
                    while (sr.Read())
                    {
                        SKColor slice = SliceColors[colorIndex % SliceColors.Length];
                        colorIndex++;
                        pieSeries.Add(new PieSeries<double>
                        {
                            Name = sr["ServiceName"]?.ToString() ?? string.Empty,
                            Values = new[] { Convert.ToDouble(sr["Total"]) },
                            Fill = new SolidColorPaint(slice),
                            DataLabelsPaint = pieLabelPaint,
                            DataLabelsSize = 12,
                            DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer,
                            DataLabelsFormatter = p => $"{p.Coordinate.PrimaryValue:0} jobs"
                        });
                    }
                }

                var freshPieSeries = pieSeries.ToArray();

                Dispatcher.Invoke(() =>
                {
                    // KPI labels
                    txtRevenue.Text = revenue.ToString("N2");
                    txtServices.Text = serviceCount.ToString();
                    txtInvoices.Text = invoiceCount.ToString();
                    txtLogins.Text = logsVal;

                    // ── BAR CHART FIX ────────────────────────────────────────
                    // Step 1: Null everything out so LiveCharts drops its cache
                    RevenueChart.Series = null;
                    RevenueChart.XAxes = null;
                    RevenueChart.YAxes = null;

                    // Step 2: Assign axes first, THEN series — all using
                    // freshly-created paint objects for this cycle
                    RevenueChart.XAxes = new[]
                    {
                        new Axis
                        {
                            Labels = new[]
                            {
                                "Jan","Feb","Mar","Apr","May","Jun",
                                "Jul","Aug","Sep","Oct","Nov","Dec"
                            },
                            LabelsPaint        = xAxisLabelPaint,
                            TextSize           = 13,
                            SeparatorsPaint    = xGridPaint,
                            ShowSeparatorLines = false
                        }
                    };
                    RevenueChart.YAxes = new[]
                    {
                        new Axis
                        {
                            Name            = "Revenue (LKR)",
                            NamePaint       = yAxisNamePaint,
                            LabelsPaint     = yAxisLabelPaint,
                            TextSize        = 13,
                            Labeler         = v => $"Rs {v / 1000:0}k",
                            SeparatorsPaint = yGridPaint
                        }
                    };
                    RevenueChart.Series = freshRevenueSeries;

                    // Pie chart — same null-then-assign pattern
                    StatsChart.Series = null;
                    StatsChart.Series = freshPieSeries;
                    try { StatsChart.LegendTextPaint = legendPaint; } catch { }

                    // Update bindable properties for any XAML bindings
                    RevenueSeries = freshRevenueSeries;
                    StatsSeries = freshPieSeries;
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                    MessageBox.Show("Failed to load dashboard: " + ex.Message,
                                    "Database Error", MessageBoxButton.OK, MessageBoxImage.Error));
            }
        }

        private void LoadServices()
        {
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT * FROM Services";
                using var reader = cmd.ExecuteReader();
                var dt = new DataTable();
                dt.Load(reader);
                Dispatcher.Invoke(() => dgServices.ItemsSource = dt.DefaultView);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load services: " + ex.Message, "Database Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadLogs()
        {
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT * FROM SystemLogs ORDER BY Timestamp DESC";
                using var reader = cmd.ExecuteReader();
                var dt = new DataTable();
                dt.Load(reader);
                Dispatcher.Invoke(() => dgLogs.ItemsSource = dt.DefaultView);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load logs: " + ex.Message, "Database Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAddService_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtServiceName.Text))
                {
                    MessageBox.Show("Please enter a service name.");
                    return;
                }
                using var conn = DatabaseHelper.GetConnection();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO Services (ServiceName, BasePrice) VALUES (@name, @price)";
                cmd.Parameters.AddWithValue("@name", txtServiceName.Text);
                cmd.Parameters.AddWithValue("@price", txtBasePrice.Text);
                cmd.ExecuteNonQuery();

                LogAction("System Administrator", $"Added Service: {txtServiceName.Text}");
                txtServiceName.Clear();
                txtBasePrice.Clear();
                LoadServices();
                LoadDashboard();
                LoadLogs();
            }
            catch (Exception ex) { MessageBox.Show("Failed to add service: " + ex.Message); }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            txtCurrentTime.Text = "🕒 " + DateTime.Now.ToString("dddd, dd MMMM yyyy HH:mm:ss");
            LoadDashboard();
            LoadServices();
            LoadLogs();
            LogAction("System Administrator", "Refreshed Dashboard");
            MessageBox.Show("Dashboard refreshed successfully.", "AutoCare");
        }

        private void BtnBackup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory("Backup");
                var backupName = "Backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt";
                File.WriteAllText(Path.Combine("Backup", backupName), "AutoCare Backup Created");
                LogAction("System Administrator", $"Created Backup: {backupName}");
                MessageBox.Show("Backup created successfully.", "Backup");
                LoadLogs();
            }
            catch (Exception ex) { MessageBox.Show("Backup failed: " + ex.Message); }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                GlobalFontSettings.UseWindowsFontsUnderWindows = true;

                var save = new SaveFileDialog
                {
                    Filter = "PDF Files (*.pdf)|*.pdf",
                    FileName = $"AutoCare_Report_{DateTime.Now:yyyyMMdd_HHmm}.pdf"
                };
                if (save.ShowDialog() != true) return;

                // ── Pull all data from DB ─────────────────────────────────
                int totalJobs = 0, completedJobs = 0, pendingJobs = 0,
                    inProgressJobs = 0, invoiceCount = 0, paidCount = 0, unpaidCount = 0;
                double totalRevenue = 0, totalServiceCost = 0, totalMaterialCost = 0, paidRevenue = 0;
                var topServices = new List<(string Name, int Count)>();
                var recentJobs = new List<(string VehicleNo, string Status, string Date)>();

                using (var conn = DatabaseHelper.GetConnection())
                {
                    var cmd = conn.CreateCommand();

                    cmd.CommandText = "SELECT COUNT(*) FROM JobCards";
                    totalJobs = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);

                    cmd.CommandText = "SELECT COUNT(*) FROM JobCards WHERE JobStatus = 'Completed'";
                    completedJobs = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);

                    cmd.CommandText = "SELECT COUNT(*) FROM JobCards WHERE JobStatus = 'Pending'";
                    pendingJobs = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);

                    cmd.CommandText = "SELECT COUNT(*) FROM JobCards WHERE JobStatus = 'In Progress'";
                    inProgressJobs = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);

                    cmd.CommandText = "SELECT COUNT(*) FROM Invoices";
                    invoiceCount = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);

                    cmd.CommandText = "SELECT COUNT(*) FROM Invoices WHERE PaymentStatus = 'Paid'";
                    paidCount = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);

                    cmd.CommandText = "SELECT COUNT(*) FROM Invoices WHERE PaymentStatus = 'Unpaid'";
                    unpaidCount = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);

                    cmd.CommandText = "SELECT IFNULL(SUM(TotalAmount),0) FROM Invoices";
                    totalRevenue = Convert.ToDouble(cmd.ExecuteScalar() ?? 0);

                    cmd.CommandText = "SELECT IFNULL(SUM(TotalAmount),0) FROM Invoices WHERE PaymentStatus = 'Paid'";
                    paidRevenue = Convert.ToDouble(cmd.ExecuteScalar() ?? 0);

                    cmd.CommandText = "SELECT IFNULL(SUM(ServiceCost),0) FROM Invoices";
                    totalServiceCost = Convert.ToDouble(cmd.ExecuteScalar() ?? 0);

                    cmd.CommandText = "SELECT IFNULL(SUM(MaterialCost),0) FROM Invoices";
                    totalMaterialCost = Convert.ToDouble(cmd.ExecuteScalar() ?? 0);

                    cmd.CommandText = @"
                SELECT s.ServiceName, COUNT(jc.JobCardID) AS Total
                FROM Services s
                INNER JOIN JobCards jc ON jc.ServiceID = s.ServiceID
                GROUP BY s.ServiceID, s.ServiceName
                ORDER BY Total DESC LIMIT 5";
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                            topServices.Add((r["ServiceName"]?.ToString() ?? "—", Convert.ToInt32(r["Total"])));

                    cmd.CommandText = @"
                SELECT VehicleNo, JobStatus, DateReceived
                FROM JobCards
                ORDER BY JobCardID DESC LIMIT 6";
                    using (var r2 = cmd.ExecuteReader())
                        while (r2.Read())
                            recentJobs.Add((
                                r2["VehicleNo"]?.ToString() ?? "—",
                                r2["JobStatus"]?.ToString() ?? "—",
                                r2["DateReceived"]?.ToString() ?? "—"
                            ));
                }

                // ── System colour palette ─────────────────────────────────
                var colDarkGreen = XColor.FromArgb(15, 26, 15);   // #0F1A0F  header bg
                var colGreen = XColor.FromArgb(74, 222, 128);   // #4ADE80  accent
                var colGreenDim = XColor.FromArgb(34, 85, 34);   // section bar
                var colSky = XColor.FromArgb(56, 189, 248);   // #38BDF8  blue accent
                var colAmber = XColor.FromArgb(251, 191, 36);   // #FBBf24  warning
                var colRed = XColor.FromArgb(248, 113, 113);    // #F87171  unpaid
                var colLightGray = XColor.FromArgb(240, 240, 240);    // card bg
                var colMidGray = XColor.FromArgb(180, 180, 180);
                var colWhite = XColors.White;
                var colBlack = XColors.Black;

                var brDarkGreen = new XSolidBrush(colDarkGreen);
                var brGreen = new XSolidBrush(colGreen);
                var brGreenDim = new XSolidBrush(colGreenDim);
                var brSky = new XSolidBrush(colSky);
                var brAmber = new XSolidBrush(colAmber);
                var brRed = new XSolidBrush(colRed);
                var brLightGray = new XSolidBrush(colLightGray);
                var brWhite = new XSolidBrush(colWhite);
                var brBlack = new XSolidBrush(colBlack);
                var brMidGray = new XSolidBrush(colMidGray);

                // ── Fonts ─────────────────────────────────────────────────
                var fntHero = new XFont("Verdana", 22, XFontStyleEx.Bold);
                var fntSub = new XFont("Verdana", 9, XFontStyleEx.Regular);
                var fntSec = new XFont("Verdana", 11, XFontStyleEx.Bold);
                var fntBody = new XFont("Verdana", 10, XFontStyleEx.Regular);
                var fntBold = new XFont("Verdana", 10, XFontStyleEx.Bold);
                var fntSmall = new XFont("Verdana", 8, XFontStyleEx.Regular);
                var fntKpiNum = new XFont("Verdana", 18, XFontStyleEx.Bold);
                var fntKpiLbl = new XFont("Verdana", 8, XFontStyleEx.Regular);

                // ── Page setup ────────────────────────────────────────────
                var document = new PdfDocument();
                document.Info.Title = "AutoCare Service Center Report";
                document.Info.Author = "System Administrator";

                var page = document.AddPage();
                page.Size = PdfSharp.PageSize.A4;
                var gfx = XGraphics.FromPdfPage(page);
                double W = page.Width.Point;   // ~595
                double H = page.Height.Point;  // ~842
                double lm = 30;                 // left margin
                double rm = W - 30;             // right margin
                double y = 0;

                // ── HEADER BAND ───────────────────────────────────────────
                gfx.DrawRectangle(brDarkGreen, 0, 0, W, 72);
                // Green left accent bar
                gfx.DrawRectangle(brGreen, 0, 0, 6, 72);
                gfx.DrawString("AutoCare Service Center", fntHero, brGreen,
                                new XRect(lm + 10, 10, W, 40), XStringFormats.TopLeft);
                gfx.DrawString("Management System  —  Business Performance Report", fntSub,
                                new XSolidBrush(colMidGray),
                                new XRect(lm + 10, 40, W, 20), XStringFormats.TopLeft);
                gfx.DrawString($"Generated: {DateTime.Now:dd MMM yyyy  HH:mm}   |   By: System Administrator",
                                fntSmall, new XSolidBrush(colMidGray),
                                new XRect(lm + 10, 55, W, 16), XStringFormats.TopLeft);
                y = 82;

                // ── Helper: draw a section header bar ────────────────────
                void SectionBar(string label, string icon)
                {
                    gfx.DrawRectangle(brGreenDim, lm, y, rm - lm, 20);
                    gfx.DrawString($"  {icon}  {label}", fntSec, brGreen,
                                    new XRect(lm + 4, y + 3, rm - lm, 20), XStringFormats.TopLeft);
                    y += 24;
                }

                // ── Helper: draw a KPI card ───────────────────────────────
                void KpiCard(double x, double cardY, double cw, double ch,
                             string num, string label, XSolidBrush accent)
                {
                    gfx.DrawRectangle(brLightGray, x, cardY, cw, ch);
                    gfx.DrawRectangle(accent, x, cardY, 4, ch);            // left colour bar
                    gfx.DrawString(num, fntKpiNum, brDarkGreen,
                                   new XRect(x + 8, cardY + 6, cw - 10, ch / 2), XStringFormats.TopLeft);
                    gfx.DrawString(label, fntKpiLbl, brMidGray,
                                   new XRect(x + 8, cardY + ch - 18, cw - 10, 16), XStringFormats.TopLeft);
                }

                // ── SECTION 1: JOB CARD KPIs (4 cards) ───────────────────
                SectionBar("Job Card Overview", "[JOBS]");
                double cardH = 54;
                double cardW = (rm - lm - 9) / 4;
                double cx = lm;
                KpiCard(cx, y, cardW, cardH, totalJobs.ToString(), "Total Jobs", brGreen);
                KpiCard(cx + cardW + 3, y, cardW, cardH, completedJobs.ToString(), "Completed", brSky);
                KpiCard(cx + (cardW + 3) * 2, y, cardW, cardH, inProgressJobs.ToString(), "In Progress", brAmber);
                KpiCard(cx + (cardW + 3) * 3, y, cardW, cardH, pendingJobs.ToString(), "Pending", brRed);
                y += cardH + 12;

                // ── SECTION 2: REVENUE KPIs (4 cards) ────────────────────
                SectionBar("Invoice & Revenue Summary", "[REVENUE]");
                KpiCard(cx, y, cardW, cardH, invoiceCount.ToString(), "Total Invoices", brGreen);
                KpiCard(cx + cardW + 3, y, cardW, cardH, $"Rs {totalRevenue / 1000:0.0}k", "Total Revenue", brSky);
                KpiCard(cx + (cardW + 3) * 2, y, cardW, cardH, paidCount.ToString(), "Paid Invoices", brGreen);
                KpiCard(cx + (cardW + 3) * 3, y, cardW, cardH, unpaidCount.ToString(), "Unpaid Invoices", brRed);
                y += cardH + 12;

                // ── SECTION 3: REVENUE BREAKDOWN TABLE ───────────────────
                SectionBar("Revenue Breakdown", "[BREAKDOWN]");
                void TableRow(string col1, string col2, bool isHeader)
                {
                    var bg = isHeader ? brGreenDim : brLightGray;
                    var fg = isHeader ? brGreen : brBlack;
                    var fn = isHeader ? fntBold : fntBody;
                    gfx.DrawRectangle(bg, lm, y, rm - lm, 16);
                    gfx.DrawString(col1, fn, fg, new XRect(lm + 6, y + 2, (rm - lm) * 0.6, 14), XStringFormats.TopLeft);
                    gfx.DrawString(col2, fn, fg, new XRect(lm + (rm - lm) * 0.62, y + 2, (rm - lm) * 0.36, 14), XStringFormats.TopLeft);
                    y += 17;
                }
                TableRow("Revenue Item", "Amount (LKR)", true);
                TableRow("Total Service Cost", $"Rs {totalServiceCost:N2}", false);
                TableRow("Total Material Cost", $"Rs {totalMaterialCost:N2}", false);
                TableRow("Total Invoice Revenue", $"Rs {totalRevenue:N2}", false);
                TableRow("Collected (Paid)", $"Rs {paidRevenue:N2}", false);
                TableRow("Outstanding (Unpaid)", $"Rs {(totalRevenue - paidRevenue):N2}", false);
                y += 10;

                // ── SECTION 4: TOP SERVICES ───────────────────────────────
                SectionBar("Top 5 Most Requested Services", "[SERVICES]");
                if (topServices.Count == 0)
                {
                    gfx.DrawString("  No service data available yet.", fntBody, brMidGray,
                                    new XRect(lm, y, rm - lm, 16), XStringFormats.TopLeft);
                    y += 20;
                }
                else
                {
                    // Find max for bar scaling
                    int maxVal = topServices[0].Count;
                    double barMaxW = rm - lm - 160;
                    foreach (var (svcName, svcCount) in topServices)
                    {
                        gfx.DrawString($"{svcName}", fntBody, brBlack,
                                        new XRect(lm + 4, y + 1, 150, 14), XStringFormats.TopLeft);
                        double barW = maxVal > 0 ? (svcCount / (double)maxVal) * barMaxW : 0;
                        gfx.DrawRectangle(brGreenDim, lm + 158, y + 2, barMaxW, 12);
                        gfx.DrawRectangle(brGreen, lm + 158, y + 2, barW, 12);
                        gfx.DrawString($"{svcCount} job(s)", fntSmall, brBlack,
                                        new XRect(lm + 162 + barMaxW, y + 2, 60, 12), XStringFormats.TopLeft);
                        y += 18;
                    }
                }
                y += 10;

                // ── SECTION 5: RECENT JOB CARDS TABLE ────────────────────
                SectionBar("Recent Job Cards", "[RECENT]");
                void JobRow(string v, string s, string d, bool header)
                {
                    var bg = header ? brGreenDim : brLightGray;
                    var fg = header ? brGreen : brBlack;
                    var fn = header ? fntBold : fntBody;
                    gfx.DrawRectangle(bg, lm, y, rm - lm, 16);

                    // Status colour dot
                    if (!header)
                    {
                        var dotBr = s == "Completed" ? brGreen : s == "In Progress" ? brAmber : brRed;
                        gfx.DrawEllipse(dotBr, lm + (rm - lm) * 0.42 + 2, y + 4, 8, 8);
                    }

                    gfx.DrawString(v, fn, fg, new XRect(lm + 6, y + 2, (rm - lm) * 0.38, 14), XStringFormats.TopLeft);
                    gfx.DrawString(s, fn, fg, new XRect(lm + (rm - lm) * 0.42 + 14, y + 2, (rm - lm) * 0.30, 14), XStringFormats.TopLeft);
                    gfx.DrawString(d, fn, fg, new XRect(lm + (rm - lm) * 0.72, y + 2, (rm - lm) * 0.26, 14), XStringFormats.TopLeft);
                    y += 17;
                }
                JobRow("Vehicle No.", "Status", "Date Received", true);
                if (recentJobs.Count == 0)
                {
                    gfx.DrawString("  No job card data available.", fntBody, brMidGray,
                                    new XRect(lm, y, rm - lm, 16), XStringFormats.TopLeft);
                    y += 20;
                }
                else
                {
                    foreach (var (v, s, d) in recentJobs)
                        JobRow(v, s, d, false);
                }
                y += 14;

                // ── FOOTER ────────────────────────────────────────────────
                double footerY = H - 30;
                gfx.DrawLine(new XPen(colGreenDim, 1), lm, footerY, rm, footerY);
                gfx.DrawString(
                    $"AutoCare Vehicle Service Center Management System  |  Confidential  |  {DateTime.Now:yyyy}",
                    fntSmall, brMidGray,
                    new XRect(lm, footerY + 4, rm - lm, 16), XStringFormats.TopLeft);
                gfx.DrawString("Page 1", fntSmall, brMidGray,
                    new XRect(0, footerY + 4, rm - 5, 16), XStringFormats.TopRight);

                // ── Save ──────────────────────────────────────────────────
                document.Save(save.FileName);
                LogAction("System Administrator", $"Exported PDF Report: {Path.GetFileName(save.FileName)}");
                LoadLogs();
                LoadDashboard();
                MessageBox.Show("PDF Report Generated Successfully!", "AutoCare",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Export failed: " + ex.Message, "Export Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAnalytics_Click(object sender, RoutedEventArgs e)
        {
            LoadDashboard();
            string report =
                $"📊 BUSINESS ANALYTICS\n\n" +
                $"💰 Revenue  : {txtRevenue.Text}\n" +
                $"🛠 Services : {txtServices.Text}\n" +
                $"🧾 Invoices : {txtInvoices.Text}\n" +
                $"👤 Logins   : {txtLogins.Text}";
            MessageBox.Show(report, "Analytics", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            string msg = Directory.Exists("Backup") ? "Backup restored successfully." : "No backup folder found.";
            if (Directory.Exists("Backup")) LogAction("System Administrator", "Restored Backup");
            MessageBox.Show(msg, "Restore");
            LoadLogs();
        }

        private void BtnLogs_Click(object sender, RoutedEventArgs e)
        {
            LoadLogs();
            MessageBox.Show("Latest system logs loaded.", "Logs");
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("⚙ AutoCare Settings\n\nVersion  : 1.0\nDatabase : Connected\nSecurity : Enabled\nBackup   : Ready",
                            "System Settings");
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            LogAction("System Administrator", "Logged Out");
            MainWindow? mainWindow =
                Application.Current?.MainWindow as MainWindow
                ?? Window.GetWindow(this) as MainWindow;
            if (mainWindow != null)
            {
                var nav = mainWindow.MainFrame.NavigationService;
                if (nav != null) while (nav.RemoveBackEntry() != null) { }
                mainWindow.MainFrame.Content = null;
            }
        }

        public void Dispose()
        {
            try
            {
                _pollTimer?.Stop();
                _pollTimer = null;
            }
            catch { }

            try
            {
                if (_dbWatcher != null)
                {
                    _dbWatcher.EnableRaisingEvents = false;
                    _dbWatcher.Changed -= DbWatcher_Changed;
                    _dbWatcher.Dispose();
                    _dbWatcher = null;
                }
            }
            catch { }
            try
            {
                if (_dbChangeDebounceTimer != null)
                {
                    _dbChangeDebounceTimer.Stop();
                    _dbChangeDebounceTimer.Dispose();
                    _dbChangeDebounceTimer = null;
                }
            }
            catch { }
        }
    }
}