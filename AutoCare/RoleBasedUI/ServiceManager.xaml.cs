using AutoCare.Models;
using AutoCare.Services;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using LiveCharts;
using LiveCharts.Wpf;

namespace AutoCare.RoleBasedUI
{
    public partial class ServiceManager : Page
    {
        private readonly InventoryService _inventoryService;
        private readonly JobService _jobService;
        private bool isFiltered = false;

        public ServiceManager()
        {
            InitializeComponent();
            _inventoryService = new InventoryService();
            _jobService = new JobService();
            DataContext = this;

            try
            {
                Application.Current.DispatcherUnhandledException += OnDispatcherUnhandledException;
            }
            catch { }

            LoadInventory();
            LoadJobs();
            LoadMechanics();
            UpdateChartData();

            LogAction("Service Manager", "Logged In");
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

        #region 🔐 LOGOUT
        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogAction("Service Manager", "Logged Out");
                var mainWindow = new MainWindow();
                Application.Current.MainWindow = mainWindow;
                mainWindow.Show();
                Window.GetWindow(this)?.Close();
            }
            catch (Exception ex)
            {
                LogException(ex);
                MessageBox.Show("An unexpected error occurred during logout. The error has been logged.");
            }
        }
        #endregion

        private void BtnDebugDate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var allJobs = _jobService?.GetAllJobs();
                if (allJobs == null)
                    MessageBox.Show("GetAllJobs returned NULL!");
                else if (allJobs.Count == 0)
                    MessageBox.Show("Database returned 0 records.");
                else
                    MessageBox.Show($"First record CreatedDate: '{allJobs[0].CreatedDate}'");
            }
            catch (Exception ex)
            {
                MessageBox.Show("CRITICAL ERROR: " + ex.ToString());
            }
        }

        private void LoadMechanics()
        {
            try
            {
                CmbMechanics.Items.Clear();
                var mechanics = _jobService.GetAllMechanicNames() ?? new List<string>();
                if (mechanics == null) return;
                CmbMechanics.ItemsSource = mechanics;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"error: {ex.Message}");
            }
        }

        private void LowStockAlertBorder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var allItems = _inventoryService.GetAllItems() ?? new List<InventoryItem>();
                if (!isFiltered)
                {
                    var lowStockItems = allItems.Where(i => i.Quantity <= i.MinStockLevel).ToList();
                    DgvInventory.ItemsSource = lowStockItems;
                    TxtAlertMessage.Text = $"Showing {lowStockItems.Count} low stock item(s) only. (Click to show all)";
                    isFiltered = true;
                }
                else
                {
                    DgvInventory.ItemsSource = allItems;
                    int lowStockCount = allItems.Count(i => i.Quantity <= i.MinStockLevel);
                    TxtAlertMessage.Text = $"{lowStockCount} item(s) are below the minimum stock level! (Click to filter)";
                    isFiltered = false;
                }
            }
            catch (Exception ex)
            {
                LogException(ex);
                MessageBox.Show("An unexpected error occurred while filtering inventory. The error has been logged.");
            }
        }

        #region 🔧 JOB ALLOCATION & TRACKING LOGIC

        private void LoadJobs()
        {
            try
            {
                DgvJobs.ItemsSource = _jobService.GetAllJobs();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading jobs: {ex.Message}");
            }
        }

        private void DgvJobs_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (DgvJobs.SelectedItem is JobCard selectedJob)
                    PopulateJobDetails(selectedJob);
            }
            catch (Exception ex)
            {
                LogException(ex);
                MessageBox.Show("An unexpected error occurred while opening the job details. The error has been logged.");
            }
        }

        private void DgvJobs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (DgvJobs.SelectedItem is JobCard job)
                    PopulateJobDetails(job);
            }
            catch (Exception ex)
            {
                LogException(ex);
                MessageBox.Show("An unexpected error occurred while selecting the job. The error has been logged.");
            }
        }

        private void PopulateJobDetails(JobCard selectedJob)
        {
            try
            {
                if (selectedJob == null) return;

                LblSelectedJobID.Text = $"Selected Job ID: {selectedJob.JobID}";
                TxtJobVehicle.Text = selectedJob.VehicleNumber;
                TxtJobIssue.Text = selectedJob.IssueDescription;

                CmbMechanics.SelectedItem = null;
                if (!string.IsNullOrEmpty(selectedJob.AssignedMechanic))
                {
                    foreach (var itm in CmbMechanics.Items)
                    {
                        if (itm?.ToString() == selectedJob.AssignedMechanic)
                        {
                            CmbMechanics.SelectedItem = itm;
                            break;
                        }
                    }
                }

                string statusToMatch = selectedJob.Status == "In Progress" ? "Ongoing" : selectedJob.Status;
                CmbStatus.SelectedItem = null;
                foreach (var itm in CmbStatus.Items)
                {
                    if (itm?.ToString() == statusToMatch)
                    {
                        CmbStatus.SelectedItem = itm;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                LogException(ex);
                MessageBox.Show("An unexpected error occurred while populating job details. The error has been logged.");
            }
        }

        private void OnDispatcherUnhandledException(object sender,
            System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                LogException(e.Exception);
                MessageBox.Show("An unexpected error occurred. The error has been logged.");
            }
            catch { }
            finally { e.Handled = true; }
        }

        private void LogException(Exception ex)
        {
            try
            {
                string logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AutoCare_Error.log");
                var sb = new StringBuilder();
                sb.AppendLine("---- " + DateTime.UtcNow.ToString("o") + " ----");
                sb.AppendLine(ex.ToString());
                sb.AppendLine();
                System.IO.File.AppendAllText(logPath, sb.ToString());
            }
            catch { }
        }

        private void BtnAssignJob_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var labelText = LblSelectedJobID?.Text ?? string.Empty;
                var parts = labelText.Split(new[] { ':' }, 2);
                if (parts.Length < 2 || !int.TryParse(parts[1].Trim(), out int jobId))
                {
                    MessageBox.Show("Please select a valid job and fields.");
                    return;
                }

                string mech = CmbMechanics.SelectedItem?.ToString() ?? "Not Assigned";
                string status = CmbStatus.SelectedItem?.ToString() ?? "Pending";

                if (_jobService.AssignMechanic(jobId, mech, status))
                {
                    // ── LOG: job assigned ────────────────────────────────
                    LogAction("Service Manager",
                        $"Assigned Job #{jobId} | Mechanic: {mech} | Status: {status}");

                    MessageBox.Show("Updated successfully!");
                    LoadJobs();
                    UpdateChartData();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Please select a valid job and fields. " + ex.Message);
            }
        }

        private void BtnClearJobForm_Click(object sender, RoutedEventArgs e)
        {
            LblSelectedJobID.Text = "Selected Job ID: None";
            TxtJobVehicle.Clear();
            TxtJobIssue.Clear();
            CmbMechanics.SelectedIndex = -1;
            CmbStatus.SelectedIndex = -1;
        }

        #endregion

        #region 📊 REPORTS & INSIGHTS

        private void BtnToggleChart_Click(object sender, RoutedEventArgs e)
        {
            UpdateChartData();
            MessageBox.Show("Statistics refreshed!");
        }

        private void BtnGenerateReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedMonthItem = CmbMonth.SelectedItem as ComboBoxItem;
                if (!int.TryParse(selectedMonthItem?.Content?.ToString(), out int month))
                {
                    MessageBox.Show("Please select a valid month.");
                    return;
                }

                if (!int.TryParse(TxtYear?.Text, out int year))
                {
                    MessageBox.Show("Please enter a valid year.");
                    return;
                }

                var reportData = _jobService.GetServiceReportsByMonth(month, year);
                if (reportData == null || !reportData.Any())
                {
                    MessageBox.Show($"No records found for {month:D2}/{year}.");
                    return;
                }

                var saveDialog = new SaveFileDialog
                {
                    Title = "Save Service Report",
                    Filter = "CSV File (*.csv)|*.csv",
                    FileName = $"ServiceReport_{year}_{month:D2}.csv"
                };
                if (saveDialog.ShowDialog() != true) return;

                var sb = new StringBuilder();
                sb.AppendLine("Job Card ID,Vehicle No,Issue Description,Mechanic Name,Date Received,Status");
                string Quote(string s) => s == null ? "" : "\"" + s.Replace("\"", "\"\"") + "\"";

                foreach (var job in reportData)
                {
                    string dateField = "'" + job.CreatedDate.ToString("yyyy-MM-dd");
                    sb.AppendLine(string.Join(",",
                        job.JobID.ToString(),
                        Quote(job.VehicleNumber),
                        Quote(job.IssueDescription),
                        Quote(job.AssignedMechanic),
                        Quote(dateField),
                        Quote(job.Status)));
                }

                File.WriteAllText(saveDialog.FileName, sb.ToString(), Encoding.UTF8);

                // ── LOG: report generated ────────────────────────────────
                LogAction("Service Manager",
                    $"Generated Service Report: {month:D2}/{year} → {Path.GetFileName(saveDialog.FileName)}");

                MessageBox.Show($"Report saved successfully!\n{saveDialog.FileName}",
                                "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.ToString());
            }
        }

        private void UpdateChartData()
        {
            var statusCounts = _jobService.GetJobStatusCounts() ?? new Dictionary<string, int>();
            int pending = statusCounts.ContainsKey("Pending") ? statusCounts["Pending"] : 0;
            int ongoing = statusCounts.ContainsKey("Ongoing") ? statusCounts["Ongoing"] : 0;
            int completed = statusCounts.ContainsKey("Completed") ? statusCounts["Completed"] : 0;

            MyPieChart.Series.Clear();
            MyPieChart.Series = new SeriesCollection
            {
                new PieSeries { Title = "Pending",   Values = new ChartValues<int> { pending },   Fill = System.Windows.Media.Brushes.Orange,        DataLabels = true },
                new PieSeries { Title = "Ongoing",   Values = new ChartValues<int> { ongoing },   Fill = System.Windows.Media.Brushes.DodgerBlue,     DataLabels = true },
                new PieSeries { Title = "Completed", Values = new ChartValues<int> { completed }, Fill = System.Windows.Media.Brushes.MediumSeaGreen, DataLabels = true }
            };
        }

        #endregion

        #region 🗃️ INVENTORY MANAGEMENT

        private void LoadInventory()
        {
            try
            {
                var items = _inventoryService.GetAllItems();
                DgvInventory.ItemsSource = null;
                DgvInventory.ItemsSource = items;
                isFiltered = false;
                UpdateLowStockAlert(items);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading inventory: {ex.Message}");
            }
        }

        private void UpdateLowStockAlert(IEnumerable<InventoryItem> items)
        {
            var lowStockItems = items?.Where(i => i.Quantity <= i.MinStockLevel).ToList()
                                ?? new List<InventoryItem>();
            if (lowStockItems.Count > 0)
            {
                LowStockAlertBorder.Visibility = Visibility.Visible;
                TxtAlertMessage.Text = $"{lowStockItems.Count} item(s) are below the minimum stock level! (Click to filter)";
            }
            else
            {
                LowStockAlertBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnAddItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var newItem = new InventoryItem
                {
                    ItemName = TxtItemName.Text,
                    Quantity = int.Parse(TxtQuantity.Text),
                    MinStockLevel = int.Parse(TxtMinLevel.Text),
                    UnitPrice = decimal.Parse(TxtPrice.Text)
                };
                if (_inventoryService.AddItem(newItem))
                {
                    // ── LOG: item added ──────────────────────────────────
                    LogAction("Service Manager",
                        $"Added Inventory Item: {newItem.ItemName} | Qty: {newItem.Quantity}");
                    MessageBox.Show("Item added successfully!");
                    LoadInventory();
                    BtnClearForm_Click(sender, e);
                }
            }
            catch (Exception ex) { MessageBox.Show("Error adding item: " + ex.Message); }
        }

        private void BtnUpdateItem_Click(object sender, RoutedEventArgs e)
        {
            string idText = LblSelectedID.Text.Replace("Selected ID: ", "").Trim();
            if (idText == "None" || !int.TryParse(idText, out int id))
            {
                MessageBox.Show("Please select an item from the list first.");
                return;
            }

            var item = new InventoryItem
            {
                ItemID = id,
                ItemName = TxtItemName.Text,
                Quantity = int.Parse(TxtQuantity.Text),
                MinStockLevel = int.Parse(TxtMinLevel.Text),
                UnitPrice = decimal.Parse(TxtPrice.Text)
            };

            if (_inventoryService.UpdateItem(item))
            {
                // ── LOG: item updated ────────────────────────────────────
                LogAction("Service Manager",
                    $"Updated Inventory Item ID: {id} → {item.ItemName}");
                MessageBox.Show("Item updated successfully!");
                LoadInventory();
                BtnClearForm_Click(sender, e);
            }
            else { MessageBox.Show("Update failed."); }
        }

        private void BtnClearForm_Click(object sender, RoutedEventArgs e)
        {
            TxtItemName.Clear();
            TxtQuantity.Clear();
            TxtMinLevel.Clear();
            TxtPrice.Clear();
            LblSelectedID.Text = "Selected ID: None";
        }

        private void BtnDeleteItem_Click(object sender, RoutedEventArgs e)
        {
            string idText = LblSelectedID.Text.Replace("Selected ID: ", "").Trim();
            if (idText == "None") { MessageBox.Show("Select an item to delete."); return; }

            var result = MessageBox.Show("Are you sure you want to delete this item?",
                                         "Confirm", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                int id = int.Parse(idText);
                if (_inventoryService.DeleteItem(id))
                {
                    // ── LOG: item deleted ────────────────────────────────
                    LogAction("Service Manager", $"Deleted Inventory Item ID: {id}");
                    MessageBox.Show("Deleted successfully.");
                    LoadInventory();
                    BtnClearForm_Click(sender, e);
                }
                else { MessageBox.Show("Delete operation failed in database."); }
            }
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog { Filter = "CSV Files (*.csv)|*.csv" };
            if (ofd.ShowDialog() == true)
            {
                try
                {
                    var lines = File.ReadAllLines(ofd.FileName);
                    for (int i = 1; i < lines.Length; i++)
                    {
                        var values = lines[i].Split(',');
                        if (values.Length >= 4)
                        {
                            var newItem = new InventoryItem
                            {
                                ItemName = values[1],
                                Quantity = int.Parse(values[2]),
                                MinStockLevel = int.Parse(values[3]),
                                UnitPrice = decimal.Parse(values[4])
                            };
                            _inventoryService.AddItem(newItem);
                        }
                    }
                    // ── LOG: import ──────────────────────────────────────
                    LogAction("Service Manager",
                        $"Imported Inventory CSV: {Path.GetFileName(ofd.FileName)}");
                    MessageBox.Show("Inventory imported successfully!");
                    LoadInventory();
                }
                catch (Exception ex) { MessageBox.Show("Error importing file: " + ex.Message); }
            }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                FileName = "Inventory_Export.csv"
            };
            if (sfd.ShowDialog() == true)
            {
                var items = _inventoryService.GetAllItems();
                StringBuilder csv = new StringBuilder();
                csv.AppendLine("ID,Name,Quantity,MinLevel,Price");
                foreach (var i in items)
                    csv.AppendLine($"{i.ItemID},{i.ItemName},{i.Quantity},{i.MinStockLevel},{i.UnitPrice}");
                File.WriteAllText(sfd.FileName, csv.ToString());

                // ── LOG: export ──────────────────────────────────────────
                LogAction("Service Manager",
                    $"Exported Inventory CSV: {Path.GetFileName(sfd.FileName)}");
                MessageBox.Show("Exported successfully!");
            }
        }

        private void DgvInventory_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DgvInventory.SelectedItem is InventoryItem selectedItem)
            {
                LblSelectedID.Text = $"Selected ID: {selectedItem.ItemID}";
                TxtItemName.Text = selectedItem.ItemName;
                TxtQuantity.Text = selectedItem.Quantity.ToString();
                TxtMinLevel.Text = selectedItem.MinStockLevel.ToString();
                TxtPrice.Text = selectedItem.UnitPrice.ToString();
            }
        }

        #endregion
    }
}