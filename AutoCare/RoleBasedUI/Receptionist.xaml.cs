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
        private string? selectedCustomerID = null; // Nullable string support (nullable reference type)
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

                        // THE REAL FIX: clear PrimaryKey and Constraints to avoid DataTable constraint issues
                        dt.PrimaryKey = null;
                        dt.Constraints.Clear();

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
                MessageBox.Show("Failed to load job vehicles or job cards: " + ex.Message, "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
                    string query = string.IsNullOrWhiteSpace(keyword)
                        ? @"SELECT V.VehicleNo, V.Model, V.Year, V.CustomerID AS OwnerID, 
                           COALESCE(C.CustomerName, 'No Owner Assigned') AS OwnerName, 
                           COALESCE(C.Phone, 'N/A') AS Phone 
                    FROM Vehicles V 
                    LEFT JOIN Customers C ON V.CustomerID = C.CustomerID 
                    ORDER BY V.VehicleNo DESC;"
                        : @"SELECT V.VehicleNo, V.Model, V.Year, V.CustomerID AS OwnerID, 
                           COALESCE(C.CustomerName, 'No Owner Assigned') AS OwnerName, 
                           COALESCE(C.Phone, 'N/A') AS Phone 
                    FROM Vehicles V 
                    LEFT JOIN Customers C ON V.CustomerID = C.CustomerID 
                    WHERE (REPLACE(REPLACE(UPPER(V.VehicleNo),' ',''),'-','') LIKE @KeyNorm) 
                       OR UPPER(V.Model) LIKE @Key 
                       OR UPPER(COALESCE(C.CustomerName, '')) LIKE @Key 
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

                            // THE REAL FIX: remove unique constraints in the DataTable to prevent in-memory conflicts
                            dt.PrimaryKey = null;
                            dt.Constraints.Clear();

                            foreach (DataRow r in dt.Rows)
                            {
                                string raw = (r["VehicleNo"] ?? string.Empty).ToString();
                                string norm = Regex.Replace(raw, "[^A-Z0-9]", string.Empty, RegexOptions.IgnoreCase).ToUpper();
                                string formatted = FormatPlateForDisplay(norm, raw);

                                // Now the value can be modified without constraint issues
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
        // Ensure nullable reference parameters are handled safely
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



        // Vehicle grid loader removed (duplicate). Remaining LoadVehiclesGrid implementation is lower in the file.

        // -------------------------------------------------------------------------
        // NEW: DATA GRID SELECTION CHANGED (loads the live receipt panel)
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
                        // Normalize vehicle identifier for robust matching against stored values
                        string norm = Regex.Replace(vehicleNo, "[^A-Z0-9]", string.Empty, RegexOptions.IgnoreCase).ToUpper();

                        string query = @"SELECT J.JobCardID, S.ServiceName, J.MechanicName, J.DateReceived, J.JobStatus, C.CustomerName, C.Phone
                                 FROM JobCards J
                                 JOIN Services S ON J.ServiceID = S.ServiceID
                                 JOIN Vehicles V ON REPLACE(REPLACE(UPPER(V.VehicleNo),' ',''),'-','') = @Norm
                                 JOIN Customers C ON V.CustomerID = C.CustomerID
                                 WHERE REPLACE(REPLACE(UPPER(J.VehicleNo),' ',''),'-','') = @Norm
                                 ORDER BY J.JobCardID DESC;";

                        using (SqliteCommand cmd = new SqliteCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@Norm", norm);
                            using (SqliteDataReader reader = cmd.ExecuteReader())
                            {
                                bool hasRecords = false;
                                int visitIndex = 1;

                                while (reader.Read())
                                {
                                    long currentJobCardID = Convert.ToInt64(reader["JobCardID"]);
                                    string currentStatus = reader["JobStatus"]?.ToString() ?? "Pending";

                                    if (!hasRecords)
                                    {
                                        paragraph.Inlines.Add(new Bold(new Run($"AUTOCARE VEHICLE HISTORY PROFILE\n")) { Foreground = System.Windows.Media.Brushes.LightGreen });
                                        paragraph.Inlines.Add(new Run($"==============================================\n"));
                                        paragraph.Inlines.Add(new Run($"Target Plate : {vehicleNo}\n"));
                                        paragraph.Inlines.Add(new Run($"Owner Name   : {reader["CustomerName"]?.ToString() ?? "N/A"}\n"));
                                        paragraph.Inlines.Add(new Run($"Contact Phone: {reader["Phone"]?.ToString() ?? "N/A"}\n"));
                                        paragraph.Inlines.Add(new Run($"----------------------------------------------\n\n"));
                                        hasRecords = true;
                                    }

                                    paragraph.Inlines.Add(new Bold(new Run($"Visit #{visitIndex} [Ticket ID: #{currentJobCardID}]\n")));
                                    paragraph.Inlines.Add(new Run($"  Service  : {reader["ServiceName"]?.ToString() ?? "N/A"}\n"));
                                    paragraph.Inlines.Add(new Run($"  Mechanic : {reader["MechanicName"]?.ToString() ?? "N/A"}\n"));
                                    paragraph.Inlines.Add(new Run($"  Schedule : {reader["DateReceived"]?.ToString() ?? "N/A"}\n"));

                                    paragraph.Inlines.Add(new Run("  Job State: "));
                                    if (currentStatus.Equals("Pending", StringComparison.OrdinalIgnoreCase))
                                    {
                                        Run statusRun = new Run("Pending ") { Foreground = System.Windows.Media.Brushes.Tomato, FontStyle = FontStyles.Italic };
                                        paragraph.Inlines.Add(statusRun);

                                        Run completeAction = new Run("[MARK AS COMPLETE]")
                                        {
                                            Foreground = System.Windows.Media.Brushes.DeepSkyBlue,
                                            Cursor = System.Windows.Input.Cursors.Hand
                                        };

                                        completeAction.MouseEnter += (s, ev) => { System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Hand; };
                                        completeAction.MouseLeave += (s, ev) => { System.Windows.Input.Mouse.OverrideCursor = null; };

                                        completeAction.MouseDown += (s, ev) =>
                                        {
                                            ExecuteDirectStatusUpdate(currentJobCardID);
                                        };

                                        paragraph.Inlines.Add(completeAction);
                                        paragraph.Inlines.Add(new Run("\n"));
                                    }
                                    else
                                    {
                                        paragraph.Inlines.Add(new Run("Complete ✓\n") { Foreground = System.Windows.Media.Brushes.LimeGreen });
                                    }

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

        // Method to mark job card status as 'Complete' in the database
        private void ExecuteDirectStatusUpdate(long jobCardID)
        {
            var confirm = MessageBox.Show($"Are you sure you want to change Ticket #{jobCardID} status to COMPLETED?",
                                         "Confirm Status Shift", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                // 1. Set the JobCards status to 'Complete' in the database
                using (SqliteConnection conn = DatabaseHelper.GetConnection())
                {
                    string query = "UPDATE JobCards SET JobStatus = 'Complete' WHERE JobCardID = @ID;";
                    using (SqliteCommand cmd = new SqliteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@ID", jobCardID);
                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show($"Ticket #{jobCardID} successfully closed and marked as Complete!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                // 2. Refresh fix: clear and reset the RichTextBox Document
                rtbHistoryReport.Document.Blocks.Clear();
                rtbHistoryReport.Document = new FlowDocument();

                // 3. Force the selected grid row to re-trigger selection changed to reload updated data
                if (dgvJobVehiclesSelection.SelectedItem != null)
                {
                    // SelectionChanged Event 
                    dgvJobVehiclesSelection_SelectionChanged(dgvJobVehiclesSelection, null!);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to shift operational status context: " + ex.Message, "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // -------------------------------------------------------------------------
        // ⚡ JOB TICKET CREATION ENGINE 
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
                // Validate booking date is strictly after today
                if (dpBookingDate.SelectedDate.HasValue)
                {
                    var selectedDate = dpBookingDate.SelectedDate.Value.Date;
                    if (selectedDate <= DateTime.Now.Date)
                    {
                        MessageBox.Show("Booking date must be after the current date. Please choose a future date.", "Invalid Booking Date", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                // Prevent duplicate service booking at same date/time slot
                string chosenTimeStrLocal = (cmbTimeSlots.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? string.Empty;
                string bookingDateOnly = dpBookingDate.SelectedDate?.ToString("yyyy-MM-dd") ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(bookingDateOnly) && !string.IsNullOrWhiteSpace(chosenTimeStrLocal) && cmbServices.SelectedValue != null)
                {
                    using (SqliteConnection tmpConn = DatabaseHelper.GetConnection())
                    {
                        string dupQuery = "SELECT COUNT(*) FROM JobCards WHERE ServiceID = @ServiceID AND DateReceived LIKE @DateLike;";
                        using (var dupCmd = new SqliteCommand(dupQuery, tmpConn))
                        {
                            dupCmd.Parameters.AddWithValue("@ServiceID", cmbServices.SelectedValue);
                            dupCmd.Parameters.AddWithValue("@DateLike", bookingDateOnly + "%" + chosenTimeStrLocal + "%");
                            long existing = (long)(dupCmd.ExecuteScalar() ?? 0L);
                            if (existing > 0)
                            {
                                MessageBox.Show("The selected service is already booked for the chosen date and time. Please pick a different time or service.", "Scheduling Conflict", MessageBoxButton.OK, MessageBoxImage.Warning);
                                return;
                            }
                        }
                    }
                }

                using (SqliteConnection conn = DatabaseHelper.GetConnection())
                {
                    string query = @"INSERT INTO JobCards (VehicleNo, ServiceID, MechanicName, DateReceived, JobStatus) 
                                     VALUES (@VehicleNo, @ServiceID, @Mechanic, @DateReceived, 'Pending');
                                     SELECT last_insert_rowid();";

                    using (SqliteCommand cmd = new SqliteCommand(query, conn))
                    {
                        // Ensure the Plate value matches the Vehicles.VehicleNo format stored in DB
                        string rawPlate = txtJobVehicleNo.Text ?? string.Empty;
                        string cleanPlate = Regex.Replace(rawPlate, "[^A-Z0-9]", string.Empty, RegexOptions.IgnoreCase).ToUpper();
                        string formattedPlate = FormatPlateForDisplay(cleanPlate, rawPlate);

                        // Verify vehicle exists in Vehicles table and get exact stored VehicleNo
                        string normOnly = cleanPlate; // already normalized
                        string findQuery = "SELECT VehicleNo FROM Vehicles WHERE REPLACE(REPLACE(UPPER(VehicleNo),' ',''),'-','') = @Norm LIMIT 1;";
                        using (var findCmd = new SqliteCommand(findQuery, conn))
                        {
                            findCmd.Parameters.AddWithValue("@Norm", normOnly);
                            var dbVal = findCmd.ExecuteScalar();
                            if (dbVal == null || dbVal == DBNull.Value)
                            {
                                MessageBox.Show($"Selected vehicle '{txtJobVehicleNo.Text}' is not registered in the system. Please add the vehicle before creating a job card.", "Vehicle Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                                return;
                            }
                            // Use the exact stored representation to satisfy FK
                            formattedPlate = dbVal.ToString();
                        }

                        cmd.Parameters.AddWithValue("@VehicleNo", formattedPlate);
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

                // Reset inputs and Refresh live receipt in the form
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