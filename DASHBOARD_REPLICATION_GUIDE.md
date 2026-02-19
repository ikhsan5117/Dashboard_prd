# AI PROMPT TEMPLATE: PREMIUM GLASSMORPHISM DASHBOARD (FULLSTACK)

<!-- COPY DARI SINI KE BAWAH UNTUK INSTRUKSI KE AI -->

**ROLE:** Expert Fullstack Developer (.NET Core + JavaScript) spesialisasi Dashboard Interaktif Visual Tinggi.

**TUGAS:**
Bangun sebuah Dashboard Monitoring satu halaman (Single Page) yang interaktif, premium, dan responsif. Ikuti standar **Design System**, **Interaction Logic**, dan **Backend Pattern** di bawah ini secara ketat.

---

### 1. DESIGN SYSTEM (CSS - STRICTLY FOLLOW)
Gunalan palet warna dan glazing effect berikut. Jangan gunakan style default Bootstrap/Browser.

```css
:root {
    --bg-dark: #0b1121;           /* Background Utama Deep Blue */
    --card-bg: #151e32;           /* Card Semi-Transparan */
    --text-primary: #ffffff;
    --text-secondary: #94a3b8;
    --accent-blue: #3b82f6;       /* Warna Utama */
    --danger: #ef4444;            /* Warna Error/NG */
    --success: #10b981;           /* Warna Sukses/OK */
    --warning: #f59e0b;           /* Warna Warning */
}

body {
    background-color: var(--bg-dark);
    color: var(--text-primary);
    font-family: 'Inter', sans-serif;
    overflow-x: hidden;
}

/* GLASS CARD COMPONENT */
.glass-card {
    background: var(--card-bg);
    border: 1px solid rgba(255, 255, 255, 0.05);
    border-radius: 12px;
    box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1);
    backdrop-filter: blur(10px);
    overflow: hidden;
}

/* TABLE MODERN STYLE */
.table-dark {
    background: transparent !important;
    --bs-table-bg: transparent;
    color: var(--text-primary);
}
.table-dark th {
    background: rgba(15, 23, 42, 0.8) !important;
    text-transform: uppercase;
    font-size: 0.75rem;
    color: var(--text-secondary);
    border-bottom: 1px solid rgba(255,255,255,0.1);
}
.table-dark td {
    border-bottom: 1px solid rgba(255,255,255,0.05);
    vertical-align: middle;
}
/* INTERACTIVE ROW */
tr { cursor: pointer; transition: background 0.2s; }
tr:hover { background: rgba(59, 130, 246, 0.1) !important; }

/* CHART INTERACTION */
.apexcharts-xaxis-label { cursor: pointer; transition: all 0.2s; }
.apexcharts-xaxis-label:hover { fill: var(--accent-blue) !important; font-weight: 700; }
```

---

### 2. FRONTEND LOGIC (JAVASCRIPT PATTERN)
Implementasikan logika interaktif berikut untuk filter dan chart.

**A. Global State Manager**
```javascript
let filterState = {
    type: null,     // 'monthly', 'weekly', 'part', 'criteria'
    value: null,
    dateRange: null // { startDate, endDate }
};

function loadData() {
    // Handling Date: Use manual formatting to avoid Timezone issues!
    const startDate = filterState.dateRange?.startDate || `${new Date().getFullYear()}-01-01`;
    const endDate = filterState.dateRange?.endDate || new Date().toISOString().split('T')[0];
    
    // Fetch data from Backend
    fetch(`/Dashboard/GetData?startDate=${startDate}&endDate=${endDate}&filterType=${filterState.type}&filterValue=${filterState.value}`)
        .then(r => r.json())
        .then(res => {
            if(res.success) {
                renderCharts(res.data);
                renderTables(res.data);
            }
        });
}

function applyFilter(type, value) {
    if (type === 'monthly') {
        const [y, m] = value.split('-');
        const lastDay = new Date(y, m, 0).getDate();
        // Manual string construction for date range
        filterState.dateRange = { 
            startDate: `${y}-${m}-01`, 
            endDate: `${y}-${m}-${String(lastDay).padStart(2, '0')}` // Precise End Date
        };
    } 
    // ... handle other types (weekly, part, criteria)
    
    filterState.type = type;
    filterState.value = value;
    loadData();
}
```

**B. Smart Chart Click Handling (PENTING)**
Setiap chart (Bar/Column/Donut) harus bisa diklik untuk memicu filter.

```javascript
events: {
    // 1. Klik pada Batang Chart
    dataPointSelection: (e, ctx, config) => {
        if (config.dataPointIndex === -1) return;
        const selected = data[config.dataPointIndex].label; // Atau properti lain yang unik
        applyFilter('category', selected);
    },
    // 2. Klik pada Label X-Axis
    xAxisLabelClick: (e, ctx, config) => {
        const selected = data[config.labelIndex].label;
        applyFilter('category', selected);
    },
    // 3. Smart Click (Area Kosong) - Untuk batang kecil yang sulit diklik
    click: (e, ctx, config) => {
        if (config.dataPointIndex === -1) {
             const hovered = ctx.w.globals.capturedDataPointIndex;
             if (hovered > -1 && hovered < data.length) {
                 applyFilter('category', data[hovered].label);
             }
        }
    }
}
```

---

### 3. BACKEND API STANDARD (C# .NET)
Backend harus mendukung filter dinamis dan mengembalikan JSON dengan struktur yang konsisten.

**A. Controller (DashboardController.cs)**
```csharp
[HttpGet]
public async Task<IActionResult> GetData(string startDate, string endDate, string filterType, string filterValue)
{
    // 1. Parse Dates safely
    DateTime start = DateTime.Parse(startDate);
    DateTime end = DateTime.Parse(endDate);

    // 2. Get Data from Repository
    var data = await _repository.GetDashboardDataAsync(start, end, filterType, filterValue);
    
    // 3. Return JSON
    return Json(new { success = true, data = data });
}
```

**B. Repository Logic (Dynamic Filtering)**
Gunakan pola ini untuk filter yang fleksibel di SQL/LINQ.

```csharp
// Contoh Logic Filter di Repository
string sqlWhere = "WHERE Tanggal BETWEEN @Start AND @End";
var parameters = new DynamicParameters();
parameters.Add("@Start", startDate);
parameters.Add("@End", endDate);

// Dynamic Filter Injection
if (!string.IsNullOrEmpty(filterType) && !string.IsNullOrEmpty(filterValue))
{
    switch (filterType.ToLower())
    {
        case "part":
            sqlWhere += " AND PartCode = @Val";
            parameters.Add("@Val", filterValue);
            break;
        case "criteria":
            sqlWhere += " AND KriteriaNG = @Val"; // Filter hanya kriteria ini
            parameters.Add("@Val", filterValue);
            break;
    }
}
// Execute Query with added filters...
```

**C. Data Model (DashboardViewModel.cs)**
Pastikan JSON response memiliki properti berikut agar sesuai dengan frontend:
```csharp
public class DashboardData
{
    // Untuk Trend Charts
    public List<ChartTrend> ChartMonthly { get; set; } // { Period, QtyCheck, QtyNg, RrPercentage }
    public List<ChartTrend> ChartWeekly { get; set; }
    
    // Untuk Pareto Chart
    public List<ParetoData> ChartPareto { get; set; } // { Label, Value }
    
    // Untuk Tables
    public List<DetailData> TableDetail { get; set; } // { Id, ItemName, QtyCheck, QtyNg, Rr }
}
```

---

### 4. SPESIFIKASI CHART KHUSUS (MIXED TREND CHART)
Gunakan konfigurasi ini untuk grafik Tren Bulanan/Mingguan (Gabungan Bar & Line).

*   **Tipe**: Mixed (Bar Stacked + Line).
*   **Series**:
    1.  **QTY CHECK** (Bar, Warna Biru `#3b82f6`)
    2.  **QTY NG** (Bar, Warna Orange `#f59e0b`)
    3.  **RR%** (Line, Warna Hijau `#10b981`, Axis Kanan)
    4.  **Target** (Line Putus-putus, Warna Merah `#ef4444`, Axis Kanan)
*   **Fitur Khusus**: Jika filter `criteria` aktif, SEMBUNYIKAN seri "QTY CHECK" agar grafik lebih fokus pada visualisasi NG saja.
