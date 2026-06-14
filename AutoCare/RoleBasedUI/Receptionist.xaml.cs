using Microsoft.Data.Sqlite;
using QRCoder;
using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media.Imaging;

namespace AutoCare
{
    public partial class Receptionist : UserControl
    {
        private string? selectedCustomerID = null; // .NET 10 Nullable string සක්‍රීය කිරීම
        private string selectedVehicleNo = string.Empty;

        private const string PlateStrictPattern1 = "^[A-Z]{2}-[A-Z]{3}-\\d{4}$"; // XX-XXX-1111
        private const string PlateStrictPattern2 = "^[A-Z]{2}-[A-Z]{2}-\\d{4}$"; // XX-XX-1111

        public Receptionist()
        {
            InitializeComponent();
            LoadCustomersGrid();
            LoadOwnersComboBox();
            LoadVehiclesGrid();
            LoadServicesComboBox();
            LoadJobVehiclesGrid();
        }

        // Wrapper to match XAML handler name (case-sensitive)
        private void dgvVehicles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DgvVehicles_SelectionChanged(sender, e);
        }

        // ==========================================
        // TAB 1: CUSTOMER MANAGEMENT CORE LOGIC
        // ==========================================
        private void LoadCustomersGrid(string filterKeyword = "")
        {
            try
            {
                using (SqliteConnection conn = DatabaseHelper.GetConnection())
                {
                    string query = string.IsNullOrWhiteSpace(filterKeyword)
                        ? "SELECT CustomerID, CustomerName, Phone, Email FROM Customers ORDER BY CustomerID DESC;"
                        : "SELECT CustomerID, CustomerName, Phone, Email FROM Customers WHERE CustomerName LIKE @Keyword OR Phone LIKE @Keyword ORDER BY CustomerID DESC;";

                    using (SqliteCommand cmd = new SqliteCommand(query, conn))
                    {
                        if (!string.IsNullOrWhiteSpace(filterKeyword))
                            cmd.Parameters.AddWithValue("@Keyword", $"%{filterKeyword.Trim()}%");

                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            DataTable dt = new DataTable();
                            dt.Load(reader);
                            dgvCustomers.ItemsSource = dt.DefaultView;
                        }
                    }
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Unable to access or load customer records.", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnSaveCustomer_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateFormInputs()) return;

            try
            {
                using (SqliteConnection conn = DatabaseHelper.GetConnection())
                {
                    string query = "INSERT INTO Customers (CustomerName, Phone, Email) VALUES (@Name, @Phone, @Email);";
                    using (SqliteCommand cmd = new SqliteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Name", txtCustomerName.Text.Trim());
                        cmd.Parameters.AddWithValue("@Phone", txtPhone.Text.Trim());
                        cmd.Parameters.AddWithValue("@Email", string.IsNullOrEmpty(txtEmail.Text) ? DBNull.Value : txtEmail.Text.Trim());
                        cmd.ExecuteNonQuery();
                    }

                    // get last inserted id and refresh owners with selection
                    using (var idCmd = new SqliteCommand("SELECT last_insert_rowid();", conn))
                    {
                        var id = idCmd.ExecuteScalar();
                        LoadCustomersGrid();
                        LoadOwnersComboBox();
                        if (id != null)
                        {
                            cmbOwners.SelectedValue = id.ToString();
                        }
                    }
                }

                MessageBox.Show("Customer registered successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                ClearCustomerFields();
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
            {
                MessageBox.Show("This phone number is already assigned to a registered customer.", "Registration Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception)
            {
                MessageBox.Show("Failed to save customer details.", "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnUpdateCustomer_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedCustomerID))
            {
                MessageBox.Show("Please select a customer profile from the grid first.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!ValidateFormInputs()) return;

            try
            {
                using (SqliteConnection conn = DatabaseHelper.GetConnection())
                {
                    string query = "UPDATE Customers SET CustomerName = @Name, Phone = @Phone, Email = @Email WHERE CustomerID = @ID;";
                    using (SqliteCommand cmd = new SqliteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Name", txtCustomerName.Text.Trim());
                        cmd.Parameters.AddWithValue("@Phone", txtPhone.Text.Trim());
                        cmd.Parameters.AddWithValue("@Email", string.IsNullOrEmpty(txtEmail.Text) ? DBNull.Value : txtEmail.Text.Trim());
                        cmd.Parameters.AddWithValue("@ID", selectedCustomerID);
                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Customer profile updated successfully!", "Update Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadCustomersGrid();
                LoadOwnersComboBox();
                ClearCustomerFields();
            }
            catch (Exception)
            {
                MessageBox.Show("Failed to save updates.", "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void dgvCustomers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgvCustomers.SelectedItem is DataRowView selectedRow)
            {
                selectedCustomerID = selectedRow["CustomerID"].ToString();
                txtCustomerName.Text = selectedRow["CustomerName"].ToString();
                txtPhone.Text = selectedRow["Phone"].ToString();
                txtEmail.Text = selectedRow["Email"].ToString();

                lblSelectedCustomerID.Text = selectedCustomerID;
                panelEditingIndicator.Visibility = Visibility.Visible;
                btnSaveCustomer.IsEnabled = false;
            }
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            LoadCustomersGrid(txtSearch.Text);
        }

        // ==========================================
        // TAB 2: VEHICLE MANAGEMENT CORE LOGIC
        // ==========================================
        private void LoadOwnersComboBox()
        {
            try
            {
                using (SqliteConnection conn = DatabaseHelper.GetConnection())
                {
                    string query = "SELECT CustomerID, CustomerName || ' (' || Phone || ')' AS DisplayText FROM Customers ORDER BY CustomerName ASC;";
                    using (SqliteCommand cmd = new SqliteCommand(query, conn))
                    {
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            DataTable dt = new DataTable();
                            dt.Load(reader);

                            cmbOwners.ItemsSource = dt.DefaultView;
                            cmbOwners.DisplayMemberPath = "DisplayText";
                            cmbOwners.SelectedValuePath = "CustomerID";
                            cmbOwners.Foreground = System.Windows.Media.Brushes.White;
                        }
                    }
                }
            }
            catch (Exception) { }
        }

        private void RefreshOwnersAndSelectLastInserted(string phoneLike)
        {
            try
            {
                using (SqliteConnection conn = DatabaseHelper.GetConnection())
                {
                    string query = "SELECT CustomerID, CustomerName || ' (' || Phone || ')' AS DisplayText FROM Customers ORDER BY CustomerName ASC;";
                    using (SqliteCommand cmd = new SqliteCommand(query, conn))
                    {
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            DataTable dt = new DataTable();
                            dt.Load(reader);
                            cmbOwners.ItemsSource = dt.DefaultView;
                            cmbOwners.DisplayMemberPath = "DisplayText";
                            cmbOwners.SelectedValuePath = "CustomerID";
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(phoneLike))
                    {
                        string selQuery = "SELECT CustomerID FROM Customers WHERE Phone = @Phone LIMIT 1;";
                        using (SqliteCommand selCmd = new SqliteCommand(selQuery, conn))
                        {
                            selCmd.Parameters.AddWithValue("@Phone", phoneLike);
                            var id = selCmd.ExecuteScalar();
                            if (id != null)
                            {
                                cmbOwners.SelectedValue = id.ToString();
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private void btnSaveVehicle_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtVehicleNo.Text) || string.IsNullOrWhiteSpace(txtModel.Text))
            {
                MessageBox.Show("Please fill in all the required fields (Vehicle Number and Brand & Model).", "Registration Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (cmbOwners.SelectedValue == null)
            {
                MessageBox.Show("Please select the registered customer (Owner) to assign to this vehicle.", "Registration Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string inputPlate = txtVehicleNo.Text ?? string.Empty;
            string normSep = Regex.Replace(inputPlate.Trim(), "\\s+", "-").ToUpper();
            normSep = Regex.Replace(normSep, "-+", "-");

            if (!Regex.IsMatch(normSep, PlateStrictPattern1) && !Regex.IsMatch(normSep, PlateStrictPattern2))
            {
                MessageBox.Show("The entered vehicle registration number format is invalid!\n\nValid examples:\n- XX-XXX-1111 (e.g. WP-ABC-1234)\n- XX-XX-1111 (e.g. WP-AB-1234)",
                                "Invalid Plate Format", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtVehicleNo.Focus();
                return;
            }
            string cleanPlate = Regex.Replace(normSep, "[^A-Z0-9]", string.Empty, RegexOptions.IgnoreCase).ToUpper();
            string formattedToStore = normSep;

            if (!string.IsNullOrWhiteSpace(txtYear.Text.Trim()))
            {
                string yearInput = txtYear.Text.Trim();
                int currentYear = DateTime.Now.Year;

                if (!yearInput.All(char.IsDigit) || yearInput.Length != 4)
                {
                    MessageBox.Show("Please enter a valid 4-digit numeric manufactured year (e.g., 2018).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtYear.Focus();
                    return;
                }

                int enteredYear = int.Parse(yearInput);
                if (enteredYear < 1900 || enteredYear > currentYear)
                {
                    MessageBox.Show($"The manufactured year must fall between 1900 and {currentYear}.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtYear.Focus();
                    return;
                }
            }

            try
            {
                using (SqliteConnection conn = DatabaseHelper.GetConnection())
                {
                    string query = "INSERT INTO Vehicles (VehicleNo, Model, Year, CustomerID) VALUES (@VehicleNo, @Model, @Year, @CustomerID);";
                    using (SqliteCommand cmd = new SqliteCommand(query, conn))
                    {
                        formattedToStore = FormatPlateForDisplay(cleanPlate, txtVehicleNo.Text);
                        cmd.Parameters.AddWithValue("@VehicleNo", formattedToStore);
                        cmd.Parameters.AddWithValue("@Model", txtModel.Text.Trim());
                        cmd.Parameters.AddWithValue("@Year", string.IsNullOrEmpty(txtYear.Text) ? DBNull.Value : txtYear.Text.Trim());
                        cmd.Parameters.AddWithValue("@CustomerID", cmbOwners.SelectedValue);
                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Vehicle details saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadVehiclesGrid();

                txtVehicleNo.Clear();
                txtModel.Clear();
                txtYear.Clear();

                if (!string.IsNullOrWhiteSpace(txtPhone.Text))
                    RefreshOwnersAndSelectLastInserted(txtPhone.Text.Trim());
                else
                    LoadOwnersComboBox();

                selectedVehicleNo = string.Empty;
                btnUpdateVehicle.IsEnabled = false;
                btnDeleteVehicle.IsEnabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Database Error: " + ex.Message, "System Failure", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadVehiclesGrid(string keyword = "")
        {
            try
            {
                using (SqliteConnection conn = DatabaseHelper.GetConnection())
                {
                    // 🛠️ FIXED: Changed JOIN to LEFT JOIN so vehicles show up even if owner linking has an anomaly
                    string query = string.IsNullOrWhiteSpace(keyword)
                        ? @"SELECT V.VehicleNo, V.Model, V.Year, C.CustomerID AS OwnerID, 
                           COALESCE(C.CustomerName, 'No Owner Assigned') AS OwnerName, 
                           COALESCE(C.Phone, 'N/A') AS Phone 
                    FROM Vehicles V 
                    LEFT JOIN Customers C ON V.CustomerID = C.CustomerID 
                    ORDER BY V.VehicleNo DESC;"
                        : @"SELECT V.VehicleNo, V.Model, V.Year, C.CustomerID AS OwnerID, 
                           COALESCE(C.CustomerName, 'No Owner Assigned') AS OwnerName, 
                           COALESCE(C.Phone, 'N/A') AS Phone 
                    FROM Vehicles V 
                    LEFT JOIN Customers C ON V.CustomerID = C.CustomerID 
                    WHERE (REPLACE(REPLACE(UPPER(V.VehicleNo),' ',''),'-','') LIKE @KeyNorm) 
                       OR UPPER(V.Model) LIKE @Key 
                       OR UPPER(COALESCE(C.CustomerName,'')) LIKE @Key 
                    ORDER BY V.VehicleNo DESC;";

                    using (SqliteCommand cmd = new SqliteCommand(query, conn))
                    {
                        if (!string.IsNullOrWhiteSpace(keyword))
                        {
                            string keyNorm = Regex.Replace(keyword, "[^A-Z0-9]", string.Empty, RegexOptions.IgnoreCase).ToUpper();
                            string keyUpper = keyword.ToUpper();
                            cmd.Parameters.AddWithValue("@KeyNorm", $"%{keyNorm}%");
                            cmd.Parameters.AddWithValue("@Key", $"%{keyUpper}%");
                        }

                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            DataTable dt = new DataTable();
                            dt.Load(reader);
                            foreach (DataRow r in dt.Rows)
                            {
                                string raw = (r["VehicleNo"] ?? string.Empty).ToString();
                                string norm = Regex.Replace(raw, "[^A-Z0-9]", string.Empty, RegexOptions.IgnoreCase).ToUpper();
                                string formatted = FormatPlateForDisplay(norm, raw);
                                r["VehicleNo"] = formatted;

                                if (!dt.Columns.Contains("NormVehicleNo"))
                                    dt.Columns.Add("NormVehicleNo", typeof(string));
                                r["NormVehicleNo"] = norm;
                            }

                            dgvVehicles.ItemsSource = dt.DefaultView;
                            dgvVehicles.SelectionChanged -= DgvVehicles_SelectionChanged;
                            dgvVehicles.SelectionChanged += DgvVehicles_SelectionChanged;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading vehicle records: " + ex.Message, "Database Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Nullable Reference parameters ටික .NET 10 වලට ගැලපෙන ලෙස ආරක්ෂිත කිරීම
        private string FormatPlateForDisplay(string? norm, string? original)
        {
            string cleanNorm = norm ?? string.Empty;
            string cleanOriginal = original ?? string.Empty;

            if (string.IsNullOrWhiteSpace(cleanNorm)) return cleanOriginal;

            var m1 = Regex.Match(cleanNorm, "^([A-Z]{2})([A-Z]{3})(\\d{1,4})$");
            if (m1.Success)
            {
                return $"{m1.Groups[1].Value}-{m1.Groups[2].Value} {m1.Groups[3].Value}";
            }
            var m2 = Regex.Match(cleanNorm, "^([A-Z]{2})([A-Z]{2})(\\d{3,4})$");
            if (m2.Success)
            {
                return $"{m2.Groups[1].Value} {m2.Groups[2].Value} {m2.Groups[3].Value}";
            }
            var md = Regex.Match(cleanNorm, "^(.*?)(\\d{3,4})$");
            if (md.Success)
            {
                string letters = md.Groups[1].Value;
                string digits = md.Groups[2].Value;
                if (letters.Length > 2)
                    return $"{letters.Substring(0, 2)}-{letters.Substring(2)} {digits}";
                return $"{letters} {digits}";
            }
            return cleanOriginal;
        }

        private void DgvVehicles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgvVehicles.SelectedItem is DataRowView row)
            {
                selectedVehicleNo = Regex.Replace(row["VehicleNo"].ToString() ?? string.Empty, "[^A-Z0-9]", string.Empty, RegexOptions.IgnoreCase).ToUpper();
                txtVehicleNo.Text = row["VehicleNo"].ToString();
                txtModel.Text = row["Model"].ToString();
                txtYear.Text = row["Year"].ToString();

                if (row.Row.Table.Columns.Contains("OwnerID") && row["OwnerID"] != DBNull.Value)
                {
                    cmbOwners.SelectedValue = row["OwnerID"].ToString();
                }
                else
                {
                    string ownerName = row["OwnerName"].ToString() ?? string.Empty;
                    foreach (var item in cmbOwners.Items)
                    {
                        if (item is DataRowView drv && (drv["DisplayText"].ToString() ?? string.Empty).StartsWith(ownerName))
                        {
                            cmbOwners.SelectedValue = drv["CustomerID"].ToString();
                            break;
                        }
                    }
                }

                btnUpdateVehicle.IsEnabled = true;
                btnDeleteVehicle.IsEnabled = true;
            }
            else
            {
                selectedVehicleNo = string.Empty;
                btnUpdateVehicle.IsEnabled = false;
                btnDeleteVehicle.IsEnabled = false;
            }
        }

        private void btnUpdateVehicle_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(selectedVehicleNo))
            {
                MessageBox.Show("Please select a vehicle from the fleet directory first.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtVehicleNo.Text) || string.IsNullOrWhiteSpace(txtModel.Text) || cmbOwners.SelectedValue == null)
            {
                MessageBox.Show("Vehicle number, model, and owner are required to update the record.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (SqliteConnection conn = DatabaseHelper.GetConnection())
                {
                    string q = "UPDATE Vehicles SET VehicleNo = @NewNo, Model = @Model, Year = @Year, CustomerID = @CustomerID WHERE REPLACE(REPLACE(UPPER(VehicleNo),' ',''),'-','') = @OldNo;";
                    using (SqliteCommand cmd = new SqliteCommand(q, conn))
                    {
                        string inputUpd = txtVehicleNo.Text ?? string.Empty;
                        string normUpd = Regex.Replace(inputUpd.Trim(), "\\s+", "-").ToUpper();
                        normUpd = Regex.Replace(normUpd, "-+", "-");
                        if (!Regex.IsMatch(normUpd, PlateStrictPattern1) && !Regex.IsMatch(normUpd, PlateStrictPattern2))
                        {
                            MessageBox.Show("The entered vehicle registration number format is invalid for update.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                        string newClean = Regex.Replace(normUpd, "[^A-Z0-9]", string.Empty, RegexOptions.IgnoreCase).ToUpper();
                        string newFormatted = normUpd;
                        cmd.Parameters.AddWithValue("@NewNo", newFormatted);
                        cmd.Parameters.AddWithValue("@Model", txtModel.Text.Trim());
                        cmd.Parameters.AddWithValue("@Year", string.IsNullOrEmpty(txtYear.Text) ? DBNull.Value : txtYear.Text.Trim());
                        cmd.Parameters.AddWithValue("@CustomerID", cmbOwners.SelectedValue);
                        cmd.Parameters.AddWithValue("@OldNo", selectedVehicleNo);
                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Vehicle record updated successfully.", "Update", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadVehiclesGrid();
                LoadJobVehiclesGrid();
                selectedVehicleNo = string.Empty;
                btnUpdateVehicle.IsEnabled = false;
                btnDeleteVehicle.IsEnabled = false;
                txtVehicleNo.Clear();
                txtModel.Clear();
                txtYear.Clear();
                cmbOwners.SelectedIndex = -1;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to update vehicle: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnDeleteVehicle_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(selectedVehicleNo))
            {
                MessageBox.Show("Please select a vehicle to delete.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show("Are you sure you want to delete the selected vehicle record?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                using (SqliteConnection conn = DatabaseHelper.GetConnection())
                {
                    string q = "DELETE FROM Vehicles WHERE REPLACE(REPLACE(UPPER(VehicleNo),' ',''),'-','') = @No;";
                    using (SqliteCommand cmd = new SqliteCommand(q, conn))
                    {
                        cmd.Parameters.AddWithValue("@No", selectedVehicleNo);
                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Vehicle removed successfully.", "Deleted", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadVehiclesGrid();
                LoadJobVehiclesGrid();
                selectedVehicleNo = string.Empty;
                txtVehicleNo.Clear();
                txtModel.Clear();
                txtYear.Clear();
                cmbOwners.SelectedIndex = -1;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to delete vehicle: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void txtVehicleSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            LoadVehiclesGrid(txtVehicleSearch.Text);
        }

        // ==========================================
        // UTILITIES & GLOBAL BUTTONS
        // ==========================================
        private void btnClearForm_Click(object sender, RoutedEventArgs e)
        {
            ClearCustomerFields();
        }

        private void btnLogout_Click(object sender, RoutedEventArgs e)
        {
            Window parentWindow = Window.GetWindow(this);
            if (parentWindow != null)
            {
                MainWindow loginWindow = new MainWindow();
                loginWindow.Show();
                parentWindow.Close();
            }
        }

        private bool ValidateFormInputs()
        {
            if (string.IsNullOrWhiteSpace(txtCustomerName.Text) || string.IsNullOrWhiteSpace(txtPhone.Text))
            {
                MessageBox.Show("Customer Name and Phone Number are required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            string phone = txtPhone.Text.Trim();
            if (!phone.All(char.IsDigit) || phone.Length != 10)
            {
                MessageBox.Show("Phone Number must be exactly 10 numeric digits.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        private void ClearCustomerFields()
        {
            txtCustomerName.Clear();
            txtPhone.Clear();
            txtEmail.Clear();
            selectedCustomerID = null;
            panelEditingIndicator.Visibility = Visibility.Collapsed;
            btnSaveCustomer.IsEnabled = true;
            dgvCustomers.SelectedItem = null;
        }

        // =========================================================================
        // TAB 3: INNOVATIVE FEATURE LOGIC (JOB CARD & LIVE RECEIPT SHEET)
        // =========================================================================
        private void LoadServicesComboBox()
        {
            try
            {
                using (SqliteConnection conn = DatabaseHelper.GetConnection())
                {
                    string query = "SELECT ServiceID, ServiceName || ' (Rs. ' || BasePrice || ')' AS DisplayText FROM Services;";
                    using (SqliteCommand cmd = new SqliteCommand(query, conn))
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        DataTable dt = new DataTable();
                        dt.Load(reader);
                        cmbServices.ItemsSource = dt.DefaultView;
                        cmbServices.DisplayMemberPath = "DisplayText";
                        cmbServices.SelectedValuePath = "ServiceID";
                    }
                }
            }
            catch (Exception) { }
        }

        

        private void LoadJobVehiclesGrid()
        {
            try
            {
                using (SqliteConnection conn = DatabaseHelper.GetConnection())
                {
                    string query = "SELECT VehicleNo, Model, Year FROM Vehicles ORDER BY VehicleNo DESC;";
                    using (SqliteCommand cmd = new SqliteCommand(query, conn))
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        DataTable dt = new DataTable();
                        dt.Load(reader);

                        // Format the plate numbers on Tab 3's grid too so everything stays uniform
                        foreach (DataRow r in dt.Rows)
                        {
                            string raw = (r["VehicleNo"] ?? string.Empty).ToString();
                            string norm = Regex.Replace(raw, "[^A-Z0-9]", string.Empty, RegexOptions.IgnoreCase).ToUpper();
                            r["VehicleNo"] = FormatPlateForDisplay(norm, raw);
                        }

                        dgvJobVehiclesSelection.ItemsSource = dt.DefaultView;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading active job selection grid: " + ex.Message, "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // -------------------------------------------------------------------------
        // ⚡ NEW: DATA GRID SELECTION CHANGED (සජීවීව රිසිට් පැනලය ලෝඩ් වන කොටස)
        // -------------------------------------------------------------------------
        private void dgvJobVehiclesSelection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgvJobVehiclesSelection.SelectedItem is DataRowView selectedRow)
            {
                string vehicleNo = selectedRow["VehicleNo"]?.ToString() ?? string.Empty;
                txtJobVehicleNo.Text = vehicleNo;

                if (string.IsNullOrWhiteSpace(vehicleNo)) return;

                rtbHistoryReport.Document.Blocks.Clear();
                FlowDocument doc = new FlowDocument();
                Paragraph paragraph = new Paragraph { FontFamily = new System.Windows.Media.FontFamily("Consolas") };

                try
                {
                    using (SqliteConnection conn = DatabaseHelper.GetConnection())
                    {
                        string query = @"SELECT J.JobCardID, S.ServiceName, J.MechanicName, J.DateReceived, J.JobStatus, C.CustomerName, C.Phone 
                                         FROM JobCards J
                                         JOIN Services S ON J.ServiceID = S.ServiceID
                                         JOIN Vehicles V ON J.VehicleNo = V.VehicleNo
                                         JOIN Customers C ON V.CustomerID = C.CustomerID
                                         WHERE J.VehicleNo = @VehicleNo
                                         ORDER BY J.JobCardID DESC;";

                        using (SqliteCommand cmd = new SqliteCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@VehicleNo", vehicleNo);
                            using (SqliteDataReader reader = cmd.ExecuteReader())
                            {
                                bool hasRecords = false;
                                int visitIndex = 1;

                                while (reader.Read())
                                {
                                    if (!hasRecords)
                                    {
                                        paragraph.Inlines.Add(new Bold(new Run($"AUTOCARE VEHICLE HISTORY PROFILE\n")) { Foreground = System.Windows.Media.Brushes.White });
                                        paragraph.Inlines.Add(new Run($"==============================================\n"));
                                        paragraph.Inlines.Add(new Run($"Target Plate : {vehicleNo}\n"));
                                        paragraph.Inlines.Add(new Run($"Owner Name   : {reader["CustomerName"]?.ToString() ?? "N/A"}\n"));
                                        paragraph.Inlines.Add(new Run($"Contact Phone: {reader["Phone"]?.ToString() ?? "N/A"}\n"));
                                        paragraph.Inlines.Add(new Run($"----------------------------------------------\n\n"));
                                        hasRecords = true;
                                    }

                                    paragraph.Inlines.Add(new Bold(new Run($"Visit #{visitIndex} [Ticket ID: #{reader["JobCardID"]}]\n")) { Foreground = System.Windows.Media.Brushes.White });
                                    paragraph.Inlines.Add(new Run($"  Service  : {reader["ServiceName"]?.ToString() ?? "N/A"}\n"));
                                    paragraph.Inlines.Add(new Run($"  Mechanic : {reader["MechanicName"]?.ToString() ?? "N/A"}\n"));
                                    paragraph.Inlines.Add(new Run($"  Schedule : {reader["DateReceived"]?.ToString() ?? "N/A"}\n"));
                                    paragraph.Inlines.Add(new Run($"  Job State: {reader["JobStatus"]?.ToString() ?? "N/A"}\n"));
                                    paragraph.Inlines.Add(new Run($"----------------------------------------------\n"));

                                    visitIndex++;
                                }

                                if (!hasRecords)
                                {
                                    paragraph.Inlines.Add(new Run("✨ First-Time Vehicle: No historical breakdown or maintenance visits recorded in system archive.") { Foreground = System.Windows.Media.Brushes.DarkGray, FontStyle = FontStyles.Italic });
                                }
                            }
                        }
                    }

                    doc.Blocks.Add(paragraph);
                    rtbHistoryReport.Document = doc;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to parse history stream: " + ex.Message, "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // -------------------------------------------------------------------------
        // ⚡ JOB TICKET CREATION ENGINE (පරණ References ඉවත් කර සුමට කළ කෝඩ් එක)
        // -------------------------------------------------------------------------
        private void btnCreateJobCard_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtJobVehicleNo.Text))
            {
                MessageBox.Show("Please select an active vehicle from the right-hand directory grid first.", "Selection Missing", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (cmbServices.SelectedValue == null)
            {
                MessageBox.Show("Please select the requested maintenance service category.", "Input Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (dpBookingDate.SelectedDate == null)
            {
                MessageBox.Show("Please select a scheduled appointment date for this booking.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (cmbTimeSlots.SelectedItem == null)
            {
                MessageBox.Show("Please choose a valid time slot for the maintenance appointment.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string mechanic = string.IsNullOrWhiteSpace(txtMechanicName.Text) ? "Unassigned" : txtMechanicName.Text.Trim();

            string chosenDateStr = dpBookingDate.SelectedDate.Value.ToString("yyyy-MM-dd");
            string chosenTimeStr = (cmbTimeSlots.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "TBD";
            string combinedBookingDateTime = $"{chosenDateStr} [{chosenTimeStr}]";

            long newlyGeneratedID = 0;

            try
            {
                using (SqliteConnection conn = DatabaseHelper.GetConnection())
                {
                    string query = @"INSERT INTO JobCards (VehicleNo, ServiceID, MechanicName, DateReceived, JobStatus) 
                                     VALUES (@VehicleNo, @ServiceID, @Mechanic, @DateReceived, 'Pending');
                                     SELECT last_insert_rowid();";

                    using (SqliteCommand cmd = new SqliteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@VehicleNo", txtJobVehicleNo.Text);
                        cmd.Parameters.AddWithValue("@ServiceID", cmbServices.SelectedValue ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Mechanic", mechanic);
                        cmd.Parameters.AddWithValue("@DateReceived", combinedBookingDateTime);

                        newlyGeneratedID = (long)(cmd.ExecuteScalar() ?? 0);
                    }
                }

                // ⚡ QR TOKEN GENERATION ENGINE
                string serviceText = cmbServices.Text ?? "Standard Maintenance";
                string qrPayloadString = $"AUTOCARE SERVICE TICKET\n" +
                                         $"====================\n" +
                                         $"Ticket ID  : #{newlyGeneratedID}\n" +
                                         $"Plate No   : {txtJobVehicleNo.Text}\n" +
                                         $"Service    : {serviceText}\n" +
                                         $"Mechanic   : {mechanic}\n" +
                                         $"APPOINTMENT: {combinedBookingDateTime}\n" +
                                         $"Status     : Pending";

                using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
                using (QRCodeData qrCodeData = qrGenerator.CreateQrCode(qrPayloadString, QRCodeGenerator.ECCLevel.Q))
                using (PngByteQRCode qrCode = new PngByteQRCode(qrCodeData))
                {
                    byte[] qrGraphArray = qrCode.GetGraphic(15);
                    using (MemoryStream stream = new MemoryStream(qrGraphArray))
                    {
                        BitmapImage bmpImage = new BitmapImage();
                        bmpImage.BeginInit();
                        bmpImage.CacheOption = BitmapCacheOption.OnLoad;
                        bmpImage.StreamSource = stream;
                        bmpImage.EndInit();

                        imgQrCode.Source = bmpImage;
                    }
                }

                txtQrPlaceholder.Visibility = Visibility.Collapsed;
                btnPrintToken.IsEnabled = true;

                MessageBox.Show($"Job Card Ticket #{newlyGeneratedID} successfully created for {combinedBookingDateTime}!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                // 🔄 Form එකේ තිබෙන Inputs රීසෙට් කිරීම සහ සජීවීව රිසිට් එක Refresh කිරීම
                txtJobVehicleNo.Clear();
                txtMechanicName.Clear();
                cmbServices.SelectedIndex = -1;
                cmbTimeSlots.SelectedIndex = -1;
                dpBookingDate.SelectedDate = null;

                // Refresh table display context
                LoadJobVehiclesGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to save operational job card: " + ex.Message, "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnPrintToken_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Sending high-resolution QR layout token blueprint slips directly to local print queue...", "Thermal Printer Interface", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}