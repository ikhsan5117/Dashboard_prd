using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;
using DashboardTest.Models;

namespace DashboardTest.Repositories
{
    public class DashboardRepository
    {
        private readonly string _connectionString;

        public DashboardRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<DashboardViewModel> GetDashboardDataAsync(int plantId, DateTime startDate, DateTime endDate, string line = null, string jenisNg = null, string kategoriNg = null, string partCode = null)
        {
            var viewModel = new DashboardViewModel
            {
                PlantId = plantId,
                StartDate = startDate,
                EndDate = endDate,
                DataKpi = new DashboardKpi(),
                ChartMonthly = new List<DashboardChartTrend>(),
                ChartWeekly = new List<DashboardChartTrend>(),
                ChartDaily = new List<DashboardChartTrend>(),
                ChartParetoPart = new List<DashboardChartPareto>(),
                ChartNgCriteria = new List<DashboardChartDonut>(),
                TablePartRejection = new List<DashboardTablePart>(),
                TableNgRejection = new List<DashboardTableNg>()
            };

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // Build dynamic filter
                string dynamicFilterHeader = "";
                if (!string.IsNullOrEmpty(line)) dynamicFilterHeader += " AND CAST(AreaId AS VARCHAR) = @Line";
                if (!string.IsNullOrEmpty(jenisNg)) dynamicFilterHeader += " AND [Group] = @JenisNg";
                if (!string.IsNullOrEmpty(kategoriNg)) dynamicFilterHeader += " AND KeteranganNG = @KategoriNg";
                if (!string.IsNullOrEmpty(partCode)) dynamicFilterHeader += " AND KodeItem = @PartCode";

                string dynamicFilterDetail = dynamicFilterHeader
                    .Replace("[Group]", "JenisNg")
                    .Replace("KeteranganNG", "AlasanNG");

                void AddParameters(SqlCommand cmd)
                {
                    cmd.Parameters.AddWithValue("@PlantId", plantId);
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    if (!string.IsNullOrEmpty(line)) cmd.Parameters.AddWithValue("@Line", line);
                    if (!string.IsNullOrEmpty(jenisNg)) cmd.Parameters.AddWithValue("@JenisNg", jenisNg);
                    if (!string.IsNullOrEmpty(kategoriNg)) cmd.Parameters.AddWithValue("@KategoriNg", kategoriNg);
                    if (!string.IsNullOrEmpty(partCode)) cmd.Parameters.AddWithValue("@PartCode", partCode);
                }

                // 1. KPI Data - Using the corrected View for everything
                string sqlKpi = $@"
                    SELECT 
                        (SELECT ISNULL(SUM(QtyOk), 0) FROM produksi.tb_elwp_produksi_input_produksis WHERE PlantId = @PlantId AND TanggalProduksi BETWEEN @StartDate AND @EndDate {dynamicFilterHeader}) as TotalCheck,
                        ISNULL(SUM(QtyNgDetailed), 0) as TotalNG
                    FROM produksi.vw_DashboardRejectionDetailed
                    WHERE PlantId = @PlantId AND TanggalProduksi BETWEEN @StartDate AND @EndDate {dynamicFilterDetail}";

                using (var cmd = new SqlCommand(sqlKpi, conn))
                {
                    AddParameters(cmd);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            viewModel.DataKpi.TotalOk = Convert.ToInt64(reader[0]);
                            viewModel.DataKpi.TotalNg = Convert.ToInt64(reader[1]);
                            viewModel.DataKpi.TotalData = viewModel.DataKpi.TotalOk + viewModel.DataKpi.TotalNg;
                            
                            if (viewModel.DataKpi.TotalData > 0)
                                viewModel.DataKpi.RrPercentage = (double)viewModel.DataKpi.TotalNg / viewModel.DataKpi.TotalData * 100.0;
                        }
                    }
                }

                // 2. Monthly Trend
                // If filtering by specific date range (from chart click), show only that period
                // Otherwise show last 12 months for overview
                DateTime monthlyStartDate;
                DateTime monthlyEndDate;
                
                // Check if we're in drill-down mode (specific month/week/day selected)
                bool isDrillDown = (endDate - startDate).TotalDays < 32; // Less than a month = drill-down
                
                if (isDrillDown)
                {
                    // Drill-down: Show only the selected month
                    monthlyStartDate = new DateTime(startDate.Year, startDate.Month, 1);
                    monthlyEndDate = new DateTime(startDate.Year, startDate.Month, DateTime.DaysInMonth(startDate.Year, startDate.Month));
                }
                else
                {
                    // Overview: Show last 12 months
                    monthlyStartDate = startDate.AddMonths(-11);
                    monthlyEndDate = endDate;
                }
                
                string sqlMonthly = $@"
                    SELECT 
                        FORMAT(TanggalProduksi, 'MMM-yyyy') as Period,
                        YEAR(TanggalProduksi) as YearNum,
                        MONTH(TanggalProduksi) as MonthNum,
                        ISNULL(SUM(QtyOk), 0) as QtyCheck,
                        ISNULL(SUM(QtyNG), 0) as QtyNg
                    FROM produksi.tb_elwp_produksi_input_produksis
                    WHERE PlantId = @PlantId 
                      AND TanggalProduksi >= @MonthlyStartDate
                      AND TanggalProduksi <= @MonthlyEndDate
                      {dynamicFilterHeader}
                    GROUP BY YEAR(TanggalProduksi), MONTH(TanggalProduksi), FORMAT(TanggalProduksi, 'MMM-yyyy')
                    ORDER BY YearNum, MonthNum";

                using (var cmd = new SqlCommand(sqlMonthly, conn))
                {
                    AddParameters(cmd);
                    cmd.Parameters.AddWithValue("@MonthlyStartDate", monthlyStartDate);
                    cmd.Parameters.AddWithValue("@MonthlyEndDate", monthlyEndDate);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var chk = Convert.ToInt64(reader[3]);
                            var ng = Convert.ToInt64(reader[4]);
                            var total = chk + ng;
                            var y = reader.GetInt32(1);
                            var m = reader.GetInt32(2);
                            viewModel.ChartMonthly.Add(new DashboardChartTrend
                            {
                                Period = reader.GetString(0),
                                PeriodValue = $"{y}-{m:D2}",
                                QtyOk = chk,
                                QtyNg = ng,
                                RrPercentage = total > 0 ? (double)ng / total * 100.0 : 0
                            });
                        }
                    }
                }

                // 3. Pareto Part
                string sqlPareto = $@"
                    SELECT TOP 10
                        KodeItem,
                        MAX(PartName) as PartName,
                        ISNULL(SUM(QtyNgDetailed), 0) as TotalNG
                    FROM produksi.vw_DashboardRejectionDetailed
                    WHERE PlantId = @PlantId AND TanggalProduksi BETWEEN @StartDate AND @EndDate {dynamicFilterDetail}
                    GROUP BY KodeItem
                    ORDER BY TotalNG DESC";

                using (var cmd = new SqlCommand(sqlPareto, conn))
                {
                    AddParameters(cmd);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            viewModel.ChartParetoPart.Add(new DashboardChartPareto
                            {
                                KodeItem = reader.GetString(0),
                                PartName = reader.IsDBNull(1) ? "-" : reader.GetString(1),
                                TotalNg = Convert.ToInt64(reader[2])
                            });
                        }
                    }
                }

                // 4. NG Criteria (Donut) - Now 100% accurate via Smart View
                string sqlDonut = $@"
                    SELECT 
                         AlasanNG as KriteriaNG,
                         ISNULL(SUM(QtyNgDetailed), 0) as TotalNG
                    FROM produksi.vw_DashboardRejectionDetailed
                    WHERE PlantId = @PlantId 
                      AND QtyNgDetailed > 0
                      AND TanggalProduksi BETWEEN @StartDate AND @EndDate {dynamicFilterDetail}
                    GROUP BY AlasanNG
                    ORDER BY TotalNG DESC";

                using (var cmd = new SqlCommand(sqlDonut, conn))
                {
                    AddParameters(cmd);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            viewModel.ChartNgCriteria.Add(new DashboardChartDonut
                            {
                                KriteriaNg = reader.IsDBNull(0) ? "UNSPECIFIED" : reader.GetString(0).Trim(),
                                TotalNg = Convert.ToInt64(reader[1])
                            });
                        }
                    }
                }
                
                // 5. Weekly Trend
                // Drill-down logic:
                // - If a specific week is selected (7 days): show only that week (no zero-filling)
                // - If a month is selected (28-31 days): show weeks in that month
                // - Otherwise: show all weeks in current year
                DateTime weeklyStartDate;
                DateTime weeklyEndDate;
                bool isDrillDownWeek = false;
                
                int daysDiff = (int)(endDate - startDate).TotalDays;
                
                if (daysDiff <= 7)
                {
                    // Drill-down to specific week: show only that week
                    weeklyStartDate = startDate;
                    weeklyEndDate = endDate;
                    isDrillDownWeek = true; // Flag to skip zero-filling
                }
                else if (daysDiff >= 27 && daysDiff <= 31)
                {
                    // Month selected: show weeks in that month
                    weeklyStartDate = new DateTime(startDate.Year, startDate.Month, 1);
                    weeklyEndDate = new DateTime(startDate.Year, startDate.Month, DateTime.DaysInMonth(startDate.Year, startDate.Month));
                }
                else
                {
                    // Overview: show all weeks in current year
                    weeklyStartDate = new DateTime(DateTime.Now.Year, 1, 1);
                    weeklyEndDate = endDate;
                }
                
                string sqlWeekly = $@"
                    SELECT 
                        'Week ' + CAST(DATEPART(week, TanggalProduksi) as VARCHAR) as Period,
                        DATEPART(week, TanggalProduksi) as WeekNum,
                        ISNULL(SUM(QtyOk), 0) as QtyCheck,
                        ISNULL(SUM(QtyNG), 0) as QtyNg
                    FROM produksi.tb_elwp_produksi_input_produksis
                    WHERE PlantId = @PlantId 
                      AND TanggalProduksi >= @WeeklyStartDate
                      AND TanggalProduksi <= @WeeklyEndDate
                      {dynamicFilterHeader}
                    GROUP BY DATEPART(week, TanggalProduksi)
                    ORDER BY WeekNum";

                using (var cmd = new SqlCommand(sqlWeekly, conn))
                {
                    AddParameters(cmd);
                    cmd.Parameters.AddWithValue("@WeeklyStartDate", weeklyStartDate);
                    cmd.Parameters.AddWithValue("@WeeklyEndDate", weeklyEndDate);

                    var weeklyData = new Dictionary<int, DashboardChartTrend>();
                    
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int wNum = reader.GetInt32(1);
                            var chk = Convert.ToInt64(reader[2]);
                            var ng = Convert.ToInt64(reader[3]);
                            var total = chk + ng;
                            
                            weeklyData[wNum] = new DashboardChartTrend
                            {
                                Period = reader.GetString(0),
                                PeriodValue = reader.GetString(0),
                                QtyOk = chk,
                                QtyNg = ng,
                                RrPercentage = total > 0 ? (double)ng / total * 100.0 : 0
                            };
                        }
                    }

                    // If drill-down to specific week, only show that week (no zero-filling)
                    if (isDrillDownWeek)
                    {
                        // Just add the week data if it exists, otherwise add empty week
                        var culture = System.Globalization.CultureInfo.CurrentCulture;
                        var calendar = culture.Calendar;
                        var rule = System.Globalization.CalendarWeekRule.FirstDay;
                        var firstDayOfWeek = DayOfWeek.Sunday;
                        
                        int targetWeek = calendar.GetWeekOfYear(weeklyStartDate, rule, firstDayOfWeek);
                        
                        if (weeklyData.ContainsKey(targetWeek))
                        {
                            viewModel.ChartWeekly.Add(weeklyData[targetWeek]);
                        }
                        else
                        {
                            // Add empty week for the selected week
                            viewModel.ChartWeekly.Add(new DashboardChartTrend
                            {
                                Period = $"Week {targetWeek}",
                                PeriodValue = $"Week {targetWeek}",
                                QtyOk = 0,
                                QtyNg = 0,
                                RrPercentage = 0
                            });
                        }
                    }
                    else
                    {
                        // Fill in gaps (Zero-Filling) for overview mode
                        var culture = System.Globalization.CultureInfo.CurrentCulture;
                        var calendar = culture.Calendar;
                        var rule = System.Globalization.CalendarWeekRule.FirstDay;
                        var firstDayOfWeek = DayOfWeek.Sunday; // Match SQL default

                        int startWeek = calendar.GetWeekOfYear(weeklyStartDate, rule, firstDayOfWeek);
                        int endWeek = calendar.GetWeekOfYear(weeklyEndDate, rule, firstDayOfWeek);
                        
                        // Specific case: if range is within same year, fill smoothly
                        if (weeklyStartDate.Year == weeklyEndDate.Year) 
                        {
                            for (int w = startWeek; w <= endWeek; w++)
                            {
                                if (weeklyData.ContainsKey(w))
                                {
                                    viewModel.ChartWeekly.Add(weeklyData[w]);
                                }
                                else
                                {
                                    // Add empty week
                                    viewModel.ChartWeekly.Add(new DashboardChartTrend
                                    {
                                        Period = $"Week {w}",
                                        PeriodValue = $"Week {w}",
                                        QtyOk = 0,
                                        QtyNg = 0,
                                        RrPercentage = 0
                                    });
                                }
                            }
                        }
                        else
                        {
                            // Fallback if cross year, just show what we have
                            foreach(var item in weeklyData.Values) viewModel.ChartWeekly.Add(item);
                        }
                    }
                }

                // 6. Daily Trend
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $@"
                        SELECT 
                            FORMAT(TanggalProduksi, 'dd-MMM') as Period,
                            FORMAT(TanggalProduksi, 'yyyy-MM-dd') as PeriodValue,
                            ISNULL(SUM(QtyOk), 0) as QtyCheck,
                            ISNULL(SUM(QtyNG), 0) as QtyNg
                        FROM produksi.tb_elwp_produksi_input_produksis
                        WHERE PlantId = @PlantId 
                          AND TanggalProduksi BETWEEN @StartDate AND @EndDate {dynamicFilterHeader}
                        GROUP BY TanggalProduksi
                        ORDER BY TanggalProduksi";

                    AddParameters(cmd);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var chk = Convert.ToInt64(reader[2]);
                            var ng = Convert.ToInt64(reader[3]);
                            var total = chk + ng;
                            viewModel.ChartDaily.Add(new DashboardChartTrend
                            {
                                Period = reader.GetString(0),
                                PeriodValue = reader.GetString(1),
                                QtyOk = chk,
                                QtyNg = ng,
                                RrPercentage = total > 0 ? (double)ng / total * 100.0 : 0
                            });
                        }
                    }
                }

                // 7. Table Part Rejection
                string sqlTablePart = $@"
                    SELECT 
                        KodeItem,
                        ISNULL(SUM(QtyOk), 0) as QtyOk,
                        ISNULL(SUM(QtyNgDetailed), 0) as QtyNg
                    FROM (
                        SELECT 
                            KodeItem,
                            MAX(QtyOk) as QtyOk,
                            SUM(QtyNgDetailed) as QtyNgDetailed
                        FROM produksi.vw_DashboardRejectionDetailed
                        WHERE PlantId = @PlantId AND TanggalProduksi BETWEEN @StartDate AND @EndDate {dynamicFilterDetail}
                        GROUP BY ProductionId, KodeItem
                    ) t
                    GROUP BY KodeItem
                    ORDER BY QtyNg DESC";

                using (var cmd = new SqlCommand(sqlTablePart, conn))
                {
                    AddParameters(cmd);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var chk = Convert.ToInt64(reader[1]);
                            var ng = Convert.ToInt64(reader[2]);
                            var total = chk + ng;
                            viewModel.TablePartRejection.Add(new DashboardTablePart
                            {
                                KodeItem = reader.GetString(0),
                                QtyOk = chk,
                                QtyNg = ng,
                                RrPercentage = total > 0 ? (double)ng / total * 100.0 : 0
                            });
                        }
                    }
                }
            }

            return viewModel;
        }

        public async Task<FilterOptionsViewModel> GetFilterOptionsAsync(int plantId)
        {
            var options = new FilterOptionsViewModel
            {
                Months = new List<string>(),
                Dates = new List<string>(),
                JenisNg = new List<string>(),
                KategoriNg = new List<string>(),
                Lines = new List<string>(),
                PartCodes = new List<string>()
            };

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // 1. Months & Dates
                string sqlDates = @"
                    SELECT DISTINCT FORMAT(TanggalProduksi, 'yyyy-MM') as MonthVal, CAST(TanggalProduksi as DATE) as DateVal
                    FROM produksi.tb_elwp_produksi_input_produksis
                    WHERE PlantId = @PlantId AND TanggalProduksi >= DATEADD(month, -6, GETDATE())
                    ORDER BY DateVal DESC";
                
                using (var cmd = new SqlCommand(sqlDates, conn))
                {
                    cmd.Parameters.AddWithValue("@PlantId", plantId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string m = reader.GetString(0);
                            string d = reader.GetDateTime(1).ToString("yyyy-MM-dd");
                            if (!options.Months.Contains(m)) options.Months.Add(m);
                            if (!options.Dates.Contains(d)) options.Dates.Add(d);
                        }
                    }
                }

                // 2. Jenis NG, Kategori NG, Lines, PartCodes from Header Table
                string sqlOptions = @"
                    SELECT DISTINCT [Group] FROM produksi.tb_elwp_produksi_input_produksis WHERE PlantId = @PlantId AND [Group] IS NOT NULL AND [Group] <> '' ORDER BY [Group];
                    SELECT DISTINCT KeteranganNG FROM produksi.tb_elwp_produksi_input_produksis WHERE PlantId = @PlantId AND KeteranganNG IS NOT NULL AND KeteranganNG <> '-' ORDER BY KeteranganNG;
                    SELECT DISTINCT CAST(AreaId AS VARCHAR) FROM produksi.tb_elwp_produksi_input_produksis WHERE PlantId = @PlantId AND AreaId IS NOT NULL ORDER BY CAST(AreaId AS VARCHAR);
                    SELECT DISTINCT KodeItem FROM produksi.tb_elwp_produksi_input_produksis WHERE PlantId = @PlantId AND KodeItem IS NOT NULL AND KodeItem <> '' ORDER BY KodeItem;";

                using (var cmd = new SqlCommand(sqlOptions, conn))
                {
                    cmd.Parameters.AddWithValue("@PlantId", plantId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        // Result 1: Jenis NG (Group)
                        while (await reader.ReadAsync()) options.JenisNg.Add(reader.IsDBNull(0) ? "N/A" : reader.GetString(0));
                        // Result 2: Kategori NG (KeteranganNG)
                        if (await reader.NextResultAsync())
                            while (await reader.ReadAsync()) options.KategoriNg.Add(reader.IsDBNull(0) ? "N/A" : reader.GetString(0));
                        // Result 3: Lines (AreaId)
                        if (await reader.NextResultAsync())
                            while (await reader.ReadAsync()) options.Lines.Add(reader.IsDBNull(0) ? "0" : reader.GetString(0));
                        // Result 4: PartCodes (KodeItem)
                        if (await reader.NextResultAsync())
                            while (await reader.ReadAsync()) options.PartCodes.Add(reader.IsDBNull(0) ? "N/A" : reader.GetString(0));
                    }
                }
            }

            return options;
        }
        public async Task<object> GetDebugDataAsync()
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                var result = new {
                    Columns = new List<string>(),
                    Areas = new List<object>(),
                    Groups = new List<object>(),
                    Keterangan = new List<object>()
                };
                
                // Columns
                string sqlCols = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'tb_elwp_produksi_input_produksis'";
                using (var cmd = new SqlCommand(sqlCols, conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                    while (await reader.ReadAsync()) result.Columns.Add(reader.GetString(0));

                // Distinct data
                string sqlData = "SELECT DISTINCT AreaId FROM produksi.tb_elwp_produksi_input_produksis; SELECT DISTINCT [Group] FROM produksi.tb_elwp_produksi_input_produksis; SELECT DISTINCT KeteranganNG FROM produksi.tb_elwp_produksi_input_produksis;";
                using (var cmd = new SqlCommand(sqlData, conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync()) result.Areas.Add(reader[0]);
                    if (await reader.NextResultAsync()) while (await reader.ReadAsync()) result.Groups.Add(reader[0]);
                    if (await reader.NextResultAsync()) while (await reader.ReadAsync()) result.Keterangan.Add(reader[0]);
                }
                // View search
                string sqlView = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'vw_RejectionDashboard'";
                using (var cmd = new SqlCommand(sqlView, conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync()) result.Columns.Add("VIEW_COL: " + reader.GetString(0));
                }

                return result;
            }
        }
    }
}
