using System;
using System.Collections.Generic;
using System.Text;

namespace AutoCare.Models
{
    public class JobCardModel
    {
        public int JobCardID { get; set; }
        public string VehicleNo { get; set; }
        public string ServiceName { get; set; }
        public string MechanicName { get; set; }
        public string DateReceived { get; set; }
        public string JobStatus { get; set; } // Pending, Ongoing, Completed
    }
}
