using System;
using System.Collections.Generic;
using System.Text;

namespace AutoCare.Models
{
    public class InventoryItem
    {
        public int ItemID { get; set; }
        public string ItemName { get; set; }
        public int Quantity { get; set; }
        public int MinStockLevel { get; set; }
        public decimal UnitPrice { get; set; }
        public bool IsLowStock => Quantity <= MinStockLevel;
    }
}
