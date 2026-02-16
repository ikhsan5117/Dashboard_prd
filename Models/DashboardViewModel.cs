using System;
using System.Collections.Generic;

namespace DashboardTest.Models
{
    public class DashboardViewModel
    {
        public int PlantId { get; set; }
        public int? CurrentYear { get; set; }
        public int? CurrentMonth { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public DashboardKpi DataKpi { get; set; }
        public List<DashboardChartTrend> ChartMonthly { get; set; }
        public List<DashboardChartTrend> ChartWeekly { get; set; }
        public List<DashboardChartTrend> ChartDaily { get; set; }
        public List<DashboardChartPareto> ChartParetoPart { get; set; }
        public List<DashboardChartDonut> ChartNgCriteria { get; set; }
        public List<DashboardTablePart> TablePartRejection { get; set; }
        public List<DashboardTableNg> TableNgRejection { get; set; }
    }

    public class DashboardKpi
    {
        public long TotalCheck { get; set; } // QtyOk (per user request)
        public long TotalNg { get; set; }
        public long TotalData { get; set; } // QtyOk + QtyNg
        public double RrPercentage { get; set; }
    }

    public class DashboardChartTrend
    {
        public string Period { get; set; } // "Jan-2025", "W01", "2025-01-01"
        public long QtyCheck { get; set; }
        public long QtyNg { get; set; }
        public double RrPercentage { get; set; }
    }

    public class DashboardChartPareto
    {
        public string KodeItem { get; set; }
        public string PartName { get; set; }
        public long TotalNg { get; set; }
    }

    public class DashboardChartDonut
    {
        public string KriteriaNg { get; set; }
        public long TotalNg { get; set; }
    }

    public class DashboardTablePart
    {
        public string KodeItem { get; set; }
        public long QtyCheck { get; set; }
        public long QtyNg { get; set; }
        public double RrPercentage { get; set; }
    }

    public class DashboardTableNg
    {
        public string KriteriaNg { get; set; }
        public long QtyNg { get; set; }
    }

    public class FilterOptionsViewModel
    {
        public List<string> Months { get; set; }
        public List<string> Dates { get; set; }
        public List<string> JenisNg { get; set; }
        public List<string> KategoriNg { get; set; }
        public List<string> Lines { get; set; }
        public List<string> PartCodes { get; set; }
    }
}
