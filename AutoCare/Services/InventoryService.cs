using System;
using System.Collections.Generic;
using AutoCare.Models;
using Microsoft.Data.Sqlite;
using System.IO;

namespace AutoCare.Services
{
    public class InventoryService
    {
        // 1. Database එකේ තියෙන ඔක්කොම Inventory Items ටික අරන් එන ක්‍රමය
        public List<InventoryItem> GetAllItems()
        {
            var items = new List<InventoryItem>();

            using (var connection = DatabaseHelper.GetConnection())
            {
                string query = "SELECT ItemID, ItemName, Quantity, MinStockLevel, UnitPrice FROM Inventory;";
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

        // 2. අලුත් Item එකක් Database එකට එකතු කරන ක්‍රමය
        public bool AddItem(InventoryItem item)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                string query = @"INSERT INTO Inventory (ItemName, Quantity, MinStockLevel, UnitPrice) 
                                 VALUES (@ItemName, @Quantity, @MinStockLevel, @UnitPrice);";

                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@ItemName", item.ItemName);
                    command.Parameters.AddWithValue("@Quantity", item.Quantity);
                    command.Parameters.AddWithValue("@MinStockLevel", item.MinStockLevel);
                    command.Parameters.AddWithValue("@UnitPrice", item.UnitPrice);

                    int rowsAffected = command.ExecuteNonQuery();
                    return rowsAffected > 0;
                }
            }
        }

        // 3. Item එකක් Update කිරීමේ ක්‍රමය
        public bool UpdateItem(InventoryItem item)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                string query = @"UPDATE Inventory 
                         SET ItemName = @ItemName, Quantity = @Quantity, MinStockLevel = @MinStockLevel, UnitPrice = @UnitPrice 
                         WHERE ItemID = @ItemID;";

                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@ItemName", item.ItemName);
                    command.Parameters.AddWithValue("@Quantity", item.Quantity);
                    command.Parameters.AddWithValue("@MinStockLevel", item.MinStockLevel);
                    command.Parameters.AddWithValue("@UnitPrice", item.UnitPrice);
                    command.Parameters.AddWithValue("@ItemID", item.ItemID);

                    int rowsAffected = command.ExecuteNonQuery();
                    return rowsAffected > 0;
                }
            }
        }

        // 4. Item එකක් Delete කිරීමේ ක්‍රමය
        public bool DeleteItem(int itemId)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                string query = "DELETE FROM Inventory WHERE ItemID = @ItemID;";

                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@ItemID", itemId);

                    int rowsAffected = command.ExecuteNonQuery();
                    return rowsAffected > 0;
                }
            }
        }

        // 5. මුළු Inventory එකම CSV පේළි පෙළක් (String List) ලෙස ලබා දෙන ක්‍රමය (Export සඳහා)
        public List<string> ExportToCsv()
        {
            var csvLines = new List<string> { "ItemID,ItemName,Quantity,MinStockLevel,UnitPrice" }; // Header එක
            var items = GetAllItems();

            foreach (var item in items)
            {
                csvLines.Add($"{item.ItemID},{item.ItemName},{item.Quantity},{item.MinStockLevel},{item.UnitPrice}");
            }

            return csvLines;
        }

        // 6. CSV file එකක් කියවා බඩු ලැයිස්තුවක් Database එකට එකතු කරන ක්‍රමය (Import සඳහා)
        public int ImportFromCsv(string filePath)
        {
            int importedCount = 0;
            var lines = File.ReadAllLines(filePath);

            using (var connection = DatabaseHelper.GetConnection())
            {
                // පළමු පේළිය Header එක නිසා i = 1 සිට පටන් ගනී
                for (int i = 1; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i])) continue;

                    var columns = lines[i].Split(',');
                    if (columns.Length >= 4)
                    {
                        try
                        {
                            string name = columns[0].Trim();
                            int qty = int.Parse(columns[1].Trim());
                            int minLevel = int.Parse(columns[2].Trim());
                            decimal price = decimal.Parse(columns[3].Trim());

                            string query = @"INSERT INTO Inventory (ItemName, Quantity, MinStockLevel, UnitPrice) 
                                         VALUES (@ItemName, @Quantity, @MinStockLevel, @UnitPrice);";

                            using (var command = new SqliteCommand(query, connection))
                            {
                                command.Parameters.AddWithValue("@ItemName", name);
                                command.Parameters.AddWithValue("@Quantity", qty);
                                command.Parameters.AddWithValue("@MinStockLevel", minLevel);
                                command.Parameters.AddWithValue("@UnitPrice", price);

                                if (command.ExecuteNonQuery() > 0)
                                {
                                    importedCount++;
                                }
                            }
                        }
                        catch
                        {
                            // යම් පේළියක දත්ත වැරදි නම් එය මඟ හැර ඊළඟ පේළියට යයි
                            continue;
                        }
                    }
                }
            }
            return importedCount;
        }
    }
}