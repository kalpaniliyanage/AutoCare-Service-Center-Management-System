using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace AutoCare
{
    public static class DatabaseHelper
    {
        private static string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AutoCareDB.db");
        private static string connectionString = $"Data Source={dbPath}";

        public static void InitializeDatabase()
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                // Foreign Keys enable
                var enableFK = connection.CreateCommand();
                enableFK.CommandText = "PRAGMA foreign_keys = ON;";
                enableFK.ExecuteNonQuery();

                // Create tables
                string createTablesQuery = @"
                    CREATE TABLE IF NOT EXISTS SystemLogs (
                        LogID INTEGER PRIMARY KEY AUTOINCREMENT,
                        UserRole TEXT NOT NULL,
                        Action TEXT NOT NULL,
                        Timestamp TEXT DEFAULT (datetime('now','localtime'))
                    );

                    CREATE TABLE IF NOT EXISTS Services (
                        ServiceID INTEGER PRIMARY KEY AUTOINCREMENT,
                        ServiceName TEXT NOT NULL,
                        BasePrice REAL NOT NULL
                    );

                    CREATE TABLE IF NOT EXISTS Customers (
                        CustomerID INTEGER PRIMARY KEY AUTOINCREMENT,
                        CustomerName TEXT NOT NULL,
                        Phone TEXT NOT NULL UNIQUE,
                        Email TEXT
                    );

                    CREATE TABLE IF NOT EXISTS Vehicles (
                        VehicleNo TEXT PRIMARY KEY,
                        Model TEXT NOT NULL,
                        CustomerID INTEGER,
                        FOREIGN KEY (CustomerID) REFERENCES Customers(CustomerID) ON DELETE CASCADE
                    );

                    CREATE TABLE IF NOT EXISTS JobCards (
                        JobCardID INTEGER PRIMARY KEY AUTOINCREMENT,
                        VehicleNo TEXT,
                        ServiceID INTEGER NOT NULL,
                        MechanicName TEXT,
                        DateReceived TEXT NOT NULL,
                        JobStatus TEXT DEFAULT 'Pending',
                        FOREIGN KEY (VehicleNo) REFERENCES Vehicles(VehicleNo) ON DELETE SET NULL,
                        FOREIGN KEY (ServiceID) REFERENCES Services(ServiceID)
                    );

                    CREATE TABLE IF NOT EXISTS Inventory (
                        ItemID INTEGER PRIMARY KEY AUTOINCREMENT,
                        ItemName TEXT NOT NULL,
                        Quantity INTEGER NOT NULL,
                        MinStockLevel INTEGER DEFAULT 5,
                        UnitPrice REAL NOT NULL
                    );

                    CREATE TABLE IF NOT EXISTS JobItems (
                        JobItemID INTEGER PRIMARY KEY AUTOINCREMENT,
                        JobCardID INTEGER NOT NULL,
                        ItemID INTEGER NOT NULL,
                        QtyUsed INTEGER NOT NULL,
                        FOREIGN KEY (JobCardID) REFERENCES JobCards(JobCardID),
                        FOREIGN KEY (ItemID) REFERENCES Inventory(ItemID)
                    );

                    CREATE TABLE IF NOT EXISTS Invoices (
                        InvoiceID INTEGER PRIMARY KEY AUTOINCREMENT,
                        JobCardID INTEGER NOT NULL,
                        ServiceCost REAL NOT NULL,
                        MaterialCost REAL NOT NULL,
                        TotalAmount REAL NOT NULL,
                        PaymentStatus TEXT DEFAULT 'Unpaid',
                        PaymentDate TEXT,
                        FOREIGN KEY (JobCardID) REFERENCES JobCards(JobCardID)
                    );
                ";

                using (var command = new SqliteCommand(createTablesQuery, connection))
                {
                    command.ExecuteNonQuery();
                }

                // Add Year column to Vehicles if missing
                try
                {
                    using (var checkCmd = new SqliteCommand("PRAGMA table_info(Vehicles);", connection))
                    using (var reader = checkCmd.ExecuteReader())
                    {
                        bool hasYear = false;
                        while (reader.Read())
                        {
                            if (reader["name"].ToString() == "Year")
                                hasYear = true;
                        }
                        reader.Close();

                        if (!hasYear)
                        {
                            using (var alterCmd = new SqliteCommand("ALTER TABLE Vehicles ADD COLUMN Year TEXT;", connection))
                            {
                                alterCmd.ExecuteNonQuery();
                            }
                        }
                    }
                }
                catch { /* ignore */ }

                // ── FIX: Wipe ALL triggers then recreate correct ones at every startup.
                // This prevents any stale/malformed trigger (e.g. NEW.CorrectColumnName)
                // from blocking INSERT/UPDATE on JobCards or any other table.
                NukeAllTriggers(connection);
                EnsureDatabaseTriggers(connection);

                // Insert sample data only if DB is empty
                InsertFakeDataIfEmpty(connection);
            }
        }

        public static SqliteConnection GetConnection()
        {
            var conn = new SqliteConnection(connectionString);
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA foreign_keys = ON;";
            cmd.ExecuteNonQuery();
            return conn;
        }

        // ── Drops EVERY trigger unconditionally so no malformed trigger survives
        // across app restarts. Called at startup before EnsureDatabaseTriggers().
        private static void NukeAllTriggers(SqliteConnection connection)
        {
            try
            {
                var listCmd = connection.CreateCommand();
                listCmd.CommandText = "SELECT name FROM sqlite_master WHERE type = 'trigger';";
                using var reader = listCmd.ExecuteReader();

                var triggers = new System.Collections.Generic.List<string>();
                while (reader.Read())
                    triggers.Add(reader.GetString(0));

                reader.Close();

                foreach (var triggerName in triggers)
                {
                    try
                    {
                        var dropCmd = connection.CreateCommand();
                        dropCmd.CommandText = $"DROP TRIGGER IF EXISTS \"{triggerName}\";";
                        dropCmd.ExecuteNonQuery();
                    }
                    catch { /* ignore per-trigger drop errors */ }
                }
            }
            catch { /* ignore — app still starts even if nuke fails */ }
        }

        // ── Creates all correct triggers using verified column names.
        // Only called after NukeAllTriggers() so no duplicates can exist.
        private static void EnsureDatabaseTriggers(SqliteConnection connection)
        {
            string[] cmds = new[]
            {
                // ── Customers ──────────────────────────────────────────────
                @"CREATE TRIGGER IF NOT EXISTS trg_customers_insert
                  AFTER INSERT ON Customers BEGIN
                      INSERT INTO SystemLogs (UserRole, Action, Timestamp)
                      VALUES ('Receptionist','Added Customer: '||NEW.CustomerName,datetime('now','localtime'));
                  END;",

                @"CREATE TRIGGER IF NOT EXISTS trg_customers_update
                  AFTER UPDATE ON Customers BEGIN
                      INSERT INTO SystemLogs (UserRole, Action, Timestamp)
                      VALUES ('Receptionist','Updated Customer: '||NEW.CustomerName,datetime('now','localtime'));
                  END;",

                @"CREATE TRIGGER IF NOT EXISTS trg_customers_delete
                  AFTER DELETE ON Customers BEGIN
                      INSERT INTO SystemLogs (UserRole, Action, Timestamp)
                      VALUES ('Receptionist','Deleted Customer: '||OLD.CustomerName,datetime('now','localtime'));
                  END;",

                // ── Vehicles ───────────────────────────────────────────────
                @"CREATE TRIGGER IF NOT EXISTS trg_vehicles_insert
                  AFTER INSERT ON Vehicles BEGIN
                      INSERT INTO SystemLogs (UserRole, Action, Timestamp)
                      VALUES ('Receptionist','Added Vehicle: '||NEW.VehicleNo,datetime('now','localtime'));
                  END;",

                @"CREATE TRIGGER IF NOT EXISTS trg_vehicles_update
                  AFTER UPDATE ON Vehicles BEGIN
                      INSERT INTO SystemLogs (UserRole, Action, Timestamp)
                      VALUES ('Receptionist','Updated Vehicle: '||NEW.VehicleNo,datetime('now','localtime'));
                  END;",

                @"CREATE TRIGGER IF NOT EXISTS trg_vehicles_delete
                  AFTER DELETE ON Vehicles BEGIN
                      INSERT INTO SystemLogs (UserRole, Action, Timestamp)
                      VALUES ('Receptionist','Deleted Vehicle: '||OLD.VehicleNo,datetime('now','localtime'));
                  END;",

                // ── JobCards ───────────────────────────────────────────────
                @"CREATE TRIGGER IF NOT EXISTS trg_jobcards_insert
                  AFTER INSERT ON JobCards BEGIN
                      INSERT INTO SystemLogs (UserRole, Action, Timestamp)
                      VALUES ('Receptionist','Created Job Card #'||NEW.JobCardID,datetime('now','localtime'));
                  END;",

                @"CREATE TRIGGER IF NOT EXISTS trg_jobcards_update
                  AFTER UPDATE ON JobCards BEGIN
                      INSERT INTO SystemLogs (UserRole, Action, Timestamp)
                      VALUES ('Service Manager','Updated Job Card #'||NEW.JobCardID||' - Status: '||NEW.JobStatus,datetime('now','localtime'));
                  END;",

                @"CREATE TRIGGER IF NOT EXISTS trg_jobcards_delete
                  AFTER DELETE ON JobCards BEGIN
                      INSERT INTO SystemLogs (UserRole, Action, Timestamp)
                      VALUES ('Service Manager','Deleted Job Card #'||OLD.JobCardID,datetime('now','localtime'));
                  END;",

                // ── Invoices ───────────────────────────────────────────────
                @"CREATE TRIGGER IF NOT EXISTS trg_invoices_insert
                  AFTER INSERT ON Invoices BEGIN
                      INSERT INTO SystemLogs (UserRole, Action, Timestamp)
                      VALUES ('Cashier','Generated Invoice #'||NEW.InvoiceID,datetime('now','localtime'));
                  END;",

                @"CREATE TRIGGER IF NOT EXISTS trg_invoices_update
                  AFTER UPDATE ON Invoices BEGIN
                      INSERT INTO SystemLogs (UserRole, Action, Timestamp)
                      VALUES ('Cashier','Updated Invoice #'||NEW.InvoiceID,datetime('now','localtime'));
                  END;",

                @"CREATE TRIGGER IF NOT EXISTS trg_invoices_delete
                  AFTER DELETE ON Invoices BEGIN
                      INSERT INTO SystemLogs (UserRole, Action, Timestamp)
                      VALUES ('Cashier','Deleted Invoice #'||OLD.InvoiceID,datetime('now','localtime'));
                  END;",

                // ── Inventory ──────────────────────────────────────────────
                @"CREATE TRIGGER IF NOT EXISTS trg_inventory_insert
                  AFTER INSERT ON Inventory BEGIN
                      INSERT INTO SystemLogs (UserRole, Action, Timestamp)
                      VALUES ('Service Manager','Added Inventory Item: '||NEW.ItemName,datetime('now','localtime'));
                  END;",

                @"CREATE TRIGGER IF NOT EXISTS trg_inventory_update
                  AFTER UPDATE ON Inventory BEGIN
                      INSERT INTO SystemLogs (UserRole, Action, Timestamp)
                      VALUES ('Service Manager','Updated Inventory Item: '||NEW.ItemName,datetime('now','localtime'));
                  END;",

                @"CREATE TRIGGER IF NOT EXISTS trg_inventory_delete
                  AFTER DELETE ON Inventory BEGIN
                      INSERT INTO SystemLogs (UserRole, Action, Timestamp)
                      VALUES ('Service Manager','Deleted Inventory Item: '||OLD.ItemName,datetime('now','localtime'));
                  END;"
            };

            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    foreach (var sql in cmds)
                    {
                        try
                        {
                            using var cmd = connection.CreateCommand();
                            cmd.Transaction = transaction;
                            cmd.CommandText = sql;
                            cmd.ExecuteNonQuery();
                        }
                        catch { /* ignore individual trigger errors */ }
                    }
                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                }
            }
        }

        private static void InsertFakeDataIfEmpty(SqliteConnection connection)
        {
            string checkQuery = "SELECT COUNT(*) FROM Customers;";
            using (var cmd = new SqliteCommand(checkQuery, connection))
            {
                long count = (long)cmd.ExecuteScalar();
                if (count > 0) return;
            }

            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    // 1. Services
                    string queryServices = @"
                        INSERT INTO Services (ServiceName, BasePrice) VALUES 
                        ('Full Service', 9500.00),
                        ('Body Wash & Vacuum', 2500.00),
                        ('Engine Oil Change', 1500.00),
                        ('Wheel Alignment', 3500.00),
                        ('Brake System Service', 4500.00),
                        ('AC Gas Charging', 6000.00);";
                    ExecuteNonQuery(queryServices, connection, transaction);

                    // 2. Inventory
                    string queryInventory = @"
                        INSERT INTO Inventory (ItemName, Quantity, MinStockLevel, UnitPrice) VALUES 
                        ('Toyota Oil Filter', 25, 5, 1850.00),
                        ('Suzuki Air Filter', 30, 5, 2200.00),
                        ('Mobil 1 Engine Oil 4L', 3, 5, 16500.00),
                        ('Castrol Edge 4L', 15, 5, 14800.00),
                        ('Front Brake Pads (Vitz)', 2, 4, 8500.00),
                        ('Rear Brake Shoe (Alto)', 12, 4, 5200.00),
                        ('Denso Spark Plug', 100, 10, 950.00),
                        ('Wiper Blade 16 Inch', 4, 5, 1200.00),
                        ('Wiper Blade 24 Inch', 18, 5, 1600.00),
                        ('Coolant 1L', 40, 8, 1450.00),
                        ('Dot4 Brake Fluid', 15, 5, 980.00),
                        ('Microfiber Cloth', 50, 10, 350.00);";
                    ExecuteNonQuery(queryInventory, connection, transaction);

                    // 3. Customers
                    string queryCustomers = @"
                        INSERT INTO Customers (CustomerName, Phone, Email) VALUES 
                        ('Kamal Perera', '0771234561', 'kamal@gmail.com'), ('Nimal Silva', '0771234562', 'nimal@gmail.com'),
                        ('Sunil Jayasinghe', '0711234563', 'sunil@yahoo.com'), ('Anura Fernando', '0761234564', 'anura@gmail.com'),
                        ('Ruwan Kumari', '0721234565', 'ruwan@outlook.com'), ('Priyantha Bandara', '0751234566', 'priyantha@gmail.com'),
                        ('Chathura Dias', '0781234567', 'chathura@gmail.com'), ('Kasun Rajapaksha', '0701234568', 'kasun@gmail.com'),
                        ('Nadeesha Hemamali', '0779876541', 'nadeesha@gmail.com'), ('Roshan Mahanama', '0719876542', 'roshan@gmail.com'),
                        ('Mahela Jayawardene', '0775551111', 'mahela@cricket.lk'), ('Kumar Sangakkara', '0775552222', 'kumar@cricket.lk'),
                        ('Sanath Jayasuriya', '0775553333', 'sanath@yahoo.com'), ('Lasith Malinga', '0765554444', 'malinga@gmail.com'),
                        ('Angelo Mathews', '0715555555', 'angelo@gmail.com'), ('Dinesh Chandimal', '0725556666', 'dinesh@gmail.com'),
                        ('Kusal Perera', '0755557777', 'kusal@gmail.com'), ('Dimuth Karunaratne', '0785558888', 'dimuth@gmail.com'),
                        ('Wanindu Hasaranga', '0705559999', 'wanindu@gmail.com'), ('Pathum Nissanka', '0776661111', 'pathum@gmail.com'),
                        ('Charith Asalanka', '0716662222', 'charith@gmail.com'), ('Dasun Shanaka', '0766663333', 'dasun@gmail.com'),
                        ('Dushmantha Chameera', '0726664444', 'chameera@gmail.com'), ('Lahiru Kumara', '0756665444', 'lahiru@gmail.com'),
                        ('Maheesh Theekshana', '0786666666', 'maheesh@gmail.com'), ('Pramod Madushan', '0706667777', 'pramod@gmail.com'),
                        ('Asitha Fernando', '0777771111', 'asitha@gmail.com'), ('Dunith Wellalage', '0717772222', 'dunith@gmail.com'),
                        ('Sadeera Samarawickrama', '0767773333', 'sadeera@gmail.com'), ('Dilshan Madushanka', '0727774444', 'dilshan@gmail.com'),
                        ('Matheesha Pathirana', '0757775555', 'matheesha@gmail.com'), ('Kasun Rajitha', '0787776666', 'rajitha@gmail.com'),
                        ('Vishwa Fernando', '0707777777', 'vishwa@gmail.com'), ('Avishka Fernando', '0778881111', 'avishka@gmail.com'),
                        ('Bhanuka Rajapaksa', '0718882222', 'bhanuka@gmail.com'), ('Nuwan Thushara', '0768883333', 'nuwan@gmail.com'),
                        ('Akila Dananjaya', '0728884444', 'akila@gmail.com'), ('Jeffrey Vandersay', '0758885555', 'jeffrey@gmail.com'),
                        ('Kamindu Mendis', '0788886666', 'kamindu@gmail.com'), ('Ashen Bandara', '0708887777', 'ashen@gmail.com'),
                        ('Danushka Gunathilaka', '0779991111', 'danushka@gmail.com'), ('Kusal Mendis', '0719992222', 'kusalm@gmail.com'),
                        ('Niroshan Dickwella', '0769993333', 'dickwella@gmail.com'), ('Oshada Fernando', '0729994444', 'oshada@gmail.com'),
                        ('Lakshan Sandakan', '0759995555', 'sandakan@gmail.com'), ('Ramesh Mendis', '0789996666', 'ramesh@gmail.com'),
                        ('Praveen Jayawickrama', '0709997777', 'praveen@gmail.com'), ('Minod Bhanuka', '0772223333', 'minod@gmail.com'),
                        ('Chamika Karunaratne', '0712224444', 'chamika@gmail.com'), ('Binura Fernando', '0762225555', 'binura@gmail.com');";
                    ExecuteNonQuery(queryCustomers, connection, transaction);

                    // 4. Vehicles
                    string queryVehicles = @"
                        INSERT INTO Vehicles (VehicleNo, Model, CustomerID) VALUES 
                        ('WP CB-1001', 'Toyota Vitz', 1), ('WP CAD-2002', 'Suzuki Alto', 2),
                        ('WP KX-3003', 'Honda Civic', 3), ('WP PH-4004', 'Toyota Prius', 4),
                        ('EP CAO-5005', 'Nissan Dayz', 5), ('WP CBM-6006', 'Toyota Aqua', 6),
                        ('SP CAI-7007', 'Suzuki WagonR', 7), ('WP CBB-8008', 'Honda Fit', 8),
                        ('WP CAS-9009', 'Toyota Raize', 9), ('WP KY-1111', 'Mitsubishi Mirage', 10),
                        ('WP CCO-1234', 'Toyota Axio', 11), ('WP CAR-5678', 'Honda Grace', 12),
                        ('WP CBA-9101', 'Suzuki Spacia', 13), ('WP CAB-1121', 'Toyota CHR', 14),
                        ('WP CBD-3141', 'Nissan Leaf', 15), ('SP CAD-5161', 'Suzuki Hustler', 16),
                        ('WP CBE-7181', 'Toyota Corolla', 17), ('WP CBF-9201', 'Honda Shuttle', 18),
                        ('NW CBG-1212', 'Mazda 3', 19), ('WP CBH-3434', 'Toyota Allion', 20),
                        ('WP CBI-5656', 'Suzuki Every', 21), ('WP CBJ-7878', 'Toyota Hilux', 22),
                        ('CP CBK-9090', 'Nissan X-Trail', 23), ('WP CBL-1313', 'Honda Vezel', 24),
                        ('WP CBM-2424', 'Toyota Land Cruiser', 25), ('WP CBN-5757', 'Mitsubishi Montero', 26),
                        ('WP CBO-6868', 'Suzuki Vitara', 27), ('WP CBP-7979', 'Hyundai Tucson', 28),
                        ('SP CBQ-8080', 'Kia Sportage', 29), ('WP CBR-9191', 'Toyota Yaris', 30),
                        ('WP CBS-1111', 'Honda CR-V', 31), ('WP CBT-2222', 'Nissan Navara', 32),
                        ('WP CBU-3333', 'Toyota Camry', 33), ('WP CBV-4444', 'Audi A4', 34),
                        ('WP CBW-5555', 'BMW 320i', 35), ('WP CBX-6666', 'Mercedes C200', 36),
                        ('WP CBY-7777', 'Toyota Premio', 37), ('WP CBZ-8888', 'Suzuki Celery', 38),
                        ('WP CAA-1222', 'Daihatsu Tanto', 39), ('WP CAB-2333', 'Toyota Rush', 40),
                        ('WP CAC-3444', 'Honda BR-V', 41), ('WP CAD-4555', 'Nissan Sunny', 42),
                        ('WP CAE-5666', 'Toyota Tank', 43), ('WP CAF-6777', 'Suzuki Baleno', 44),
                        ('WP CAG-7888', 'Toyota Pasos', 45), ('WP CAH-8999', 'Honda Accord', 46),
                        ('WP CAI-9001', 'Hyundai I10', 47), ('WP CAJ-9002', 'Kia Picanto', 48),
                        ('WP CAK-9003', 'Tata Nano', 49), ('WP CAL-9004', 'Mahindra KUV100', 50);";
                    ExecuteNonQuery(queryVehicles, connection, transaction);

                    // 5. JobCards
                    string queryJobCards = @"
                        INSERT INTO JobCards (VehicleNo, ServiceID, MechanicName, DateReceived, JobStatus) VALUES 
                        ('WP CB-1001', 1, 'Sunil', '2026-05-20 08:30:00', 'Completed'),
                        ('WP CAD-2002', 2, 'Nimal', '2026-05-20 09:15:00', 'Completed'),
                        ('WP KX-3003', 3, 'Jagath', '2026-05-21 10:00:00', 'Completed'),
                        ('WP PH-4004', 4, 'Saman', '2026-05-21 11:30:00', 'Completed'),
                        ('EP CAO-5005', 5, 'Sunil', '2026-05-22 14:00:00', 'Completed'),
                        ('WP CBM-6006', 6, 'Kamal', '2026-05-22 15:45:00', 'Completed'),
                        ('SP CAI-7007', 1, 'Nimal', '2026-05-23 08:00:00', 'Completed'),
                        ('WP CBB-8008', 2, 'Jagath', '2026-05-23 09:30:00', 'Completed'),
                        ('WP CAS-9009', 3, 'Saman', '2026-05-24 10:45:00', 'Completed'),
                        ('WP KY-1111', 4, 'Kamal', '2026-05-24 13:15:00', 'Completed'),
                        ('WP CCO-1234', 5, 'Sunil', '2026-05-25 08:30:00', 'Completed'),
                        ('WP CAR-5678', 6, 'Nimal', '2026-05-25 11:00:00', 'Completed'),
                        ('WP CBA-9101', 1, 'Jagath', '2026-05-26 13:00:00', 'Completed'),
                        ('WP CAB-1121', 2, 'Saman', '2026-05-26 14:30:00', 'Completed'),
                        ('WP CBD-3141', 3, 'Kamal', '2026-05-27 09:00:00', 'Completed'),
                        ('SP CAD-5161', 4, 'Sunil', '2026-05-27 10:15:00', 'Completed'),
                        ('WP CBE-7181', 5, 'Nimal', '2026-05-28 11:30:00', 'Completed'),
                        ('WP CBF-9201', 6, 'Jagath', '2026-05-28 15:00:00', 'Completed'),
                        ('NW CBG-1212', 1, 'Saman', '2026-05-29 08:15:00', 'Completed'),
                        ('WP CBH-3434', 2, 'Kamal', '2026-05-29 10:30:00', 'Completed'),
                        ('WP CBI-5656', 3, 'Sunil', '2026-05-30 09:00:00', 'Completed'),
                        ('WP CBJ-7878', 4, 'Nimal', '2026-05-30 11:45:00', 'Completed'),
                        ('CP CBK-9090', 5, 'Jagath', '2026-05-31 13:15:00', 'Completed'),
                        ('WP CBL-1313', 6, 'Saman', '2026-05-31 16:00:00', 'Completed'),
                        ('WP CBM-2424', 1, 'Kamal', '2026-06-01 08:30:00', 'Completed'),
                        ('WP CBN-5757', 2, 'Sunil', '2026-06-01 10:00:00', 'Completed'),
                        ('WP CBO-6868', 3, 'Nimal', '2026-06-01 11:15:00', 'Completed'),
                        ('WP CBP-7979', 4, 'Jagath', '2026-06-02 08:45:00', 'Completed'),
                        ('SP CBQ-8080', 5, 'Saman', '2026-06-02 11:00:00', 'Completed'),
                        ('WP CBR-9191', 6, 'Kamal', '2026-06-02 14:20:00', 'Completed'),
                        ('WP CBS-1111', 1, 'Sunil', '2026-06-03 08:15:00', 'Completed'),
                        ('WP CBT-2222', 2, 'Nimal', '2026-06-03 09:40:00', 'Completed'),
                        ('WP CBU-3333', 3, 'Jagath', '2026-06-03 11:10:00', 'Completed'),
                        ('WP CBV-4444', 4, 'Saman', '2026-06-03 13:30:00', 'Completed'),
                        ('WP CBW-5555', 5, 'Kamal', '2026-06-03 15:00:00', 'Completed'),
                        ('WP CBX-6666', 1, 'Sunil', '2026-06-04 08:00:00', 'Completed'),
                        ('WP CBY-7777', 2, 'Nimal', '2026-06-04 08:30:00', 'Completed'),
                        ('WP CBZ-8888', 3, 'Jagath', '2026-06-04 09:00:00', 'Completed'),
                        ('WP CAA-1222', 4, 'Saman', '2026-06-04 09:45:00', 'Ongoing'),
                        ('WP CAB-2333', 5, 'Kamal', '2026-06-04 10:15:00', 'Ongoing'),
                        ('WP CAC-3444', 6, 'Sunil', '2026-06-04 11:00:00', 'Ongoing'),
                        ('WP CAD-4555', 1, 'Nimal', '2026-06-04 13:00:00', 'Pending'),
                        ('WP CAE-5666', 2, 'Jagath', '2026-06-04 14:15:00', 'Pending'),
                        ('WP CAF-6777', 3, null, '2026-06-04 15:00:00', 'Pending'),
                        ('WP CAG-7888', 4, null, '2026-06-04 15:30:00', 'Pending'),
                        ('WP CAH-8999', 5, null, '2026-06-04 16:00:00', 'Pending'),
                        ('WP CAI-9001', 6, null, '2026-06-04 16:15:00', 'Pending'),
                        ('WP CAJ-9002', 1, null, '2026-06-04 16:30:00', 'Pending'),
                        ('WP CAK-9003', 2, null, '2026-06-04 16:45:00', 'Pending'),
                        ('WP CAL-9004', 3, null, '2026-06-04 17:00:00', 'Pending');";
                    ExecuteNonQuery(queryJobCards, connection, transaction);

                    // 6. JobItems
                    string queryJobItems = @"
                        INSERT INTO JobItems (JobCardID, ItemID, QtyUsed) VALUES 
                        (1, 1, 1), (1, 3, 1), (3, 3, 1), (3, 7, 4),
                        (5, 5, 1), (7, 1, 1), (7, 4, 1), (9, 4, 1),
                        (11, 11, 1), (13, 2, 1), (13, 3, 1), (15, 4, 1),
                        (17, 5, 1), (19, 1, 1), (19, 4, 1), (21, 4, 1),
                        (23, 6, 1), (25, 2, 1), (25, 3, 1), (27, 4, 1),
                        (29, 5, 1), (31, 1, 1), (31, 10, 2), (33, 4, 1),
                        (35, 11, 2), (36, 1, 1), (36, 3, 1), (38, 4, 1);";
                    ExecuteNonQuery(queryJobItems, connection, transaction);

                    // 7. Invoices
                    string queryInvoices = @"
                        INSERT INTO Invoices (JobCardID, ServiceCost, MaterialCost, TotalAmount, PaymentStatus, PaymentDate) VALUES 
                        (1, 9500.00, 18350.00, 27850.00, 'Paid', '2026-05-20 12:00:00'),
                        (2, 2500.00, 0.00, 2500.00, 'Paid', '2026-05-20 14:30:00'),
                        (3, 1500.00, 20300.00, 21800.00, 'Paid', '2026-05-21 16:00:00'),
                        (4, 3500.00, 0.00, 3500.00, 'Paid', '2026-05-21 17:15:00'),
                        (5, 4500.00, 8500.00, 13000.00, 'Paid', '2026-05-22 17:30:00'),
                        (6, 6000.00, 0.00, 6000.00, 'Paid', '2026-05-22 18:00:00'),
                        (7, 9500.00, 16650.00, 26150.00, 'Paid', '2026-05-23 11:30:00'),
                        (8, 2500.00, 0.00, 2500.00, 'Paid', '2026-05-23 13:00:00'),
                        (9, 1500.00, 14800.00, 16300.00, 'Paid', '2026-05-24 15:45:00'),
                        (10, 3500.00, 0.00, 3500.00, 'Paid', '2026-05-24 16:20:00'),
                        (11, 4500.00, 980.00, 5480.00, 'Paid', '2026-05-25 12:00:00'),
                        (12, 6000.00, 0.00, 6000.00, 'Paid', '2026-05-25 14:15:00'),
                        (13, 9500.00, 18700.00, 28200.00, 'Paid', '2026-05-26 17:00:00'),
                        (14, 2500.00, 0.00, 2500.00, 'Paid', '2026-05-26 17:45:00'),
                        (15, 1500.00, 14800.00, 16300.00, 'Paid', '2026-05-27 13:00:00'),
                        (16, 3500.00, 0.00, 3500.00, 'Paid', '2026-05-27 14:30:00'),
                        (17, 4500.00, 8500.00, 13000.00, 'Paid', '2026-05-28 16:15:00'),
                        (18, 6000.00, 0.00, 6000.00, 'Paid', '2026-05-28 17:00:00'),
                        (19, 9500.00, 16650.00, 26150.00, 'Paid', '2026-05-29 12:30:00'),
                        (20, 2500.00, 0.00, 2500.00, 'Paid', '2026-05-29 14:00:00'),
                        (21, 1500.00, 14800.00, 16300.00, 'Paid', '2026-05-30 13:00:00'),
                        (22, 3500.00, 0.00, 3500.00, 'Paid', '2026-05-30 15:30:00'),
                        (23, 4500.00, 5200.00, 9700.00, 'Paid', '2026-05-31 16:15:00'),
                        (24, 6000.00, 0.00, 6000.00, 'Paid', '2026-05-31 17:45:00'),
                        (25, 9500.00, 18700.00, 28200.00, 'Paid', '2026-06-01 13:00:00'),
                        (26, 2500.00, 0.00, 2500.00, 'Paid', '2026-06-01 14:20:00'),
                        (27, 1500.00, 14800.00, 16300.00, 'Paid', '2026-06-01 16:10:00'),
                        (28, 3500.00, 0.00, 3500.00, 'Paid', '2026-06-02 11:30:00'),
                        (29, 4500.00, 8500.00, 13000.00, 'Paid', '2026-06-02 14:00:00'),
                        (30, 6000.00, 0.00, 6000.00, 'Paid', '2026-06-02 16:45:00'),
                        (31, 9500.00, 4750.00, 14250.00, 'Paid', '2026-06-03 12:00:00'),
                        (32, 2500.00, 0.00, 2500.00, 'Paid', '2026-06-03 13:15:00'),
                        (33, 1500.00, 14800.00, 16300.00, 'Paid', '2026-06-03 14:40:00'),
                        (34, 3500.00, 0.00, 3500.00, 'Paid', '2026-06-03 16:00:00'),
                        (35, 4500.00, 1960.00, 6460.00, 'Paid', '2026-06-03 17:10:00'),
                        (36, 9500.00, 18350.00, 27850.00, 'Paid', '2026-06-04 11:00:00'),
                        (37, 2500.00, 0.00, 2500.00, 'Paid', '2026-06-04 11:45:00'),
                        (38, 1500.00, 14800.00, 16300.00, 'Paid', '2026-06-04 13:20:00');";
                    ExecuteNonQuery(queryInvoices, connection, transaction);

                    // 8. System Logs
                    string queryLogs = @"
                        INSERT INTO SystemLogs (UserRole, Action, Timestamp) VALUES 
                        ('Admin', 'Database Initialized with Sample Data', '2026-05-20 08:00:00'),
                        ('Receptionist', 'Logged In', '2026-06-04 08:00:00'),
                        ('Receptionist', 'Created JobCard for WP CBX-6666', '2026-06-04 08:05:00'),
                        ('Service Manager', 'Logged In', '2026-06-04 08:10:00'),
                        ('Service Manager', 'Assigned Mechanic Sunil to JobCard #36', '2026-06-04 08:12:00'),
                        ('Service Manager', 'Updated Status to Completed for JobCard #36', '2026-06-04 10:30:00'),
                        ('Cashier', 'Logged In', '2026-06-04 10:45:00'),
                        ('Cashier', 'Generated Invoice for JobCard #36', '2026-06-04 11:00:00'),
                        ('Cashier', 'Payment Received for Invoice #36', '2026-06-04 11:02:00');";
                    ExecuteNonQuery(queryLogs, connection, transaction);

                    transaction.Commit();
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        private static void ExecuteNonQuery(string query, SqliteConnection connection, SqliteTransaction transaction)
        {
            using (var command = new SqliteCommand(query, connection, transaction))
            {
                command.ExecuteNonQuery();
            }
        }
    }
}