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
using System.Linq; // 💡 මෙන්න මේකයි අඩුවෙලා තිබුණේ! LINQ වැඩ කරන්න මේක අනිවාර්යයි.

namespace AutoCare.RoleBasedUI
{
    public partial class ServiceManager : Page
    {
        private readonly InventoryService _inventoryService;
        private bool isFiltered = false;

        public ServiceManager()
        {
            InitializeComponent();
            _inventoryService = new InventoryService();

            // Page එක load වෙද්දීම දත්ත ටික Grid එකට දාන්න
            LoadInventory();
        }

        // DataGrid එකට දත්ත Load කරන සහ Low Stock Alert එක පෙන්වන ක්‍රමය
        private void LoadInventory()
        {
            try
            {
                var itemList = _inventoryService.GetAllItems();
                DgvInventory.ItemsSource = itemList;

                // යම් අයිටම් එකක තොග අවම මට්ටමට වඩා අඩුදැයි පරික්ෂා කිරීම
                bool hasLowStock = itemList.Any(item => item.Quantity <= item.MinStockLevel);

                if (hasLowStock)
                {
                    LowStockAlertBorder.Visibility = Visibility.Visible;
                }
                else
                {
                    LowStockAlertBorder.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading inventory: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 💡 බැනර් එක ක්ලික් කරාම Low Stock බඩු විතරක් Filter වීම
        private void LowStockAlertBorder_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                var itemList = _inventoryService.GetAllItems();

                if (!isFiltered)
                {
                    // Quantity එක MinStockLevel එකට වඩා අඩු හෝ සමාන බඩු විතරක් Filter කරලා Grid එකට දානවා
                    var lowStockItems = itemList.Where(item => item.Quantity <= item.MinStockLevel).ToList();
                    DgvInventory.ItemsSource = lowStockItems;

                    // බැනර් එකේ පණිවිඩය වෙනස් කරනවා
                    TxtAlertMessage.Text = "Showing Low Stock Items only! (Click again to show all)";
                    isFiltered = true;
                }
                else
                {
                    // ආයෙත් ක්ලික් කරාම සාමාන්‍ය විදිහට ඔක්කොම බඩු පෙන්වනවා
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

        // 1. DataGrid එක Double-Click කරපුහම දත්ත TextBoxes වලට පිරෙන ක්‍රමය
        private void DgvInventory_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (DgvInventory.SelectedItem is InventoryItem selectedItem)
            {
                LblSelectedID.Text = $"Selected ID: {selectedItem.ItemID}";
                TxtItemName.Text = selectedItem.ItemName;
                TxtQuantity.Text = selectedItem.Quantity.ToString();
                TxtMinLevel.Text = selectedItem.MinStockLevel.ToString();
                TxtPrice.Text = selectedItem.UnitPrice.ToString();

                // Add Button එක Disable කරලා Update/Delete කරන්න ඉඩ දෙනවා
                BtnAddItem.IsEnabled = false;
            }
        }

        // Add Item බටන් එක click කරපුහම වෙන දේ
        private void BtnAddItem_Click(object sender, RoutedEventArgs e)
        {
            // Input Validation (හිස්තැන් තිබේදැයි පරික්ෂාව)
            if (string.IsNullOrWhiteSpace(TxtItemName.Text) ||
                string.IsNullOrWhiteSpace(TxtQuantity.Text) ||
                string.IsNullOrWhiteSpace(TxtMinLevel.Text) ||
                string.IsNullOrWhiteSpace(TxtPrice.Text))
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

                bool success = _inventoryService.AddItem(newItem);

                if (success)
                {
                    MessageBox.Show("Item added successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    ClearForm();
                    LoadInventory();
                }
            }
            catch (FormatException)
            {
                MessageBox.Show("Please enter valid numeric values for Quantity, Min Level, and Price.", "Input Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to add item: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 2. Update බටන් එක ක්ලික් කරපුහම වෙන දේ
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

        // 3. Delete බටන් එක ක්ලික් කරපුහම වෙන දේ
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

        // 4. Form එක Reset කරන ක්‍රමය
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

        // 📥 CSV Import Button
        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                Title = "Select Inventory CSV File"
            };

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

        // 📤 CSV Export Button
        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                FileName = "AutoCare_Inventory.csv",
                Title = "Save Inventory Export"
            };

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
    }
}