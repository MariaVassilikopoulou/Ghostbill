The backend’s job is to safely add XLSX file support without touching existing CSV behavior, by introducing a new parser that outputs transactions in exactly the same format, which then flow through the existing analysis pipeline unchanged.
Ghostbill Architectural Contract (Optimized v2)

Purpose
- Single source-of-truth architectural contract for all Ghostbill backend tasks.
- Defines strict constraints for parsing, analysis, API behavior, and change control.
- All implementation planning must comply with this contract.

Core Goal
Analyze financial transaction files to detect:
- Ghost subscriptions → recurring outgoing payments likely forgotten
- Regular expenses → known recurring costs

Output must be:
- Deterministic
- Actionable
- Format-independent

Guiding Principles
- Parsers are translators only
- Business logic lives only in analysis layer
- Same input → same output (determinism)
- Format must not influence analysis behavior

Change Control Rules
- Additive changes only unless explicitly approved
- Do not modify existing working logic unless instructed
- Do not change:
  - API routes
  - DTOs
  - Method signatures
- If a change impacts shared logic:
  1. Explain why
  2. Explain what changes
  3. Explain advantages
  4. Wait for approval

Processing Pipeline (Non-Negotiable)
File → Parser → List<Transaction> → Analysis → AnalysisResult
- Order must never change
- No shortcuts or bypasses allowed

Architecture Overview
- Backend: ASP.NET Core Web API (.NET 9)
- Frontend: React + TypeScript + Vite
- Repo:
  - /backend
  - /frontend

Parser Architecture

Abstraction
All parsers must implement:
bool CanHandle(string extension)
ParseResult Parse(string filePath)

Parser Responsibilities
Parsers must:
- Extract raw transaction data only
- Perform format translation only

Parsers must NOT:
- Perform recurrence detection
- Perform grouping or classification
- Call analysis services
- Contain business logic

Determinism Rules
- CanHandle must be:
  - Pure
  - Deterministic
  - Side-effect free

Parser Resolution (Authoritative)
- Guarded and deterministic
- Order-independent
- Based only on CanHandle(extension)

Resolution Rules:
- 0 matches → UNSUPPORTED_FORMAT
- 1 match → use parser
- >1 matches → configuration error

Parser Implementations

CsvParsingService (Legacy)
- Source of truth
- Completely frozen

CsvFileParserAdapter (New)
- Pure pass-through wrapper

ExcelParsingService (New)
- Supports .xlsx via ClosedXML
- Uses shared helpers only

Shared Parsing Helpers
Location:
Ghostbill.Api.Parsing.Shared

Responsibilities:
- Header detection
- Column mapping
- Date parsing
- Amount parsing
- Row mapping

Rules:
- Used by new parsers only
- Not used by CsvParsingService

Analysis Layer Rules
- Format-agnostic
- No format branching
- No new parameters

API Contract
POST /api/transactions/analyze

Controller Flow
1. Validate file
2. Save temp file
3. Resolve parser
4. Parse
5. Analyze
6. Delete temp file (finally)
7. Return result

File Validation Rules
- Max 5MB
- Reject empty files
- Validate extension + MIME

Supported:
- .csv
- .xlsx

Error Contract
{
  "message": "string",
  "code": "INVALID_FILE | UNSUPPORTED_FORMAT | PARSE_ERROR | NO_DATA_FOUND",
  "details": "optional"
}

Rules:
- No unhandled exceptions
- No format-specific errors
- CSV behavior unchanged

Temp File Cleanup
- Always delete in finally if created

Determinism and Performance
- Same input → same output
- No randomness
- No time-based logic

Parity Requirement
- CSV and XLSX must produce identical results
- Must be verified by automated test

Frontend Rule
- No changes unless approved

Blocked Actions
- No CsvParsingService changes
- No business logic in parsers
- No pipeline changes
- No API changes
- No non-deterministic logic
- No CSV behavior changes

Approval Protocol
1. State change
2. Justification
3. Benefits
4. Reference rule
5. Await approval
