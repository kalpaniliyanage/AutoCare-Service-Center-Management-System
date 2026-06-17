using AutoCare.Models;
using AutoCare.Services;
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

            // Register a dispatcher-level exception handler so crashes do not shut down the whole system
            try
            {
                Application.Current.DispatcherUnhandledException += OnDispatcherUnhandledException;
            }
            catch
            {
                // Ignore if Application.Current is null (design-time) or handler cannot be attached
            }

            LoadInventory();
            LoadJobs();
            LoadMechanics();
            UpdateChartData();
        }

        #region 🔐 LOGOUT

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // TODO: If your main window class isn't named "MainWindow" (or lives in a
                // different namespace), update the line below to match.
                var mainWindow = new MainWindow();
                Application.Current.MainWindow = mainWindow;
                mainWindow.Show();

                // Close whichever window is currently hosting this page
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
                {
                    MessageBox.Show("GetAllJobs returned NULL!");
                }
                else if (allJobs.Count == 0)
                {
                    MessageBox.Show("Database returned 0 records.");
                }
                else
                {
                    // Show the actual CreatedDate value from the first record (JobCard uses CreatedDate)
                    MessageBox.Show($"First record CreatedDate: '{allJobs[0].CreatedDate}'");
                }
            }
            catch (Exception ex)
            {
                // This will finally tell us WHY it wasn't showing anything
                MessageBox.Show("CRITICAL ERROR: " + ex.ToString());
            }
        }
        private void LoadMechanics()
        {
            try
            {
                CmbMechanics.Items.Clear();

                // දැන් දත්ත එන්නේ JobCards වගුවෙන්
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
                    // Show only the low-stock items
                    var lowStockItems = allItems.Where(i => i.Quantity <= i.MinStockLevel).ToList();
                    DgvInventory.ItemsSource = lowStockItems;
                    TxtAlertMessage.Text = $"Showing {lowStockItems.Count} low stock item(s) only. (Click to show all)";
                    isFiltered = true;
                }
                else
                {
                    // Restore the full inventory list
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
                {
                    PopulateJobDetails(selectedJob);
                }
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
                // Populate details when selection changes (single-click selection)
                if (DgvJobs.SelectedItem is JobCard job)
                {
                    PopulateJobDetails(job);
                }
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

                // Select mechanic in ComboBox (items are strings)
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

                // Select status (normalize 'In Progress')
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

        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                LogException(e.Exception);
                MessageBox.Show("An unexpected error occurred. The error has been logged.");
            }
            catch { }
            finally
            {
                e.Handled = true; // prevent process termination from unhandled exceptions
            }
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
                // Safely extract job id from label text (avoid null deref and parse exceptions)
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
                // Step 1: Get month
                var selectedMonthItem = CmbMonth.SelectedItem as ComboBoxItem;
                if (!int.TryParse(selectedMonthItem?.Content?.ToString(), out int month))
                {
                    MessageBox.Show("Please select a valid month.");
                    return;
                }

                // Step 2: Get year
                if (!int.TryParse(TxtYear?.Text, out int year))
                {
                    MessageBox.Show("Please enter a valid year.");
                    return;
                }

                // Step 3: Fetch data
                var reportData = _jobService.GetServiceReportsByMonth(month, year);

                if (reportData == null || !reportData.Any())
                {
                    MessageBox.Show($"No records found for {month:D2}/{year}.");
                    return;
                }

                // Step 4: Ask user where to save
                var saveDialog = new SaveFileDialog
                {
                    Title = "Save Service Report",
                    Filter = "CSV File (*.csv)|*.csv",
                    FileName = $"ServiceReport_{year}_{month:D2}.csv"
                };

                if (saveDialog.ShowDialog() != true) return;

                // Step 5: Build CSV using JobCard property names
                var sb = new StringBuilder();
                sb.AppendLine("Job Card ID,Vehicle No,Issue Description,Mechanic Name,Date Received,Status");

                // helper to CSV-quote strings and escape internal quotes
                string Quote(string s) => s == null ? "" : "\"" + s.Replace("\"", "\"\"") + "\"";

                foreach (var job in reportData)
                {
                    // Force date to be treated as text in Excel by prefixing with apostrophe inside the quoted field
                    string dateField = "'" + job.CreatedDate.ToString("yyyy-MM-dd");

                    sb.AppendLine(string.Join(",",
                        job.JobID.ToString(),
                        Quote(job.VehicleNumber),
                        Quote(job.IssueDescription),
                        Quote(job.AssignedMechanic),
                        Quote(dateField),
                        Quote(job.Status)
                    ));
                }

                // Step 6: Write file
                File.WriteAllText(saveDialog.FileName, sb.ToString(), Encoding.UTF8);

                MessageBox.Show($"Report saved successfully!\n{saveDialog.FileName}",
                                "Export Complete",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.ToString());
            }
        }
        private void UpdateChartData()
        {
            var statusCounts = _jobService.GetJobStatusCounts() ?? new Dictionary<string, int>();
            // ensure keys exist to avoid KeyNotFoundException
            int pending = statusCounts.ContainsKey("Pending") ? statusCounts["Pending"] : 0;
            int ongoing = statusCounts.ContainsKey("Ongoing") ? statusCounts["Ongoing"] : 0;
            int completed = statusCounts.ContainsKey("Completed") ? statusCounts["Completed"] : 0;

            MyPieChart.Series.Clear();
            MyPieChart.Series = new SeriesCollection
            {
                new PieSeries { Title = "Pending", Values = new ChartValues<int> { pending }, Fill = System.Windows.Media.Brushes.Orange, DataLabels = true },
                new PieSeries { Title = "Ongoing", Values = new ChartValues<int> { ongoing }, Fill = System.Windows.Media.Brushes.DodgerBlue, DataLabels = true },
                new PieSeries { Title = "Completed", Values = new ChartValues<int> { completed }, Fill = System.Windows.Media.Brushes.MediumSeaGreen, DataLabels = true }
            };
        }

        #endregion

        #region 🗃️ INVENTORY MANAGEMENT

        private void LoadInventory()
        {
            try
            {
                // 1. දත්ත ගබඩාවෙන් අලුත්ම ලැයිස්තුව ලබා ගන්න
                var items = _inventoryService.GetAllItems();

                // 2. DataGrid එකේ කලින් තිබූ දත්ත මුලින්ම ඉවත් කරන්න (මෙය වැදගත්)
                DgvInventory.ItemsSource = null;

                // 3. නව ලැයිස්තුව නැවත පවරන්න
                DgvInventory.ItemsSource = items;

                // 4. Refresh the low-stock alert banner based on the freshly loaded data
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
                // 1. Create the new item
                var newItem = new InventoryItem
                {
                    ItemName = TxtItemName.Text,
                    Quantity = int.Parse(TxtQuantity.Text),
                    MinStockLevel = int.Parse(TxtMinLevel.Text),
                    UnitPrice = decimal.Parse(TxtPrice.Text)
                };

                // 2. Add to database via service
                if (_inventoryService.AddItem(newItem))
                {
                    MessageBox.Show("Item added successfully!");
                    LoadInventory(); // Refresh the grid
                    BtnClearForm_Click(sender, e);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error adding item: " + ex.Message);
            }
        }

        private void BtnUpdateItem_Click(object sender, RoutedEventArgs e)
        {
            // 1. Get the ID from your Label
            string idText = LblSelectedID.Text.Replace("Selected ID: ", "").Trim();
            if (idText == "None" || !int.TryParse(idText, out int id))
            {
                MessageBox.Show("Please select an item from the list first.");
                return;
            }

            // 2. Prepare the object (assuming your InventoryItem model has these properties)
            var item = new InventoryItem
            {
                ItemID = id,
                ItemName = TxtItemName.Text,
                Quantity = int.Parse(TxtQuantity.Text),
                MinStockLevel = int.Parse(TxtMinLevel.Text),
                UnitPrice = decimal.Parse(TxtPrice.Text)
            };

            // 3. Call the Service (You need an UpdateItem method in your InventoryService)
            if (_inventoryService.UpdateItem(item))
            {
                MessageBox.Show("Item updated successfully!");
                LoadInventory(); // Refresh the grid
                BtnClearForm_Click(sender, e); // Clear inputs
            }
            else
            {
                MessageBox.Show("Update failed.");
            }
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
            // 1. Get the ID from your label
            string idText = LblSelectedID.Text.Replace("Selected ID: ", "").Trim();
            if (idText == "None") { MessageBox.Show("Select an item to delete."); return; }

            var result = MessageBox.Show("Are you sure you want to delete this item?", "Confirm", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                int id = int.Parse(idText);

                // 2. Perform the deletion in your service
                if (_inventoryService.DeleteItem(id))
                {
                    MessageBox.Show("Deleted successfully.");

                    // 3. THIS IS THE MISSING STEP:
                    // You must call the load method again to refresh the list in the DataGrid
                    LoadInventory();

                    // 4. Clear the form so the deleted item isn't still visible in the text boxes
                    BtnClearForm_Click(sender, e);
                }
                else
                {
                    MessageBox.Show("Delete operation failed in database.");
                }
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
                    // Start at index 1 to skip the header row
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
                            // Call your service to insert into database
                            _inventoryService.AddItem(newItem);
                        }
                    }
                    MessageBox.Show("Inventory imported successfully!");
                    LoadInventory(); // Refresh the grid
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error importing file: " + ex.Message);
                }
            }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog { Filter = "CSV Files (*.csv)|*.csv", FileName = "Inventory_Export.csv" };
            if (sfd.ShowDialog() == true)
            {
                var items = _inventoryService.GetAllItems();
                StringBuilder csv = new StringBuilder();
                csv.AppendLine("ID,Name,Quantity,MinLevel,Price");
                foreach (var i in items)
                    csv.AppendLine($"{i.ItemID},{i.ItemName},{i.Quantity},{i.MinStockLevel},{i.UnitPrice}");

                File.WriteAllText(sfd.FileName, csv.ToString());
                MessageBox.Show("Exported successfully!");
            }
        }

        private void DgvInventory_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // DataGrid එකෙන් තෝරාගත් අයිතමය ලබා ගැනීම
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
