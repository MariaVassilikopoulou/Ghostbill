# Ghostbill Project Reference (Updated)

## Goal
Build a web app that analyzes bank transaction CSVs and detects recurring outgoing payments (“ghost” subscriptions) and other regular recurring payments. The focus is on identifying repeated payment patterns, not just displaying raw transactions.

## Tech Stack
- **Backend:** ASP.NET Core Web API, .NET 9  
- **Frontend:** React + TypeScript + Vite  
- Backend and frontend must be separate folders in a single repo.

## Repository Structure
Ghostbill.sln  
backend/src/Ghostbill.Api/...  
frontend/ghostbill-ui/...

---

## Backend Requirements

### 1. Project Setup
- Ghostbill.Api as a .NET 9 Web API  
- Enable Swagger in Development only  
- Add CORS policy allowing http://localhost:5173  
- Register CodePagesEncodingProvider for Windows-1252 CSV input  
- JSON enums must serialize as strings  

### 2. Data Models
public class Transaction {
    public DateTime Date { get; set; }
    public string Description { get; set; }
    public decimal Amount { get; set; }
}

public enum ExpenseCategory { Ghost, Regular, Noise }

public class RecurringGroup {
    public string MerchantName { get; set; }
    public List<Transaction> Transactions { get; set; }
    public decimal AverageAmount { get; set; }
    public decimal MonthlyAmount { get; set; }
    public decimal YearlyCost { get; set; }
    public int OccurrenceCount { get; set; }
    public ExpenseCategory Category { get; set; }
}

public class AnalysisResult {
    public List<RecurringGroup> Ghosts { get; set; }
    public List<RecurringGroup> Regulars { get; set; }
    public List<Transaction> Transactions { get; set; }
    public int SkippedRows { get; set; }
    public int TotalTransactionsAnalyzed { get; set; }
    public decimal TotalMonthlyGhostCost { get; set; }
}

// Internal CSV parsing result
public class ParseResult {
    public List<Transaction> Transactions { get; set; }
    public int SkippedRows { get; set; }
    public List<string> SkippedReasons { get; set; }
}

### 3. API Endpoint
- Route: api/transactions  
- POST: api/transactions/analyze  
- Accept multipart file field csvFile  
- Save to temp file → parse → analyze → return AnalysisResult → delete temp file in finally  
- Return HTTP 400 for missing file  

---

### 4. CSV Parsing Service (CsvParsingService)

#### Entry Point
ParseTransactions(string filePath) → ParseResult

#### 4a. Encoding Detection (ReadAllLinesWithEncoding)
- Try: UTF-8 BOM, UTF-8, Windows-1252  
- Strip BOM from first line  
- Throw NotSupportedException("Unable to decode CSV file") if all fail  

#### 4b. Delimiter Detection (DetectDelimiter)
- Evaluate up to 15 non-empty lines  
- Candidates: ;, ,, \t, |  
- Score based on consistent column counts and delimiter occurrences  
- Tie-break: ; > , > \t > |  
- Emit: Delimiter: '{delimiter}' ({cols} cols consistent)  

#### 4c. Header Detection (FindHeaderIndex)
- Scan first 10 lines  
- Skip empty or non-tabular lines  
- Normalize headers before matching (NormalizeHeader)  
- Accept line if: ≥2 keyword hits or all three categories (date/description/amount) present  
- Throw NotSupportedException("No valid header found") if none  
- Emit: Header line: {lineNumber}, Header: [{firstColumns}]  

#### 4d. Header Normalization (NormalizeHeader)
- Trim + lowercase  
- Swedish transliteration: å → a, ä → a, ö → o  
- Remove non-alphanumeric symbols  
- Compact whitespace  
- Examples:  
  - Bokföringsdag → bokforingsdag  
  - Belopp (SEK) → beloppsek  
  - Bokfört saldo → bokfortsaldo  

#### 4e. Column Mapping (MapColumns + FindBestColumnMatch)
- Map required columns using normalized keywords  
- Support Swedish & English:  
  - date: bokforingsdag, datum, date, transaction date, posting date  
  - description: beskrivning, text, description, name  
  - amount: belopp, amount, debit, withdrawal  
- Score exact matches and partial Contains matches  
- Throw only if required columns cannot be mapped:  
  NotSupportedException("Unsupported CSV format: required columns missing (date/description/amount)")  
- Emit: Raw headers: [...], Mapped: date={dateIdx}, desc={descIdx}, amount={amountIdx}  

#### 4f. Row Parsing (ParseRows)
- Pre-assemble multiline quoted records until quotes are balanced  
- Split using SplitCsvLine (supports quotes & escaped "")  
- Skip malformed/invalid rows with explicit SkippedReasons  
- Include only outgoing transactions (amount < 0)  
- Silently ignore non-negative amounts  
- Diagnostics per-row:  
  - Skipped line X: unbalanced quotes | raw='...'  
  - Skipped line X: column mismatch (expected at least N, got M) | raw='...'  
  - Skipped line X: invalid date '...' | raw='...'  
  - Skipped line X: invalid amount '...' | raw='...'  
- Summary: Processed {total} rows, skipped {skipped}  

#### 4g. Date Parsing (TryParseDate)
- Support formats: yyyy-MM-dd, yyyy/MM/dd, dd/MM/yyyy, MM/dd/yyyy, dd-MM-yyyy, yyyyMMdd, dd.MM.yyyy  
- Fall back: InvariantCulture → CurrentCulture  

#### 4h. Amount Parsing (TryParseAmount)
- Strip spaces, NBSP, currency markers (kr, sek, $, €, £)  
- Handle trailing minus & parenthesized negatives  
- Support comma/dot decimal separators and thousands separators  
- Parse order: InvariantCulture → sv-SE → CurrentCulture  

#### 4i. CSV-safe Line Splitter (SplitCsvLine)
- Respect quoted fields  
- Handle escaped quotes ""  
- Preserve signature: SplitCsvLine(string line, char delimiter)  

#### 4j. Preserved Method Signatures
ParseTransactions(string filePath)  
ParseRows(List<string> lines, int headerIndex, char delimiter, int descriptionIndex, int amountIndex, int dateIndex)  
SplitCsvLine(string line, char delimiter)  

---

### 5. Recurrence Detection Service
- Input: parsed Transaction list  
- Pre-filter: outgoing only, remove descriptions with swish, överföring, insättning, uttag  
- Merchant normalization: lowercase, trim, remove non-letter characters, normalize whitespace, use first two words as key  
- Group analysis:  
  - Sort by date  
  - Compute OccurrenceCount, AverageAmount  
  - Detect regularity: at least 3 transactions, monthly-like (25–35 days ±4), biweekly-like (13–15 ±2), ≥70% gaps match pattern  
  - chargesPerYear = 365 / averageGapDays (fallback 12)  
  - Amount consistency: within min(5% of |average|, 20)  
  - Category rules: frequent + regular + consistent → Ghost; frequent + regular + not consistent → Regular; else → Noise  
  - Set: MonthlyAmount = AverageAmount, YearlyCost = round(AverageAmount * chargesPerYear, 0)  
- Return only Ghost and Regular groups  

---

### 6. Analysis Output Construction
- Ghosts = groups with category Ghost  
- Regulars = groups with category Regular  
- Transactions = parsed outgoing transactions  
- SkippedRows = malformed/skipped rows count  
- TotalTransactionsAnalyzed = total parsed transactions  
- TotalMonthlyGhostCost = sum of absolute AverageAmount for ghost groups  

---

## Frontend Requirements

### 1. Project Setup
- Vite + React + TypeScript in frontend/ghostbill-ui  
- Implement analyzeTransactions(file) helper posting to: http://localhost:5094/api/transactions/analyze  

### 2. TypeScript Types
type ExpenseCategory = 'Ghost' | 'Regular' | 'Noise';  

interface Transaction { date: string; description: string; amount: number; }  

interface RecurringGroup { merchantName: string; transactions: Transaction[]; averageAmount: number; monthlyAmount: number; yearlyCost: number; occurrenceCount: number; category: ExpenseCategory; }  

interface AnalysisResult { ghosts: RecurringGroup[]; regulars: RecurringGroup[]; transactions: Transaction[]; skippedRows: number; totalTransactionsAnalyzed: number; totalMonthlyGhostCost: number; }  

### 3. App Behavior
- Single-page UI: CSV upload + Analyze button  
- Show loading & error states  
- On success: summary cards, repeated charges card if groups exist, sections for Ghosts and Regulars  
- Each merchant row: avatar initials, occurrence count, monthly & yearly SEK cost  
- If no recurring groups: show Top spending list or raw transactions  

### 4. Visual Direction
- Dark green background, gold accent gradient, Ghostbill wordmark, ghost icon motif  
- Custom CSS-heavy styling, avoid default starter look  
- Mobile responsive (~600px and ~375px breakpoints)  

---

### Implementation Notes
- Backend runs at http://localhost:5094  
- CORS origin matches frontend dev host http://localhost:5173  
- Keep endpoint/field names: POST /api/transactions/analyze, field: csvFile  
- Use camelCase JSON property names  

### Quality Checks
- dotnet build Ghostbill.sln -c Release succeeds  
- Frontend runs with npm run dev  
- Manual test: sample CSV with repeated monthly charges, noise transactions, malformed rows  

---

### Deliverables
- Complete monorepo: backend + frontend  
- Root-level README.md with run instructions: backend start, frontend start, CSV upload & inspection