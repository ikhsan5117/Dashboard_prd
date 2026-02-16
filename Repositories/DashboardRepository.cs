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
                string dynamicFilter = "";
                if (!string.IsNullOrEmpty(line)) dynamicFilter += " AND CAST(AreaId AS VARCHAR) = @Line";
                if (!string.IsNullOrEmpty(jenisNg)) dynamicFilter += " AND [Group] = @JenisNg";
                if (!string.IsNullOrEmpty(kategoriNg)) dynamicFilter += " AND KeteranganNG = @KategoriNg";
                if (!string.IsNullOrEmpty(partCode)) dynamicFilter += " AND KodeItem = @PartCode";

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

                // 1. KPI Data
                string sqlKpi = $@"
                    SELECT 
                        ISNULL(SUM(QtyOk), 0) as TotalCheck, 
                        ISNULL(SUM(QtyNG), 0) as TotalNG,
                        ISNULL(SUM(QtyOk + QtyNG), 0) as TotalData
                    FROM produksi.tb_elwp_produksi_input_produksis
                    WHERE PlantId = @PlantId AND TanggalProduksi BETWEEN @StartDate AND @EndDate {dynamicFilter}";

                using (var cmd = new SqlCommand(sqlKpi, conn))
                {
                    AddParameters(cmd);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            viewModel.DataKpi.TotalCheck = Convert.ToInt64(reader[0]);
                            viewModel.DataKpi.TotalNg = Convert.ToInt64(reader[1]);
                            viewModel.DataKpi.TotalData = Convert.ToInt64(reader[2]);
                            
                            if (viewModel.DataKpi.TotalData > 0)
                                viewModel.DataKpi.RrPercentage = (double)viewModel.DataKpi.TotalNg / viewModel.DataKpi.TotalData * 100.0;
                        }
                    }
                }

                // 2. Monthly Trend
                string sqlMonthly = $@"
                    SELECT 
                        FORMAT(TanggalProduksi, 'MMM-yyyy') as Period,
                        YEAR(TanggalProduksi) as YearNum,
                        MONTH(TanggalProduksi) as MonthNum,
                        ISNULL(SUM(QtyOk), 0) as QtyCheck,
                        ISNULL(SUM(QtyNG), 0) as QtyNg,
                        ISNULL(SUM(QtyOk + QtyNG), 0) as TotalData
                    FROM produksi.tb_elwp_produksi_input_produksis
                    WHERE PlantId = @PlantId 
                      AND TanggalProduksi >= DATEADD(year, -1, GETDATE())
                      {dynamicFilter}
                    GROUP BY YEAR(TanggalProduksi), MONTH(TanggalProduksi), FORMAT(TanggalProduksi, 'MMM-yyyy')
                    ORDER BY YearNum, MonthNum";

                using (var cmd = new SqlCommand(sqlMonthly, conn))
                {
                    AddParameters(cmd);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var totalData = Convert.ToInt64(reader[5]);
                            var ng = Convert.ToInt64(reader[4]);
                            viewModel.ChartMonthly.Add(new DashboardChartTrend
                            {
                                Period = reader.GetString(0),
                                QtyCheck = Convert.ToInt64(reader[3]),
                                QtyNg = ng,
                                RrPercentage = totalData > 0 ? (double)ng / totalData * 100.0 : 0
                            });
                        }
                    }
                }

                // 3. Pareto Part
                string sqlPareto = $@"
                    SELECT TOP 10
                        KodeItem,
                        MAX(PartName) as PartName,
                        ISNULL(SUM(QtyNG), 0) as TotalNG
                    FROM produksi.tb_elwp_produksi_input_produksis
                    WHERE PlantId = @PlantId AND TanggalProduksi BETWEEN @StartDate AND @EndDate {dynamicFilter}
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

                // 4. NG Criteria (Donut)
                string sqlDonut = $@"
                    SELECT 
                         ISNULL(NULLIF(KeteranganNG, ''), 'Undefined') as KriteriaNG,
                         ISNULL(SUM(QtyNG), 0) as TotalNG
                    FROM produksi.tb_elwp_produksi_input_produksis
                    WHERE PlantId = @PlantId 
                      AND QtyNG > 0
                      AND TanggalProduksi BETWEEN @StartDate AND @EndDate {dynamicFilter}
                    GROUP BY KeteranganNG
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
                                KriteriaNg = reader.GetString(0),
                                TotalNg = Convert.ToInt64(reader[1])
                            });
                        }
                    }
                }
                
                // 5. Weekly Trend
                string sqlWeekly = $@"
                    SELECT 
                        'W' + CAST(DATEPART(week, TanggalProduksi) as VARCHAR) as Period,
                        DATEPART(week, TanggalProduksi) as WeekNum,
                        ISNULL(SUM(QtyOk), 0) as QtyCheck,
                        ISNULL(SUM(QtyNG), 0) as QtyNg,
                        ISNULL(SUM(QtyOk + QtyNG), 0) as TotalData
                    FROM produksi.tb_elwp_produksi_input_produksis
                    WHERE PlantId = @PlantId 
                      AND YEAR(TanggalProduksi) = YEAR(GETDATE()) 
                      {dynamicFilter}
                    GROUP BY DATEPART(week, TanggalProduksi)
                    ORDER BY WeekNum";

                using (var cmd = new SqlCommand(sqlWeekly, conn))
                {
                    AddParameters(cmd);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var totalData = Convert.ToInt64(reader[4]);
                            var ng = Convert.ToInt64(reader[3]);
                            viewModel.ChartWeekly.Add(new DashboardChartTrend
                            {
                                Period = reader.GetString(0),
                                QtyCheck = Convert.ToInt64(reader[2]),
                                QtyNg = ng,
                                RrPercentage = totalData > 0 ? (double)ng / totalData * 100.0 : 0
                            });
                        }
                    }
                }

                // 6. Daily Trend
                string sqlDaily = $@"
                    SELECT 
                        FORMAT(TanggalProduksi, 'dd-MMM') as Period,
                        ISNULL(SUM(QtyOk), 0) as QtyCheck,
                        ISNULL(SUM(QtyNG), 0) as QtyNg,
                        ISNULL(SUM(QtyOk + QtyNG), 0) as TotalData
                    FROM produksi.tb_elwp_produksi_input_produksis
                    WHERE PlantId = @PlantId 
                      AND TanggalProduksi BETWEEN @StartDate AND @EndDate {dynamicFilter}
                    GROUP BY TanggalProduksi
                    ORDER BY TanggalProduksi";

                using (var cmd = new SqlCommand(sqlDaily, conn))
                {
                    AddParameters(cmd);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var totalData = Convert.ToInt64(reader[3]);
                            var ng = Convert.ToInt64(reader[2]);
                            viewModel.ChartDaily.Add(new DashboardChartTrend
                            {
                                Period = reader.GetString(0),
                                QtyCheck = Convert.ToInt64(reader[1]),
                                QtyNg = ng,
                                RrPercentage = totalData > 0 ? (double)ng / totalData * 100.0 : 0
                            });
                        }
                    }
                }

                // 7. Table Part Rejection
                string sqlTablePart = $@"
                    SELECT 
                        KodeItem,
                        ISNULL(SUM(QtyOk), 0) as QtyCheck,
                        ISNULL(SUM(QtyNG), 0) as QtyNg,
                        ISNULL(SUM(QtyOk + QtyNG), 0) as TotalData
                    FROM produksi.tb_elwp_produksi_input_produksis
                    WHERE PlantId = @PlantId AND TanggalProduksi BETWEEN @StartDate AND @EndDate {dynamicFilter}
                    GROUP BY KodeItem
                    ORDER BY TotalData DESC";

                using (var cmd = new SqlCommand(sqlTablePart, conn))
                {
                    AddParameters(cmd);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var totalData = Convert.ToInt64(reader[3]);
                            var ng = Convert.ToInt64(reader[2]);
                            viewModel.TablePartRejection.Add(new DashboardTablePart
                            {
                                KodeItem = reader.GetString(0),
                                QtyCheck = Convert.ToInt64(reader[1]),
                                QtyNg = ng,
                                RrPercentage = totalData > 0 ? (double)ng / totalData * 100.0 : 0
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

                // 2. Jenis NG, Kategori NG, Lines, PartCodes
                string sqlOptions = @"
                    SELECT DISTINCT [Group] FROM produksi.tb_elwp_produksi_input_produksis WHERE PlantId = @PlantId AND [Group] IS NOT NULL AND [Group] <> '';
                    SELECT DISTINCT KeteranganNG FROM produksi.tb_elwp_produksi_input_produksis WHERE PlantId = @PlantId AND KeteranganNG IS NOT NULL AND KeteranganNG <> '';
                    SELECT DISTINCT CAST(AreaId AS VARCHAR) FROM produksi.tb_elwp_produksi_input_produksis WHERE PlantId = @PlantId AND AreaId IS NOT NULL;
                    SELECT DISTINCT KodeItem FROM produksi.tb_elwp_produksi_input_produksis WHERE PlantId = @PlantId AND KodeItem IS NOT NULL AND KodeItem <> '';";

                using (var cmd = new SqlCommand(sqlOptions, conn))
                {
                    cmd.Parameters.AddWithValue("@PlantId", plantId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        // Result 1: Jenis NG (Group)
                        while (await reader.ReadAsync()) options.JenisNg.Add(reader.GetString(0));
                        // Result 2: Kategori NG (KeteranganNG)
                        if (await reader.NextResultAsync())
                            while (await reader.ReadAsync()) options.KategoriNg.Add(reader.GetString(0));
                        // Result 3: Lines (AreaId)
                        if (await reader.NextResultAsync())
                            while (await reader.ReadAsync()) options.Lines.Add(reader.GetString(0));
                        // Result 4: PartCodes (KodeItem)
                        if (await reader.NextResultAsync())
                            while (await reader.ReadAsync()) options.PartCodes.Add(reader.GetString(0));
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
