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
        private string? selectedCustomerID = null;
        // Always holds pure alphanumeric uppercase (e.g. WPABC1234) — used in WHERE clauses
        private string selectedVehicleNo = string.Empty;

        private const string PlateStrictPattern1 = "^[A-Z]{2}-[A-Z]{3}-\\d{4}$";
        private const string PlateStrictPattern2 = "^[A-Z]{2}-[A-Z]{2}-\\d{4}$";

        public Receptionist()
        {
            InitializeComponent();
            LoadCustomersGrid();
            LoadOwnersComboBox();
            LoadVehiclesGrid();
            LoadServicesComboBox();
            LoadJobVehiclesGrid();
        }

        // ==========================================
        // SHARED HELPERS
        // ==========================================

        /// <summary>Strip everything except A-Z and 0-9, return uppercase. Used for WHERE clause matching.</summary>
        private static string NormalizePlate(string? input)
            => string.IsNullOrWhiteSpace(input)
               ? string.Empty
               : Regex.Replace(input, "[^A-Z0-9]", string.Empty, RegexOptions.IgnoreCase).ToUpper();

        /// <summary>
        /// Convert user-typed input to the canonical storage format (WP-ABC-1234 / WP-AB-1234).
        /// Collapses spaces/multiple-dashes into single dashes, uppercases.
        /// </summary>
        private static string ToStorageFormat(string input)
        {
            string s = Regex.Replace(input.Trim(), @"\s+", "-").ToUpper();
            return Regex.Replace(s, "-+", "-");
        }

        /// <summary>Pretty-print a normalised plate for display only — never saved back to DB.</summary>
        private string FormatPlateForDisplay(string? norm, string? original)
        {
            string n = norm ?? string.Empty;
            string o = original ?? string.Empty;
            if (string.IsNullOrWhiteSpace(n)) return o;

            var m1 = Regex.Match(n, @"^([A-Z]{2})([A-Z]{3})(\d{1,4})$");
            if (m1.Success) return $"{m1.Groups[1].Value}-{m1.Groups[2].Value} {m1.Groups[3].Value}";

            var m2 = Regex.Match(n, @"^([A-Z]{2})([A-Z]{2})(\d{3,4})$");
            if (m2.Success) return $"{m2.Groups[1].Value} {m2.Groups[2].Value} {m2.Groups[3].Value}";

            var md = Regex.Match(n, @"^(.*?)(\d{3,4})$");
            if (md.Success)
            {
                string letters = md.Groups[1].Value;
                string digits = md.Groups[2].Value;
                return letters.Length > 2
                    ? $"{letters.Substring(0, 2)}-{letters.Substring(2)} {digits}"
                    : $"{letters} {digits}";
            }
            return o;
        }

        // ==========================================
        // TAB 1: CUSTOMER MANAGEMENT
        // ==========================================
        private void LoadCustomersGrid(string filterKeyword = "")
        {
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                string query = string.IsNullOrWhiteSpace(filterKeyword)
                    ? "SELECT CustomerID, CustomerName, Phone, Email FROM Customers ORDER BY CustomerID DESC;"
                    : "SELECT CustomerID, CustomerName, Phone, Email FROM Customers WHERE CustomerName LIKE @Keyword OR Phone LIKE @Keyword ORDER BY CustomerID DESC;";

                using var cmd = new SqliteCommand(query, conn);
                if (!string.IsNullOrWhiteSpace(filterKeyword))
                    cmd.Parameters.AddWithValue("@Keyword", $"%{filterKeyword.Trim()}%");

                using var reader = cmd.ExecuteReader();
                var dt = new DataTable();
                dt.Load(reader);
                dgvCustomers.ItemsSource = dt.DefaultView;
            }
            catch
            {
                MessageBox.Show("Unable to load customer records.", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnSaveCustomer_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateFormInputs()) return;
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                using (var cmd = new SqliteCommand(
                    "INSERT INTO Customers (CustomerName, Phone, Email) VALUES (@Name, @Phone, @Email);", conn))
                {
                    cmd.Parameters.AddWithValue("@Name", txtCustomerName.Text.Trim());
                    cmd.Parameters.AddWithValue("@Phone", txtPhone.Text.Trim());
                    cmd.Parameters.AddWithValue("@Email",
                        string.IsNullOrEmpty(txtEmail.Text) ? DBNull.Value : (object)txtEmail.Text.Trim());
                    cmd.ExecuteNonQuery();
                }

                using (var idCmd = new SqliteCommand("SELECT last_insert_rowid();", conn))
                {
                    var id = idCmd.ExecuteScalar();
                    LoadCustomersGrid();
                    LoadOwnersComboBox();
                    if (id != null) cmbOwners.SelectedValue = id.ToString();
                }

                MessageBox.Show("Customer registered successfully!", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                ClearCustomerFields();
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
            {
                MessageBox.Show("This phone number is already assigned to a registered customer.",
                    "Registration Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch
            {
                MessageBox.Show("Failed to save customer details.", "System Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnUpdateCustomer_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedCustomerID))
            {
                MessageBox.Show("Please select a customer profile from the grid first.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!ValidateFormInputs()) return;
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                using var cmd = new SqliteCommand(
                    "UPDATE Customers SET CustomerName=@Name, Phone=@Phone, Email=@Email WHERE CustomerID=@ID;", conn);
                cmd.Parameters.AddWithValue("@Name", txtCustomerName.Text.Trim());
                cmd.Parameters.AddWithValue("@Phone", txtPhone.Text.Trim());
                cmd.Parameters.AddWithValue("@Email",
                    string.IsNullOrEmpty(txtEmail.Text) ? DBNull.Value : (object)txtEmail.Text.Trim());
                cmd.Parameters.AddWithValue("@ID", selectedCustomerID);
                cmd.ExecuteNonQuery();

                MessageBox.Show("Customer profile updated successfully!", "Update Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                LoadCustomersGrid();
                LoadOwnersComboBox();
                ClearCustomerFields();
            }
            catch
            {
                MessageBox.Show("Failed to save updates.", "System Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void dgvCustomers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgvCustomers.SelectedItem is DataRowView row)
            {
                selectedCustomerID = row["CustomerID"].ToString();
                txtCustomerName.Text = row["CustomerName"].ToString();
                txtPhone.Text = row["Phone"].ToString();
                txtEmail.Text = row["Email"].ToString();
                lblSelectedCustomerID.Text = selectedCustomerID;
                panelEditingIndicator.Visibility = Visibility.Visible;
                btnSaveCustomer.IsEnabled = false;
            }
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
            => LoadCustomersGrid(txtSearch.Text);

        // ==========================================
        // TAB 2: VEHICLE MANAGEMENT
        // ==========================================
        private void LoadOwnersComboBox()
        {
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                using var cmd = new SqliteCommand(
                    "SELECT CustomerID, CustomerName || ' (' || Phone || ')' AS DisplayText FROM Customers ORDER BY CustomerName ASC;",
                    conn);
                using var reader = cmd.ExecuteReader();
                var dt = new DataTable();
                dt.Load(reader);
                cmbOwners.ItemsSource = dt.DefaultView;
                cmbOwners.DisplayMemberPath = "DisplayText";
                cmbOwners.SelectedValuePath = "CustomerID";
                cmbOwners.Foreground = System.Windows.Media.Brushes.White;
            }
            catch { }
        }

        private void RefreshOwnersAndSelectLastInserted(string phone)
        {
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                using (var cmd = new SqliteCommand(
                    "SELECT CustomerID, CustomerName || ' (' || Phone || ')' AS DisplayText FROM Customers ORDER BY CustomerName ASC;",
                    conn))
                using (var reader = cmd.ExecuteReader())
                {
                    var dt = new DataTable();
                    dt.Load(reader);
                    cmbOwners.ItemsSource = dt.DefaultView;
                    cmbOwners.DisplayMemberPath = "DisplayText";
                    cmbOwners.SelectedValuePath = "CustomerID";
                }

                if (!string.IsNullOrWhiteSpace(phone))
                {
                    using var selCmd = new SqliteCommand(
                        "SELECT CustomerID FROM Customers WHERE Phone = @Phone LIMIT 1;", conn);
                    selCmd.Parameters.AddWithValue("@Phone", phone);
                    var id = selCmd.ExecuteScalar();
                    if (id != null) cmbOwners.SelectedValue = id.ToString();
                }
            }
            catch { }
        }

        private void btnSaveVehicle_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtVehicleNo.Text) || string.IsNullOrWhiteSpace(txtModel.Text))
            {
                MessageBox.Show("Please fill in Vehicle Number and Brand & Model.", "Registration Warning",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (cmbOwners.SelectedValue == null)
            {
                MessageBox.Show("Please select the owner for this vehicle.", "Registration Warning",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // FIX: always produce the canonical storage format first, then validate it
            string storageFormat = ToStorageFormat(txtVehicleNo.Text);
            if (!Regex.IsMatch(storageFormat, PlateStrictPattern1) &&
                !Regex.IsMatch(storageFormat, PlateStrictPattern2))
            {
                MessageBox.Show(
                    "Invalid registration number format.\n\nValid examples:\n  WP-ABC-1234\n  WP-AB-1234",
                    "Invalid Plate Format", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtVehicleNo.Focus();
                return;
            }

            if (!string.IsNullOrWhiteSpace(txtYear.Text))
            {
                string y = txtYear.Text.Trim();
                if (!y.All(char.IsDigit) || y.Length != 4)
                {
                    MessageBox.Show("Please enter a valid 4-digit year.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtYear.Focus(); return;
                }
                int yr = int.Parse(y);
                if (yr < 1900 || yr > DateTime.Now.Year)
                {
                    MessageBox.Show($"Year must be between 1900 and {DateTime.Now.Year}.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtYear.Focus(); return;
                }
            }

            try
            {
                using var conn = DatabaseHelper.GetConnection();
                using var cmd = new SqliteCommand(
                    "INSERT INTO Vehicles (VehicleNo, Model, Year, CustomerID) VALUES (@VehicleNo,@Model,@Year,@CustomerID);",
                    conn);
                // FIX: save storageFormat (WP-ABC-1234) — consistent, predictable
                cmd.Parameters.AddWithValue("@VehicleNo", storageFormat);
                cmd.Parameters.AddWithValue("@Model", txtModel.Text.Trim());
                cmd.Parameters.AddWithValue("@Year",
                    string.IsNullOrEmpty(txtYear.Text) ? DBNull.Value : (object)txtYear.Text.Trim());
                cmd.Parameters.AddWithValue("@CustomerID", cmbOwners.SelectedValue);
                cmd.ExecuteNonQuery();

                MessageBox.Show("Vehicle saved successfully!", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                LoadVehiclesGrid();
                LoadJobVehiclesGrid();
                txtVehicleNo.Clear(); txtModel.Clear(); txtYear.Clear();
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
                MessageBox.Show("Database Error: " + ex.Message, "System Failure",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadVehiclesGrid(string keyword = "")
        {
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                string query = string.IsNullOrWhiteSpace(keyword)
                    ? @"SELECT V.VehicleNo, V.Model, V.Year, V.CustomerID AS OwnerID,
                               COALESCE(C.CustomerName,'No Owner Assigned') AS OwnerName,
                               COALESCE(C.Phone,'N/A') AS Phone
                        FROM Vehicles V
                        LEFT JOIN Customers C ON V.CustomerID = C.CustomerID
                        ORDER BY V.VehicleNo DESC;"
                    : @"SELECT V.VehicleNo, V.Model, V.Year, V.CustomerID AS OwnerID,
                               COALESCE(C.CustomerName,'No Owner Assigned') AS OwnerName,
                               COALESCE(C.Phone,'N/A') AS Phone
                        FROM Vehicles V
                        LEFT JOIN Customers C ON V.CustomerID = C.CustomerID
                        WHERE REPLACE(REPLACE(UPPER(V.VehicleNo),' ',''),'-','') LIKE @KeyNorm
                           OR UPPER(V.Model) LIKE @Key
                           OR UPPER(COALESCE(C.CustomerName,'')) LIKE @Key
                        ORDER BY V.VehicleNo DESC;";

                using var cmd = new SqliteCommand(query, conn);
                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    cmd.Parameters.AddWithValue("@KeyNorm", $"%{NormalizePlate(keyword)}%");
                    cmd.Parameters.AddWithValue("@Key", $"%{keyword.ToUpper()}%");
                }

                using var reader = cmd.ExecuteReader();
                var dt = new DataTable();
                dt.Load(reader);
                dt.PrimaryKey = null;
                dt.Constraints.Clear();

                // FIX: add RawVehicleNo BEFORE the loop so every row can use it
                dt.Columns.Add("RawVehicleNo", typeof(string));

                foreach (DataRow r in dt.Rows)
                {
                    string raw = r["VehicleNo"]?.ToString() ?? string.Empty;
                    r["RawVehicleNo"] = raw;                              // keep the exact DB value
                    r["VehicleNo"] = FormatPlateForDisplay(NormalizePlate(raw), raw); // pretty display
                }

                dgvVehicles.ItemsSource = dt.DefaultView;
                dgvVehicles.SelectionChanged -= DgvVehicles_SelectionChanged;
                dgvVehicles.SelectionChanged += DgvVehicles_SelectionChanged;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading vehicles: " + ex.Message, "Database Warning",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Wrapper to satisfy XAML event binding (case-sensitive name)
        private void dgvVehicles_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => DgvVehicles_SelectionChanged(sender, e);

        private void DgvVehicles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgvVehicles.SelectedItem is DataRowView row)
            {
                // FIX: read the raw DB value we saved in RawVehicleNo, not the formatted display column
                string rawStored = row.Row.Table.Columns.Contains("RawVehicleNo")
                    ? (row["RawVehicleNo"]?.ToString() ?? string.Empty)
                    : (row["VehicleNo"]?.ToString() ?? string.Empty);

                // selectedVehicleNo = pure alphanumeric, used in WHERE clauses
                selectedVehicleNo = NormalizePlate(rawStored);

                txtVehicleNo.Text = rawStored;   // show the real stored value for editing
                txtModel.Text = row["Model"].ToString();
                txtYear.Text = row["Year"].ToString();

                if (row.Row.Table.Columns.Contains("OwnerID") && row["OwnerID"] != DBNull.Value)
                {
                    cmbOwners.SelectedValue = row["OwnerID"].ToString();
                }
                else
                {
                    string ownerName = row["OwnerName"]?.ToString() ?? string.Empty;
                    foreach (var item in cmbOwners.Items)
                        if (item is DataRowView drv &&
                            (drv["DisplayText"]?.ToString() ?? string.Empty).StartsWith(ownerName))
                        {
                            cmbOwners.SelectedValue = drv["CustomerID"].ToString();
                            break;
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
                MessageBox.Show("Please select a vehicle from the grid first.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (string.IsNullOrWhiteSpace(txtVehicleNo.Text) ||
                string.IsNullOrWhiteSpace(txtModel.Text) ||
                cmbOwners.SelectedValue == null)
            {
                MessageBox.Show("Vehicle number, model and owner are all required.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var conn = DatabaseHelper.GetConnection();

                // Step 1: Get the EXACT plate string as stored in DB using the normalized key
                string? exactOldPlate;
                using (var findOld = new SqliteCommand(
                    "SELECT VehicleNo FROM Vehicles " +
                    "WHERE REPLACE(REPLACE(UPPER(VehicleNo),' ',''),'-','') = @OldNorm LIMIT 1;", conn))
                {
                    findOld.Parameters.AddWithValue("@OldNorm", selectedVehicleNo);
                    exactOldPlate = findOld.ExecuteScalar()?.ToString();
                }

                if (exactOldPlate == null)
                {
                    MessageBox.Show("Vehicle not found. Please re-select and try again.",
                        "Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Step 2: Work out what plate value to save
                // Normalize both the textbox input and the DB value to compare them
                string typedNorm = NormalizePlate(txtVehicleNo.Text);
                bool plateChanged = (typedNorm != selectedVehicleNo);

                // The value we will write to the DB:
                // - If plate did NOT change: reuse exactOldPlate (avoids any reformatting issues)
                // - If plate DID change: validate the new format and use ToStorageFormat()
                string plateToSave;
                if (!plateChanged)
                {
                    // No plate change — keep exactly what is already in the DB
                    plateToSave = exactOldPlate;
                }
                else
                {
                    // Plate is being changed — validate the new value
                    string newStorageFormat = ToStorageFormat(txtVehicleNo.Text);
                    if (!Regex.IsMatch(newStorageFormat, PlateStrictPattern1) &&
                        !Regex.IsMatch(newStorageFormat, PlateStrictPattern2))
                    {
                        MessageBox.Show("Invalid plate format.\n\nValid examples: WP-ABC-1234  or  WP-AB-1234",
                            "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Check the new plate is not already taken by another vehicle
                    using var dupCheck = new SqliteCommand(
                        "SELECT COUNT(*) FROM Vehicles " +
                        "WHERE REPLACE(REPLACE(UPPER(VehicleNo),' ',''),'-','') = @NewNorm;", conn);
                    dupCheck.Parameters.AddWithValue("@NewNorm", typedNorm);
                    if ((long)(dupCheck.ExecuteScalar() ?? 0L) > 0)
                    {
                        MessageBox.Show($"Plate '{newStorageFormat}' is already registered to another vehicle.",
                            "Duplicate Plate", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    plateToSave = newStorageFormat;
                }

                // Step 3: Run in a transaction
                using var tran = conn.BeginTransaction();
                try
                {
                    // If the plate changed, update JobCards FK first so history stays linked
                    if (plateChanged)
                    {
                        using var updateJobs = new SqliteCommand(
                            "UPDATE JobCards SET VehicleNo = @NewNo WHERE VehicleNo = @OldNo;",
                            conn, tran);
                        updateJobs.Parameters.AddWithValue("@NewNo", plateToSave);
                        updateJobs.Parameters.AddWithValue("@OldNo", exactOldPlate);
                        updateJobs.ExecuteNonQuery();
                    }

                    // Update Vehicles using the EXACT old plate — no REPLACE() in WHERE,
                    // so SQLite UNIQUE index is never confused
                    using var updateVehicle = new SqliteCommand(
                        "UPDATE Vehicles SET VehicleNo=@NewNo, Model=@Model, Year=@Year, " +
                        "CustomerID=@CustomerID WHERE VehicleNo = @OldNo;",
                        conn, tran);
                    updateVehicle.Parameters.AddWithValue("@NewNo", plateToSave);
                    updateVehicle.Parameters.AddWithValue("@Model", txtModel.Text.Trim());
                    updateVehicle.Parameters.AddWithValue("@Year",
                        string.IsNullOrEmpty(txtYear.Text) ? DBNull.Value : (object)txtYear.Text.Trim());
                    updateVehicle.Parameters.AddWithValue("@CustomerID", cmbOwners.SelectedValue);
                    updateVehicle.Parameters.AddWithValue("@OldNo", exactOldPlate);
                    int affected = updateVehicle.ExecuteNonQuery();

                    if (affected == 0)
                    {
                        tran.Rollback();
                        MessageBox.Show("No row was changed. Please re-select the vehicle and try again.",
                            "Update Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    tran.Commit();
                }
                catch
                {
                    tran.Rollback();
                    throw;
                }

                MessageBox.Show("Vehicle updated successfully.", "Update",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                LoadVehiclesGrid();
                LoadJobVehiclesGrid();
                selectedVehicleNo = string.Empty;
                btnUpdateVehicle.IsEnabled = false;
                btnDeleteVehicle.IsEnabled = false;
                txtVehicleNo.Clear(); txtModel.Clear(); txtYear.Clear();
                cmbOwners.SelectedIndex = -1;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to update vehicle: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void btnDeleteVehicle_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(selectedVehicleNo))
            {
                MessageBox.Show("Please select a vehicle to delete.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (MessageBox.Show("Delete the selected vehicle record?", "Confirm Delete",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                using var conn = DatabaseHelper.GetConnection();

                // Get the EXACT plate string stored in DB
                string? exactPlate;
                using (var findCmd = new SqliteCommand(
                    "SELECT VehicleNo FROM Vehicles " +
                    "WHERE REPLACE(REPLACE(UPPER(VehicleNo),' ',''),'-','') = @Norm LIMIT 1;", conn))
                {
                    findCmd.Parameters.AddWithValue("@Norm", selectedVehicleNo);
                    exactPlate = findCmd.ExecuteScalar()?.ToString();
                }

                if (exactPlate == null)
                {
                    MessageBox.Show("Vehicle not found. Please re-select and try again.",
                        "Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Check for linked JobCards
                long jobCount;
                using (var countCmd = new SqliteCommand(
                    "SELECT COUNT(*) FROM JobCards WHERE VehicleNo = @Plate;", conn))
                {
                    countCmd.Parameters.AddWithValue("@Plate", exactPlate);
                    jobCount = (long)(countCmd.ExecuteScalar() ?? 0L);
                }

                if (jobCount > 0)
                {
                    var choice = MessageBox.Show(
                        $"This vehicle has {jobCount} job card(s) linked to it.\n\nDeleting the vehicle will also permanently remove all its job history.\n\nDo you want to continue?",
                        "Linked Job Cards Found",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    if (choice != MessageBoxResult.Yes) return;
                }

                // Use a transaction: delete child JobCards first, then the Vehicle
                using var tran = conn.BeginTransaction();
                try
                {
                    if (jobCount > 0)
                    {
                        using var delJobs = new SqliteCommand(
                            "DELETE FROM JobCards WHERE VehicleNo = @Plate;", conn, tran);
                        delJobs.Parameters.AddWithValue("@Plate", exactPlate);
                        delJobs.ExecuteNonQuery();
                    }

                    using var delVehicle = new SqliteCommand(
                        "DELETE FROM Vehicles WHERE VehicleNo = @Plate;", conn, tran);
                    delVehicle.Parameters.AddWithValue("@Plate", exactPlate);
                    int affected = delVehicle.ExecuteNonQuery();

                    if (affected == 0)
                    {
                        tran.Rollback();
                        MessageBox.Show("No row was deleted. Please re-select and try again.",
                            "Delete Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    tran.Commit();
                }
                catch
                {
                    tran.Rollback();
                    throw;
                }

                MessageBox.Show("Vehicle removed successfully.", "Deleted",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                LoadVehiclesGrid();
                LoadJobVehiclesGrid();
                selectedVehicleNo = string.Empty;
                txtVehicleNo.Clear(); txtModel.Clear(); txtYear.Clear();
                cmbOwners.SelectedIndex = -1;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to delete vehicle: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void txtVehicleSearch_TextChanged(object sender, TextChangedEventArgs e)
            => LoadVehiclesGrid(txtVehicleSearch.Text);

        // ==========================================
        // UTILITIES & GLOBAL BUTTONS
        // ==========================================
        private void btnClearForm_Click(object sender, RoutedEventArgs e) => ClearCustomerFields();

        private void btnLogout_Click(object sender, RoutedEventArgs e)
        {
            Window parent = Window.GetWindow(this);
            if (parent != null) { new MainWindow().Show(); parent.Close(); }
        }

        private bool ValidateFormInputs()
        {
            if (string.IsNullOrWhiteSpace(txtCustomerName.Text) || string.IsNullOrWhiteSpace(txtPhone.Text))
            {
                MessageBox.Show("Customer Name and Phone Number are required.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            string phone = txtPhone.Text.Trim();
            if (!phone.All(char.IsDigit) || phone.Length != 10)
            {
                MessageBox.Show("Phone Number must be exactly 10 digits.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        private void ClearCustomerFields()
        {
            txtCustomerName.Clear(); txtPhone.Clear(); txtEmail.Clear();
            selectedCustomerID = null;
            panelEditingIndicator.Visibility = Visibility.Collapsed;
            btnSaveCustomer.IsEnabled = true;
            dgvCustomers.SelectedItem = null;
        }

        // ==========================================
        // TAB 3: JOB CARD & RECEIPT
        // ==========================================
        private void LoadServicesComboBox()
        {
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                using var cmd = new SqliteCommand(
                    "SELECT ServiceID, ServiceName || ' (Rs. ' || BasePrice || ')' AS DisplayText FROM Services;",
                    conn);
                using var reader = cmd.ExecuteReader();
                var dt = new DataTable();
                dt.Load(reader);
                cmbServices.ItemsSource = dt.DefaultView;
                cmbServices.DisplayMemberPath = "DisplayText";
                cmbServices.SelectedValuePath = "ServiceID";
            }
            catch { }
        }

        private void LoadJobVehiclesGrid()
        {
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                using var cmd = new SqliteCommand(
                    "SELECT VehicleNo, Model, Year FROM Vehicles ORDER BY VehicleNo DESC;", conn);
                using var reader = cmd.ExecuteReader();
                var dt = new DataTable();
                dt.Load(reader);
                dt.PrimaryKey = null;
                dt.Constraints.Clear();

                foreach (DataRow r in dt.Rows)
                {
                    string raw = r["VehicleNo"]?.ToString() ?? string.Empty;
                    r["VehicleNo"] = FormatPlateForDisplay(NormalizePlate(raw), raw);
                }

                dgvJobVehiclesSelection.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load job vehicles: " + ex.Message, "Database Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void dgvJobVehiclesSelection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgvJobVehiclesSelection.SelectedItem is not DataRowView selectedRow) return;

            string vehicleNo = selectedRow["VehicleNo"]?.ToString() ?? string.Empty;
            txtJobVehicleNo.Text = vehicleNo;
            if (string.IsNullOrWhiteSpace(vehicleNo)) return;

            rtbHistoryReport.Document.Blocks.Clear();
            var doc = new FlowDocument();
            var para = new Paragraph { FontFamily = new System.Windows.Media.FontFamily("Consolas") };

            try
            {
                using var conn = DatabaseHelper.GetConnection();
                string norm = NormalizePlate(vehicleNo);

                using var cmd = new SqliteCommand(@"
                    SELECT J.JobCardID, S.ServiceName, J.MechanicName, J.DateReceived, J.JobStatus,
                           C.CustomerName, C.Phone
                    FROM JobCards J
                    JOIN Services  S ON J.ServiceID  = S.ServiceID
                    JOIN Vehicles  V ON REPLACE(REPLACE(UPPER(V.VehicleNo),' ',''),'-','') = @Norm
                    JOIN Customers C ON V.CustomerID  = C.CustomerID
                    WHERE REPLACE(REPLACE(UPPER(J.VehicleNo),' ',''),'-','') = @Norm
                    ORDER BY J.JobCardID DESC;", conn);
                cmd.Parameters.AddWithValue("@Norm", norm);

                using var reader = cmd.ExecuteReader();
                bool hasRecords = false;
                int visitIndex = 1;

                while (reader.Read())
                {
                    long jobID = Convert.ToInt64(reader["JobCardID"]);
                    string status = reader["JobStatus"]?.ToString() ?? "Pending";

                    if (!hasRecords)
                    {
                        para.Inlines.Add(new Bold(new Run("AUTOCARE VEHICLE HISTORY PROFILE\n"))
                        { Foreground = System.Windows.Media.Brushes.LightGreen });
                        para.Inlines.Add(new Run("==============================================\n"));
                        para.Inlines.Add(new Run($"Target Plate : {vehicleNo}\n"));
                        para.Inlines.Add(new Run($"Owner Name   : {reader["CustomerName"]}\n"));
                        para.Inlines.Add(new Run($"Contact Phone: {reader["Phone"]}\n"));
                        para.Inlines.Add(new Run("----------------------------------------------\n\n"));
                        hasRecords = true;
                    }

                    para.Inlines.Add(new Bold(new Run($"Visit #{visitIndex} [Ticket ID: #{jobID}]\n")));
                    para.Inlines.Add(new Run($"  Service  : {reader["ServiceName"]}\n"));
                    para.Inlines.Add(new Run($"  Mechanic : {reader["MechanicName"]}\n"));
                    para.Inlines.Add(new Run($"  Schedule : {reader["DateReceived"]}\n"));
                    para.Inlines.Add(new Run("  Job State: "));

                    if (status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
                    {
                        para.Inlines.Add(new Run("Pending ")
                        { Foreground = System.Windows.Media.Brushes.Tomato, FontStyle = FontStyles.Italic });

                        var link = new Run("[MARK AS COMPLETE]")
                        {
                            Foreground = System.Windows.Media.Brushes.DeepSkyBlue,
                            Cursor = System.Windows.Input.Cursors.Hand
                        };
                        link.MouseEnter += (s, _) => System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Hand;
                        link.MouseLeave += (s, _) => System.Windows.Input.Mouse.OverrideCursor = null;
                        long capturedID = jobID;
                        link.MouseDown += (s, _) => ExecuteDirectStatusUpdate(capturedID);
                        para.Inlines.Add(link);
                        para.Inlines.Add(new Run("\n"));
                    }
                    else
                    {
                        para.Inlines.Add(new Run("Complete ✓\n")
                        { Foreground = System.Windows.Media.Brushes.LimeGreen });
                    }

                    para.Inlines.Add(new Run("----------------------------------------------\n"));
                    visitIndex++;
                }

                if (!hasRecords)
                    para.Inlines.Add(new Run(
                        "✨ First-Time Vehicle: No maintenance visits recorded yet.")
                    {
                        Foreground = System.Windows.Media.Brushes.DarkGray,
                        FontStyle = FontStyles.Italic
                    });

                doc.Blocks.Add(para);
                rtbHistoryReport.Document = doc;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load history: " + ex.Message, "System Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteDirectStatusUpdate(long jobCardID)
        {
            if (MessageBox.Show($"Mark Ticket #{jobCardID} as COMPLETED?", "Confirm",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            try
            {
                using var conn = DatabaseHelper.GetConnection();
                using var cmd = new SqliteCommand(
                    "UPDATE JobCards SET JobStatus='Complete' WHERE JobCardID=@ID;", conn);
                cmd.Parameters.AddWithValue("@ID", jobCardID);
                cmd.ExecuteNonQuery();

                MessageBox.Show($"Ticket #{jobCardID} marked as Complete!", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                rtbHistoryReport.Document = new FlowDocument();
                if (dgvJobVehiclesSelection.SelectedItem != null)
                    dgvJobVehiclesSelection_SelectionChanged(dgvJobVehiclesSelection, null!);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to update status: " + ex.Message, "Database Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnCreateJobCard_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtJobVehicleNo.Text))
            { MessageBox.Show("Please select a vehicle from the grid.", "Selection Missing", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (cmbServices.SelectedValue == null)
            { MessageBox.Show("Please select a service.", "Input Error", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (dpBookingDate.SelectedDate == null)
            { MessageBox.Show("Please select a booking date.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (cmbTimeSlots.SelectedItem == null)
            { MessageBox.Show("Please select a time slot.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (dpBookingDate.SelectedDate.Value.Date <= DateTime.Now.Date)
            { MessageBox.Show("Booking date must be a future date.", "Invalid Date", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            string mechanic = string.IsNullOrWhiteSpace(txtMechanicName.Text) ? "Unassigned" : txtMechanicName.Text.Trim();
            string dateStr = dpBookingDate.SelectedDate.Value.ToString("yyyy-MM-dd");
            string timeStr = (cmbTimeSlots.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "TBD";
            string bookingDT = $"{dateStr} [{timeStr}]";
            long newID = 0;

            try
            {
                // Duplicate check
                using (var tmp = DatabaseHelper.GetConnection())
                using (var dup = new SqliteCommand(
                    "SELECT COUNT(*) FROM JobCards WHERE ServiceID=@S AND DateReceived LIKE @D;", tmp))
                {
                    dup.Parameters.AddWithValue("@S", cmbServices.SelectedValue);
                    dup.Parameters.AddWithValue("@D", $"{dateStr}%{timeStr}%");
                    if ((long)(dup.ExecuteScalar() ?? 0L) > 0)
                    {
                        MessageBox.Show("That service/date/time slot is already booked.", "Scheduling Conflict",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                using var conn = DatabaseHelper.GetConnection();

                // Get exact stored plate to satisfy FK constraint
                string normPlate = NormalizePlate(txtJobVehicleNo.Text);
                string? exactPlate;
                using (var find = new SqliteCommand(
                    "SELECT VehicleNo FROM Vehicles WHERE REPLACE(REPLACE(UPPER(VehicleNo),' ',''),'-','')=@N LIMIT 1;",
                    conn))
                {
                    find.Parameters.AddWithValue("@N", normPlate);
                    var v = find.ExecuteScalar();
                    if (v == null || v == DBNull.Value)
                    {
                        MessageBox.Show("Vehicle not found in system.", "Vehicle Not Found",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    exactPlate = v.ToString();
                }

                using (var ins = new SqliteCommand(@"
                    INSERT INTO JobCards (VehicleNo, ServiceID, MechanicName, DateReceived, JobStatus)
                    VALUES (@V,@S,@M,@D,'Pending');
                    SELECT last_insert_rowid();", conn))
                {
                    ins.Parameters.AddWithValue("@V", exactPlate);
                    ins.Parameters.AddWithValue("@S", cmbServices.SelectedValue ?? DBNull.Value);
                    ins.Parameters.AddWithValue("@M", mechanic);
                    ins.Parameters.AddWithValue("@D", bookingDT);
                    newID = (long)(ins.ExecuteScalar() ?? 0);
                }

                // QR code
                string payload =
                    $"AUTOCARE SERVICE TICKET\n====================\n" +
                    $"Ticket ID  : #{newID}\nPlate No   : {txtJobVehicleNo.Text}\n" +
                    $"Service    : {cmbServices.Text}\nMechanic   : {mechanic}\n" +
                    $"APPOINTMENT: {bookingDT}\nStatus     : Pending";

                using var gen = new QRCodeGenerator();
                using var data = gen.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
                using var png = new PngByteQRCode(data);
                byte[] bytes = png.GetGraphic(15);
                using var ms = new MemoryStream(bytes);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
                imgQrCode.Source = bmp;

                txtQrPlaceholder.Visibility = Visibility.Collapsed;
                btnPrintToken.IsEnabled = true;

                MessageBox.Show($"Job Card #{newID} created for {bookingDT}!", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                txtJobVehicleNo.Clear(); txtMechanicName.Clear();
                cmbServices.SelectedIndex = -1; cmbTimeSlots.SelectedIndex = -1;
                dpBookingDate.SelectedDate = null;
                LoadJobVehiclesGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to create job card: " + ex.Message, "Database Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnPrintToken_Click(object sender, RoutedEventArgs e)
            => MessageBox.Show("Sending QR token to print queue...", "Thermal Printer Interface",
                   MessageBoxButton.OK, MessageBoxImage.Information);
    }
}