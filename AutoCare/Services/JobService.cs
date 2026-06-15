using System;
using System.Collections.Generic;
using AutoCare.Models;
using Microsoft.Data.Sqlite;

namespace AutoCare.Services
{
    public class JobService
    {
        // 3. Chart එක සඳහා Status අනුව ජොබ් ප්‍රමාණයන් ගණන් කරගෙන එන ක්‍රමය
        public Dictionary<string, int> GetJobStatusCounts()
        {
            var counts = new Dictionary<string, int>
    {
        { "Pending", 0 },
        { "Ongoing", 0 },
        { "Completed", 0 }
    };

            try
            {
                using (var connection = DatabaseHelper.GetConnection())
                {
                    // 💡 නිවැරදි SQLite Column Name එක වන JobStatus පමණක් මෙහිදී සමූහගත (Group) කර ගණන් ගනු ලබයි
                    string query = "SELECT JobStatus, COUNT(*) FROM JobCards GROUP BY JobStatus;";

                    using (var command = new SqliteCommand(query, connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                if (!reader.IsDBNull(0))
                                {
                                    string status = reader.GetString(0).Trim();

                                    // ඩේටාබේස් එකේ "In Progress" කියලා තිබුණොත් ඒක "Ongoing" විදිහට එකතු කරනවා
                                    if (status == "In Progress") status = "Ongoing";

                                    if (counts.ContainsKey(status))
                                    {
                                        counts[status] = reader.GetInt32(1);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // කිසියම් හේතුවකින් ඩේටාබේස් එක වැඩ නොකළහොත් ඇප් එක ක්‍රැෂ් නොවී හිස් අගයන් ලබාදේ
                return counts;
            }

            return counts;
        }

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
        // සේවකයන්ගේ නම් Database එකෙන් ලබා ගැනීමේ ක්‍රමය
        public List<string> GetAllMechanicNames()
        {
            List<string> mechanicNames = new List<string>();

            using (var connection = DatabaseHelper.GetConnection())
            {
                // JobCards වගුවේ ඇති MechanicName තීරුවෙන් ප복ිත නම් (Duplicates නැතිව) ලබා ගනී
                string query = "SELECT DISTINCT MechanicName FROM JobCards WHERE MechanicName IS NOT NULL AND MechanicName != '';";

                using (var command = new SqliteCommand(query, connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            mechanicNames.Add(reader.GetString(0));
                        }
                    }
                }
            }
            return mechanicNames;
        }

        public List<JobCard> GetServiceReportsByMonth(int month, int year)
        {
            var reports = new List<JobCard>();
            using (var connection = DatabaseHelper.GetConnection())
            {
                // We use the same format for the parameter as the database (YYYY-MM-DD)
                // Ensure month is 2 digits (e.g., '06')
                string monthStr = month.ToString("D2");
                string yearStr = year.ToString();

                // Query looks for the pattern YYYY-MM
                string query = "SELECT JobCardID, VehicleNo, MechanicName, JobStatus, DateReceived FROM JobCards " +
                               "WHERE strftime('%m', DateReceived) = @Month AND strftime('%Y', DateReceived) = @Year;";

                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Month", monthStr);
                    command.Parameters.AddWithValue("@Year", yearStr);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            reports.Add(new JobCard
                            {
                                JobID = reader.GetInt32(0),
                                VehicleNumber = reader.IsDBNull(1) ? "Unknown" : reader.GetString(1),
                                AssignedMechanic = reader.IsDBNull(2) ? "Not Assigned" : reader.GetString(2),
                                Status = reader.IsDBNull(3) ? "Pending" : reader.GetString(3),
                                CreatedDate = DateTime.TryParse(reader.GetString(4), out DateTime dt) ? dt : DateTime.Now
                            });
                        }
                    }
                }
            }
            return reports;
        }

    }
}