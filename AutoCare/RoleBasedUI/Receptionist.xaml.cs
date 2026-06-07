using Microsoft.Data.Sqlite;
using System;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace AutoCare
{
    public partial class Receptionist : UserControl
    {
        private string selectedCustomerID = null;
            private string selectedVehicleNo = string.Empty;

            private const string PlateStrictPattern1 = "^[A-Z]{2}-[A-Z]{3}-\\d{4}$"; // XX-XXX-1111
            private const string PlateStrictPattern2 = "^[A-Z]{2}-[A-Z]{2}-\\d{4}$"; // XX-XX-1111

        public Receptionist()
        {
            InitializeComponent();
            LoadCustomersGrid();
            LoadOwnersComboBox();
            LoadVehiclesGrid();
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
                            // ensure every item visible uses the right foreground
                            cmbOwners.Foreground = System.Windows.Media.Brushes.White;
                        }
                    }
                }
            }
            catch (Exception) { }
        }

        private void RefreshOwnersAndSelectLastInserted(string phoneLike)
        {
            // reload owners and select the owner matching phoneLike if exists
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

                    // try to find newly inserted customer by phone
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
            // 1. Presence validation: Ensure mandatory fields are not left blank
            if (string.IsNullOrWhiteSpace(txtVehicleNo.Text) || string.IsNullOrWhiteSpace(txtModel.Text))
            {
                MessageBox.Show("Please fill in all the required fields (Vehicle Number and Brand & Model).", "Registration Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Ensure a registered customer profile is selected from the owner dropdown menu
            if (cmbOwners.SelectedValue == null)
            {
                MessageBox.Show("Please select the registered customer (Owner) to assign to this vehicle.", "Registration Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 3. Normalize the vehicle plate number input by trimming and normalizing separators
            string inputPlate = txtVehicleNo.Text ?? string.Empty;
            string normSep = Regex.Replace(inputPlate.Trim(), "\\s+", "-").ToUpper();
            normSep = Regex.Replace(normSep, "-+", "-");

            if (!Regex.IsMatch(normSep, PlateStrictPattern1) && !Regex.IsMatch(normSep, PlateStrictPattern2))
            {
                MessageBox.Show("The entered vehicle registration number format is invalid!\n\nValid examples (after normalization):\n- XX-XXX-1111 (e.g. WP-ABC-1234)\n- XX-XX-1111 (e.g. WP-AB-1234)",
                                "Invalid Plate Format", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtVehicleNo.Focus();
                return;
            }
            // normalized string without separators for internal matching
            string cleanPlate = Regex.Replace(normSep, "[^A-Z0-9]", string.Empty, RegexOptions.IgnoreCase).ToUpper();
            // formatted string to store (keep hyphens as normalized)
            string formattedToStore = normSep;

            // 5. Conduct manufactured year boundary evaluation if data is provided
            if (!string.IsNullOrWhiteSpace(txtYear.Text.Trim()))
            {
                string yearInput = txtYear.Text.Trim();
                int currentYear = DateTime.Now.Year;

                // Verify that the year field consists strictly of exactly 4 numeric characters
                if (!yearInput.All(char.IsDigit) || yearInput.Length != 4)
                {
                    MessageBox.Show("Please enter a valid 4-digit numeric manufactured year (e.g., 2018).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtYear.Focus();
                    return;
                }

                int enteredYear = int.Parse(yearInput);

                // Enforce practical automotive age boundaries (from year 1900 up to the current calendar year)
                if (enteredYear < 1900 || enteredYear > currentYear)
                {
                    MessageBox.Show($"The manufactured year must fall between 1900 and {currentYear}.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtYear.Focus();
                    return;
                }
            }

            // 6. Execute data insertion into the SQLite workspace without using a 'Brand' column mapping
            try
            {
                using (SqliteConnection conn = DatabaseHelper.GetConnection())
                {
                    string query = "INSERT INTO Vehicles (VehicleNo, Model, Year, CustomerID) VALUES (@VehicleNo, @Model, @Year, @CustomerID);";
                    using (SqliteCommand cmd = new SqliteCommand(query, conn))
                    {
                        // store a user-friendly formatted plate in the DB
                        formattedToStore = FormatPlateForDisplay(cleanPlate, txtVehicleNo.Text);
                        cmd.Parameters.AddWithValue("@VehicleNo", formattedToStore);
                        cmd.Parameters.AddWithValue("@Model", txtModel.Text.Trim());
                        cmd.Parameters.AddWithValue("@Year", string.IsNullOrEmpty(txtYear.Text) ? DBNull.Value : txtYear.Text.Trim());
                        cmd.Parameters.AddWithValue("@CustomerID", cmbOwners.SelectedValue);

                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Vehicle details saved and linked to the customer profile successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadVehiclesGrid();

                // Clear form
                txtVehicleNo.Clear();
                txtModel.Clear();
                txtYear.Clear();

                // Refresh owners list and try to select the owner by phone
                if (!string.IsNullOrWhiteSpace(txtPhone.Text))
                    RefreshOwnersAndSelectLastInserted(txtPhone.Text.Trim());
                else
                    LoadOwnersComboBox();

                selectedVehicleNo = null;
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
                    // Select maps across VehicleNo, Model, Year, and the Owner details
                    string query = string.IsNullOrWhiteSpace(keyword)
                        ? "SELECT V.VehicleNo, V.Model, V.Year, C.CustomerID AS OwnerID, C.CustomerName AS OwnerName, C.Phone FROM Vehicles V JOIN Customers C ON V.CustomerID = C.CustomerID ORDER BY V.VehicleNo DESC;"
                        : "SELECT V.VehicleNo, V.Model, V.Year, C.CustomerID AS OwnerID, C.CustomerName AS OwnerName, C.Phone FROM Vehicles V JOIN Customers C ON V.CustomerID = C.CustomerID WHERE (REPLACE(REPLACE(UPPER(V.VehicleNo),' ',''),'-','') LIKE @KeyNorm) OR UPPER(V.Model) LIKE @Key OR UPPER(C.CustomerName) LIKE @Key ORDER BY V.VehicleNo DESC;";

                    using (SqliteCommand cmd = new SqliteCommand(query, conn))
                    {
                        if (!string.IsNullOrWhiteSpace(keyword))
                        {
                            // normalized key for plate matching (remove non-alphanumeric)
                            string keyNorm = Regex.Replace(keyword ?? string.Empty, "[^A-Z0-9]", string.Empty, RegexOptions.IgnoreCase).ToUpper();
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
                                // format for display: try patterns
                                string formatted = FormatPlateForDisplay(norm, raw);
                                // replace VehicleNo cell with formatted string so grid shows user-friendly value
                                r["VehicleNo"] = formatted;
                                // store normalized value in a hidden column if needed for selection
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
            catch (Exception) { }
        }

        private string FormatPlateForDisplay(string norm, string original)
        {
            if (string.IsNullOrWhiteSpace(norm)) return original;
            // Pattern1: AA + AAA + digits -> AA-AAA NNNN
            var m1 = Regex.Match(norm, "^([A-Z]{2})([A-Z]{3})(\\d{1,4})$");
            if (m1.Success)
            {
                return $"{m1.Groups[1].Value}-{m1.Groups[2].Value} {m1.Groups[3].Value}";
            }
            // Pattern2: AA + AA + digits(3-4) -> AA AA NNNN
            var m2 = Regex.Match(norm, "^([A-Z]{2})([A-Z]{2})(\\d{3,4})$");
            if (m2.Success)
            {
                return $"{m2.Groups[1].Value} {m2.Groups[2].Value} {m2.Groups[3].Value}";
            }
            // Fallback: if original contained separators, return original; else attempt split before last 3-4 digits
            var md = Regex.Match(norm, "^(.*?)(\\d{3,4})$");
            if (md.Success)
            {
                string letters = md.Groups[1].Value;
                string digits = md.Groups[2].Value;
                if (letters.Length > 2)
                    return $"{letters.Substring(0,2)}-{letters.Substring(2)} {digits}";
                return $"{letters} {digits}";
            }
            return original;
        }

        private void DgvVehicles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgvVehicles.SelectedItem is DataRowView row)
            {
                // normalize stored vehicle number to expected format
                selectedVehicleNo = Regex.Replace(row["VehicleNo"].ToString() ?? string.Empty, "[^A-Z0-9]", string.Empty, RegexOptions.IgnoreCase).ToUpper();
                txtVehicleNo.Text = row["VehicleNo"].ToString();
                txtModel.Text = row["Model"].ToString();
                txtYear.Text = row["Year"].ToString();

                // Prefer selecting owner by OwnerID if query included it
                if (row.Row.Table.Columns.Contains("OwnerID") && row["OwnerID"] != DBNull.Value)
                {
                    cmbOwners.SelectedValue = row["OwnerID"].ToString();
                }
                else
                {
                    string ownerName = row["OwnerName"].ToString();
                    foreach (var item in cmbOwners.Items)
                    {
                        if (item is DataRowView drv && drv["DisplayText"].ToString().StartsWith(ownerName))
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
                selectedVehicleNo = null;
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
                        // normalize and validate update plate (same rules as create)
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
                selectedVehicleNo = null;
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
                selectedVehicleNo = null;
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
    }
}