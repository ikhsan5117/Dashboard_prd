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

        public DashboardKpi DataKpi { get; set; } = new DashboardKpi();
        public List<DashboardChartTrend> ChartMonthly { get; set; } = new List<DashboardChartTrend>();
        public List<DashboardChartTrend> ChartWeekly { get; set; } = new List<DashboardChartTrend>();
        public List<DashboardChartTrend> ChartDaily { get; set; } = new List<DashboardChartTrend>();
        public List<DashboardChartPareto> ChartParetoPart { get; set; } = new List<DashboardChartPareto>();
        public List<DashboardChartDonut> ChartNgCriteria { get; set; } = new List<DashboardChartDonut>();
        public List<DashboardTablePart> TablePartRejection { get; set; } = new List<DashboardTablePart>();
        public List<DashboardTableNg> TableNgRejection { get; set; } = new List<DashboardTableNg>();
    }

    public class DashboardKpi
    {
        public long TotalOk { get; set; } // QtyOk (per user request)
        public long TotalNg { get; set; }
        public long TotalData { get; set; } // QtyOk + QtyNg
        public double RrPercentage { get; set; }
    }

    public class DashboardChartTrend
    {
        public string Period { get; set; } = string.Empty;
        public string PeriodValue { get; set; } = string.Empty;
        public long QtyOk { get; set; }
        public long QtyNg { get; set; }
        public double RrPercentage { get; set; }
    }

    public class DashboardChartPareto
    {
        public string KodeItem { get; set; } = string.Empty;
        public string PartName { get; set; } = string.Empty;
        public long TotalNg { get; set; }
    }

    public class DashboardChartDonut
    {
        public string KriteriaNg { get; set; } = string.Empty;
        public long TotalNg { get; set; }
    }

    public class DashboardTablePart
    {
        public string KodeItem { get; set; } = string.Empty;
        public long QtyOk { get; set; }
        public long QtyNg { get; set; }
        public double RrPercentage { get; set; }
    }

    public class DashboardTableNg
    {
        public string KriteriaNg { get; set; } = string.Empty;
        public long QtyNg { get; set; }
    }

    public class FilterOptionsViewModel
    {
        public List<string> Months { get; set; } = new List<string>();
        public List<string> Dates { get; set; } = new List<string>();
        public List<string> JenisNg { get; set; } = new List<string>();
        public List<string> KategoriNg { get; set; } = new List<string>();
        public List<string> Lines { get; set; } = new List<string>();
        public List<string> PartCodes { get; set; } = new List<string>();
    }
}
