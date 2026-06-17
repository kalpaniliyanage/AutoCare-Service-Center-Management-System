# AutoCare-Service-Center-Management-System
VAP group project 

# AutoCare Premier Service Suite

AutoCare Premier Service Suite is a professional, standalone desktop application designed to streamline automotive service center management. Built using C# and the .NET Windows Forms framework, the application incorporates role-based access, automated workflows, multi-format reporting, and modern interactive dashboards.

## 🚀 System Features

* **Role-Based Access Control:** Distinct workflows for Admin, Receptionist, Service Manager, and Cashier accessed via secure passcodes.
* **Audit Trail Logs:** Complete logging of system events, logins, and crucial actions.
* **QR-Code Workflow Ecosystem:** Instant job sheet Generation and touchless checkout billing using dynamically rendered QR codes.
* **Inventory & Low Stock Analytics:** Live tracking of automotive items with automated visual warnings for dropping stock levels.
* **Comprehensive Financial Processing:** Auto-calculated service/material invoices alongside configurable financial report builders.
* **Robust Data Architecture:** Seamless SQLite transactional engine with on-demand physical backup and recovery utilities.

---

## 🛠️ Project Architecture & Database Schema

The system utilizes an unshared, embedded **SQLite** instance handling localized business logic relations. Relations are constructed under standard database notation architectures as follows:

* **`SystemLogs`** (`LogID [PK]`, `UserRole`, `Action`, `Timestamp`)
* **`Services`** (`ServiceID [PK]`, `ServiceName`, `BasePrice`)
* **`Customers`** (`CustomerID [PK]`, `CustomerName`, `Phone [Unique]`, `Email`)
* **`Vehicles`** (`VehicleNo [PK]`, `Model`, `CustomerID [FK]`)
* **`JobCards`** (`JobCardID [PK]`, `VehicleNo [FK]`, `ServiceID [FK]`, `MechanicName`, `DateReceived`, `JobStatus`)
* **`Inventory`** (`ItemID [PK]`, `ItemName`, `Quantity`, `MinStockLevel`, `UnitPrice`)
* **`JobItems`** (`JobItemID [PK]`, `JobCardID [FK]`, `ItemID [FK]`, `QtyUsed`)
* **`Invoices`** (`InvoiceID [PK]`, `JobCardID [FK]`, `ServiceCost`, `MaterialCost`, `TotalAmount`, `PaymentStatus`, `PaymentDate`)

---

## 👥 Team Contributions & Module Breakdown

This project was built through balanced, collaborative modular version tracking across 4 team members. Contributions are auditable via the Git commit timeline history.

### 👑 Member 1: System Administrator & Core DevOps
* **Modules Built:** System Administration Control Center, Shared Database Kernel Engine (`DatabaseHelper.cs`), Seed Mock Engine (50 automated mock-records).
* **Key Features:** Automated physical database backup & restore routines, live global operational logs (Audit Trail View Grid), system performance analytics chart configurations.
* **Database Mapping Focus:** `SystemLogs`, `Services`.
* **Default Passcode:** `0000`

### 📋 Member 2: Front Desk Operations & Intake Registry
* **Modules Built:** Receptionist Control Terminal, Customer Relationship Center, Active Garage Job Placement Workspace.
* **Key Features:** Customer & Vehicle Profile CRUD interactions, Multi-Criteria History Match filtering, **Innovative QR Module Part 1** (Dynamic QR generation and physical print rendering for unique job tickets).
* **Database Mapping Focus:** `Customers`, `Vehicles`, `JobCards`.
* **Default Passcode:** `0001`

### 🔧 Member 3: Technical Operations & Inventory Supply Management
* **Modules Built:** Workshop Supervisor Workspace, Automotive Parts Inventory Warehouse, Data Migration Portals.
* **Key Features:** Interactive Job allocation & Status Progress flow updates, Inventory Stock Ledger Tracking, CSV/Excel Parts Manifest import/export pipelines, Red Alert Dashboard for low-stock triggers.
* **Database Mapping Focus:** `Inventory`, `JobItems`.
* **Default Passcode:** `0002`

### 💰 Member 4: Financial Accounting & Billing Checkout
* **Modules Built:** Cashier Settlement Desk, Invoice Billing Ledger, Financial Analytics Suite.
* **Key Features:** **Innovative QR Module Part 2** (On-screen camera scanner integration for zero-input rapid checkout billing lookup), Automatic dual-cost calculation vectors, Native multi-format reporting exports (PDF & Excel generation engines).
* **Database Mapping Focus:** `Invoices`.
* **Default Passcode:** `0003`

---

## ⚙️ Setup and Installation Instructions

Follow these instructions exactly to compile and run the application locally on a Windows platform.

### Prerequisites
* **Operating System:** Windows 10 or Windows 11
* **IDE:** Visual Studio 2022 (Community, Professional, or Enterprise editions)
* **Workload:** `.NET Desktop Development` (with C# language features enabled)
* **Framework Runtime:** .NET 8.0 SDK or newer

### Installation Steps

1.  **Extract the Files:** Extract the contents of the submitted project archive (`GroupNo_ProjectTitle.zip`) into a localized working directory on your filesystem.
2.  **Open the Solution:** * Launch Visual Studio 2022.
    * Select **Open a project or solution**.
    * Navigate to the extracted directory and select the `AutoCareSuite.sln` solution file.
3.  **Restore Dependency Packages:**
    * Open the **Package Manager Console** (`Tools > NuGet Package Manager > Package Manager Console`).
    * Execute the package restore command to gather database and reporting infrastructure components:
        ```bash
        dotnet restore
        ```
4.  **Database Provisioning:**
    * The localized database file (`autocare.db`) initializes automatically in the output target context on the first execution via the internal `DatabaseHelper.cs` controller.
    * The initialization routines will inject 50 mock entries into the application layout structure instantly to populate operational dashboards with valid test telemetry.
5.  **Compile and Run:**
    * Set the active solution build profile flag configuration to `Debug` / `Any CPU`.
    * Press **F5** or click the **Start/Play** action icon inside the Visual Studio ribbon to compile and target runtime processing.

---

## 🖥️ Evaluation Framework Reference

* **Data Consistency:** Visual metrics charts scale dynamically via pre-configured layout records.
* **Git Integrity:** Complete commit histories remain logged on local branches to verify discrete code configurations authored per participant.
* **Innovative Elements:** Native execution of QR standard configurations handles automatic lookup operations across the system.
