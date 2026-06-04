using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using AutoCare.Models;

namespace AutoCare.Services
{
    public class InventoryService
    {
        // ඔයාලගේ ටීම් එක පාවිච්චි කරන SQLite Connection String එක මෙතනට දාන්න
        private string connectionString = "Data Source=AutoCare.db";

        // 1. සියලුම බඩු තොගය ඩේටාබේස් එකෙන් ගැනීම
        public List<InventoryItem> GetAllInventory()
        {
            var items = new List<InventoryItem>();
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                string query = "SELECT ItemID, ItemName, Quantity, MinStockLevel, UnitPrice FROM Inventory";
                using (var command = new SqliteCommand(query, connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(new InventoryItem
                            {
                                ItemID = reader.GetInt32(0),
                                ItemName = reader.GetString(1),
                                Quantity = reader.GetInt32(2),
                                MinStockLevel = reader.GetInt32(3),
                                UnitPrice = reader.GetDecimal(4)
                            });
                        }
                    }
                }
            }
            return items;
        }

        // 2. අලුත් අයිටම් එකක් DB එකට එකතු කිරීම
        public bool AddItem(InventoryItem item)
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                string query = "INSERT INTO Inventory (ItemName, Quantity, MinStockLevel, UnitPrice) VALUES (@ItemName, @Quantity, @MinStockLevel, @UnitPrice)";
                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@ItemName", item.ItemName);
                    command.Parameters.AddWithValue("@Quantity", item.Quantity);
                    command.Parameters.AddWithValue("@MinStockLevel", item.MinStockLevel);
                    command.Parameters.AddWithValue("@UnitPrice", item.UnitPrice);
                    return command.ExecuteNonQuery() > 0;
                }
            }
        }

        // 3. Low Stock තියෙන බඩු විතරක් වෙනම ඇලර්ට් එකට ගැනීම
        public List<InventoryItem> GetLowStockItems()
        {
            var lowStockList = new List<InventoryItem>();
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                string query = "SELECT ItemID, ItemName, Quantity, MinStockLevel, UnitPrice FROM Inventory WHERE Quantity <= MinStockLevel";
                using (var command = new SqliteCommand(query, connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lowStockList.Add(new InventoryItem
                            {
                                ItemID = reader.GetInt32(0),
                                ItemName = reader.GetString(1),
                                Quantity = reader.GetInt32(2),
                                MinStockLevel = reader.GetInt32(3),
                                UnitPrice = reader.GetDecimal(4)
                            });
                        }
                    }
                }
            }
            return lowStockList;
        }
    }
}