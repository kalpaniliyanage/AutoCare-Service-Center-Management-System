using System;
using System.Collections.Generic;
using System.Text;

namespace AutoCare.Models
{
    public class JobCard
    {
        public int JobID { get; set; }
        public string VehicleNumber { get; set; }
        public string CustomerName { get; set; }
        public string IssueDescription { get; set; }
        public string AssignedMechanic { get; set; } // Mechanic ගේ නම
        public string Status { get; set; }           // Pending, In Progress, Completed
        public DateTime CreatedDate { get; set; }
    }
}
