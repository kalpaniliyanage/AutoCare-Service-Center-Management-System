using AutoCare.Models;
using AutoCare.Services;
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
using Microsoft.Win32;
using System.IO;
using System.Linq;

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

            // Page එක load වෙද්දීම දත්ත ටික Grid දෙකටම දාන්න
            LoadInventory();
            LoadJobs();
        }

        #region 📦 INVENTORY CONTROL LOGIC

        private void LoadInventory()
        {
            try
            {
                var itemList = _inventoryService.GetAllItems();
                DgvInventory.ItemsSource = itemList;

                bool hasLowStock = itemList.Any(item => item.Quantity <= item.MinStockLevel);
                LowStockAlertBorder.Visibility = hasLowStock ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading inventory: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LowStockAlertBorder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var itemList = _inventoryService.GetAllItems();

                if (!isFiltered)
                {
                    var lowStockItems = itemList.Where(item => item.Quantity <= item.MinStockLevel).ToList();
                    DgvInventory.ItemsSource = lowStockItems;
                    TxtAlertMessage.Text = "Showing Low Stock Items only! (Click again to show all)";
                    isFiltered = true;
                }
                else
                {
                    DgvInventory.ItemsSource = itemList;
                    TxtAlertMessage.Text = "Some spare parts are below the minimum stock level! (Click to filter)";
                    isFiltered = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error filtering inventory: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                BtnAddItem.IsEnabled = false;
            }
        }

        private void BtnAddItem_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtItemName.Text) || string.IsNullOrWhiteSpace(TxtQuantity.Text) ||
                string.IsNullOrWhiteSpace(TxtMinLevel.Text) || string.IsNullOrWhiteSpace(TxtPrice.Text))
            {
                MessageBox.Show("Please fill in all fields before adding an item.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var newItem = new InventoryItem
                {
                    ItemName = TxtItemName.Text.Trim(),
                    Quantity = int.Parse(TxtQuantity.Text.Trim()),
                    MinStockLevel = int.Parse(TxtMinLevel.Text.Trim()),
                    UnitPrice = decimal.Parse(TxtPrice.Text.Trim())
                };

                if (_inventoryService.AddItem(newItem))
                {
                    MessageBox.Show("Item added successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    ClearForm();
                    LoadInventory();
                }
            }
            catch (FormatException)
            {
                MessageBox.Show("Please enter valid numeric values.", "Input Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to add item: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnUpdateItem_Click(object sender, RoutedEventArgs e)
        {
            if (!LblSelectedID.Text.StartsWith("Selected ID: ") || LblSelectedID.Text.Contains("None"))
            {
                MessageBox.Show("Please double-click an item from the grid to update.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                int itemId = int.Parse(LblSelectedID.Text.Replace("Selected ID: ", ""));
                var updatedItem = new InventoryItem
                {
                    ItemID = itemId,
                    ItemName = TxtItemName.Text.Trim(),
                    Quantity = int.Parse(TxtQuantity.Text.Trim()),
                    MinStockLevel = int.Parse(TxtMinLevel.Text.Trim()),
                    UnitPrice = decimal.Parse(TxtPrice.Text.Trim())
                };

                if (_inventoryService.UpdateItem(updatedItem))
                {
                    MessageBox.Show("Item updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    ClearForm();
                    LoadInventory();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Update failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (!LblSelectedID.Text.StartsWith("Selected ID: ") || LblSelectedID.Text.Contains("None"))
            {
                MessageBox.Show("Please double-click an item from the grid to delete.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show("Are you sure you want to delete this item permanently?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    int itemId = int.Parse(LblSelectedID.Text.Replace("Selected ID: ", ""));
                    if (_inventoryService.DeleteItem(itemId))
                    {
                        MessageBox.Show("Item deleted successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        ClearForm();
                        LoadInventory();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Delete failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnClearForm_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

        private void ClearForm()
        {
            LblSelectedID.Text = "Selected ID: None";
            TxtItemName.Clear();
            TxtQuantity.Clear();
            TxtMinLevel.Clear();
            TxtPrice.Clear();
            BtnAddItem.IsEnabled = true;
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog { Filter = "CSV Files (*.csv)|*.csv", Title = "Select Inventory CSV File" };
            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    int count = _inventoryService.ImportFromCsv(openFileDialog.FileName);
                    MessageBox.Show($"{count} items imported successfully!", "Import Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadInventory();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to import CSV: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog { Filter = "CSV Files (*.csv)|*.csv", FileName = "AutoCare_Inventory.csv", Title = "Save Inventory Export" };
            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    var csvLines = _inventoryService.ExportToCsv();
                    File.WriteAllLines(saveFileDialog.FileName, csvLines);
                    MessageBox.Show("Inventory exported to CSV successfully!", "Export Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to export CSV: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        #region 🔧 JOB ALLOCATION & TRACKING LOGIC

        // 1. DataGrid එකට Jobs ටික Load කරන මෙතඩ් එක
        private void LoadJobs()
        {
            try
            {
                var jobList = _jobService.GetAllJobs();
                DgvJobs.ItemsSource = jobList;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading jobs: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 2. Job Grid එක Double-Click කරාම Form එකට Data පිරෙන ඉවෙන්ට් එක
        private void DgvJobs_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DgvJobs.SelectedItem is JobCard selectedJob)
            {
                LblSelectedJobID.Text = $"Selected Job ID: {selectedJob.JobID}";

                // දත්ත TextBox වලට ලබා දීම
                TxtJobVehicle.Text = selectedJob.VehicleNumber;
                TxtJobIssue.Text = selectedJob.IssueDescription;

                // 💡 වැදගත්: TextBox වලට Type කරන්න පුළුවන් වෙන්න ReadOnly එක False කරන්න
                TxtJobVehicle.IsReadOnly = false;
                TxtJobIssue.IsReadOnly = false;

                // ComboBox වල දැනට තියෙන අගයන් Select කිරීම
                CmbMechanics.Text = selectedJob.AssignedMechanic;
                CmbStatus.Text = selectedJob.Status;
            }
        }

        // 3. Assign & Update බටන් එක ක්ලික් කරාම වෙන දේ
        private void BtnAssignJob_Click(object sender, RoutedEventArgs e)
        {
            if (!LblSelectedJobID.Text.StartsWith("Selected Job ID: ") || LblSelectedJobID.Text.Contains("None"))
            {
                MessageBox.Show("Please double-click a job from the grid to select.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (CmbMechanics.SelectedItem == null || CmbStatus.SelectedItem == null)
            {
                MessageBox.Show("Please select both a Mechanic and a Status.", "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                int jobId = int.Parse(LblSelectedJobID.Text.Replace("Selected Job ID: ", ""));

                // 💡 CS8600 Null Warnings මඟහරවා ගැනීමට ආරක්ෂිතව Content ලබා ගැනීම
                string selectedMechanic = (CmbMechanics.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Not Assigned";
                string selectedStatus = (CmbStatus.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Pending";

                if (_jobService.AssignMechanic(jobId, selectedMechanic, selectedStatus))
                {
                    MessageBox.Show("Job assignment updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    ClearJobForm();
                    LoadJobs(); // Grid එක refresh කරනවා
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to update job: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 4. Job Form එක Clear කරන බටන් එක
        private void BtnClearJobForm_Click(object sender, RoutedEventArgs e)
        {
            ClearJobForm();
        }

        private void ClearJobForm()
        {
            LblSelectedJobID.Text = "Selected Job ID: None";
            TxtJobVehicle.Clear();
            TxtJobIssue.Clear();

            // 💡 Reset කරද්දීත් Type කරන්න පුළුවන් වෙන විදිහටම තියන්න
            TxtJobVehicle.IsReadOnly = false;
            TxtJobIssue.IsReadOnly = false;

            CmbMechanics.SelectedIndex = -1;
            CmbStatus.SelectedIndex = -1;
        }

        #endregion
    }
}