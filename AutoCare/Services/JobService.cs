using System;
using System.Collections.Generic;
using AutoCare.Models;
using Microsoft.Data.Sqlite;

namespace AutoCare.Services
{
    public class JobService
    {
        // 1. දැනට පද්ධතියේ තියෙන ඔක්කොම Job Cards ටික අරන් එන ක්‍රමය
        public List<JobCard> GetAllJobs()
        {
            var jobs = new List<JobCard>();

            using (var connection = DatabaseHelper.GetConnection())
            {
                // ඩේටාබේස් එකේ තියෙන ඇත්තම Column Names ටික Query එකට දාලා තියෙනවා
                string query = "SELECT JobCardID, VehicleNo, ServiceID, MechanicName, DateReceived, JobStatus FROM JobCards;";

                using (var command = new SqliteCommand(query, connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var job = new JobCard();

                            // 1. JobCardID -> JobID (index 0)
                            job.JobID = reader.GetInt32(0);

                            // 2. VehicleNo -> VehicleNumber (index 1)
                            job.VehicleNumber = reader.IsDBNull(1) ? "Unknown" : reader.GetString(1);

                            // 3. Customer Name එක වගුවේ නැති නිසා දැනට default අගයක් දානවා (නැත්නම් Warning එකක් එනවා)
                            job.CustomerName = "Registered Customer";

                            // 4. ServiceID එක Issue Description එක විදිහට පෙන්වනවා (index 2)
                            job.IssueDescription = reader.IsDBNull(2) ? "General Service" : $"Service ID: {reader.GetInt32(2)}";

                            // 5. MechanicName -> AssignedMechanic (index 3)
                            job.AssignedMechanic = reader.IsDBNull(3) ? "Not Assigned" : reader.GetString(3);

                            // 6. JobStatus -> Status (index 5)
                            job.Status = reader.IsDBNull(5) ? "Pending" : reader.GetString(5);

                            // 7. DateReceived (TEXT) -> CreatedDate DateTime එකකට හරවනවා (index 4)
                            if (!reader.IsDBNull(4))
                            {
                                if (DateTime.TryParse(reader.GetString(4), out DateTime parsedDate))
                                {
                                    job.CreatedDate = parsedDate;
                                }
                                else
                                {
                                    job.CreatedDate = DateTime.Now;
                                }
                            }
                            else
                            {
                                job.CreatedDate = DateTime.Now;
                            }

                            jobs.Add(job);
                        }
                    }
                }
            }
            return jobs;
        }

        // 2. Job එකකට Mechanic කෙනෙක් සහ Status එකක් Update කරන ක්‍රමය
        public bool AssignMechanic(int jobId, string mechanicName, string status)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                // ඩේටාබේස් එකේ තියෙන වගුව අප්ඩේට් කරන්න නිවැරදි Column Names භාවිතා කර ඇත
                string query = @"UPDATE JobCards 
                                 SET MechanicName = @Mechanic, JobStatus = @Status 
                                 WHERE JobCardID = @JobID;";

                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Mechanic", (object)mechanicName ?? DBNull.Value);
                    command.Parameters.AddWithValue("@Status", status);
                    command.Parameters.AddWithValue("@JobID", jobId);

                    int rowsAffected = command.ExecuteNonQuery();
                    return rowsAffected > 0;
                }
            }
        }
    }
}