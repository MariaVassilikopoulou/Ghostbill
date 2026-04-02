**Backend Job Prompt: Ghostbill XLSX Support (Contract-Bound, Additive Only)**

## Context and Goal
You are adding `.xlsx` transaction file support to Ghostbill backend while preserving existing CSV behavior and existing analysis behavior.

Goal:
- Support `.xlsx` uploads through the same analysis pipeline as CSV.
- Keep parser behavior deterministic and translator-only.
- Guarantee CSV/XLSX parity for identical data.

Must not change:
- Existing business logic behavior.
- Existing CSV parser internals.
- Existing API route/request/DTO/method signatures.
- Existing pipeline order/semantics.

## Non-Negotiable Architecture Contract
- Pipeline must remain:
  - `File -> Parser -> List<Transaction> -> Analysis -> AnalysisResult`
- Parsers are translators only.
- Business logic lives in one place (analysis services).
- No format-specific branching in analysis.

## Existing Components That Are Frozen
- `CsvParsingService` is legacy-stable and source of truth.
- No internal changes, refactors, renames, signature changes, or execution-order changes to `CsvParsingService`.

## Allowed Existing File Modifications
Only these existing files may be modified:
- Controller/orchestration entrypoint (additive integration only).
- `Program.cs` (DI registration only, additive).

No other existing files may be modified without explicit approval.

## New Components to Introduce (Exact Paths and Namespaces)
1. `backend/src/Ghostbill.Api/Parsing/Abstractions/ITransactionFileParser.cs`
- Namespace: `Ghostbill.Api.Parsing.Abstractions`

2. `backend/src/Ghostbill.Api/Parsing/Resolution/ParserResolutionService.cs`
- Namespace: `Ghostbill.Api.Parsing.Resolution`

3. `backend/src/Ghostbill.Api/Parsing/Parsers/CsvFileParserAdapter.cs`
- Namespace: `Ghostbill.Api.Parsing.Parsers`

4. `backend/src/Ghostbill.Api/Parsing/Parsers/ExcelParsingService.cs`
- Namespace: `Ghostbill.Api.Parsing.Parsers`

5. `backend/src/Ghostbill.Api/Parsing/Shared/HeaderDetectionService.cs`
- Namespace: `Ghostbill.Api.Parsing.Shared`

6. `backend/src/Ghostbill.Api/Parsing/Shared/ColumnMappingService.cs`
- Namespace: `Ghostbill.Api.Parsing.Shared`

7. `backend/src/Ghostbill.Api/Parsing/Shared/ValueParsingService.cs`
- Namespace: `Ghostbill.Api.Parsing.Shared`

8. `backend/src/Ghostbill.Api/Parsing/Shared/RowMaterializationService.cs`
- Namespace: `Ghostbill.Api.Parsing.Shared`

Important:
- Shared helpers must exist only under `Ghostbill.Api.Parsing.Shared`.
- Shared helpers must not be located inside parser implementation files.
- `ParseDiagnostics` is explicitly out of scope and must not be introduced.

## Responsibilities Per Component

### `ITransactionFileParser`
Does:
- Defines parser contract (`CanHandle`, `Parse`).

Must NOT:
- Contain parsing logic implementation.
- Contain business logic.
- Perform parser resolution.

### `ParserResolutionService`
Does:
- Resolves parser deterministically and order-independently based only on `CanHandle(extension)`.
- Enforces exactly one matching parser.

Must NOT:
- Parse files.
- Call analysis services.
- Depend on DI registration order for behavior.

Resolution rules:
- `0 matches -> UNSUPPORTED_FORMAT`
- `>1 matches -> configuration exception at startup or DI validation time (developer error, not user-facing).`
- `Must never reach the error contract response.`

### `CsvFileParserAdapter`
Does:
- Delegates to `CsvParsingService` and returns result unchanged.

Must NOT:
- Transform, filter, reorder, reinterpret, normalize, or remap CSV output in any way.
- Add wrapper behavior that changes observable CSV semantics.

Hard rule:
- Pure pass-through only. Any extra logic requires explicit approval.

### `ExcelParsingService`
Does:
- Reads first worksheet via ClosedXML.
- Converts row/cell values to parser input shape.
- Uses only shared parsing helpers for translation.
- Returns `ParseResult`.

Must NOT:
- Perform business logic (no recurrence/grouping/classification).
- Call analysis services.
- Introduce format-specific analysis/error branching.

### Shared Helpers (`Ghostbill.Api.Parsing.Shared`)
Does:
- Header detection.
- Column mapping.
- Date parsing.
- Amount parsing.
- Row materialization to `Transaction` + skipped-reason output.

Must NOT:
- Depend on `CsvParsingService`.
- Be injected into or retrofitted into CSV parser internals.
- Contain analysis logic.

## Parser Constraints (Mandatory)
For all parser components:
- `CanHandle` must be pure, deterministic, side-effect free.
- Parsers must not alter original data meaning.
- Only format translation is allowed (e.g., `string -> DateTime`, `string -> decimal`).

## Analysis Layer Constraints
- Analysis services must be called exactly as in current CSV flow.
- No new parameters, flags, parser metadata, or format identifiers.
- Analysis layer must remain unaware of which parser produced the input.
- No format-specific branching in analysis or error handling.

## Parser Resolution Rules (Authoritative)
- Guarded resolution strategy only.
- Do not use `.First(...)` as unguarded resolver.
- Resolution must be deterministic and order-independent.
- Match must be based solely on `CanHandle(extension)`.

## Error Contract (Must Match Exactly)
Response shape:
```json
{
  "message": "string",
  "code": "INVALID_FILE | UNSUPPORTED_FORMAT | PARSE_ERROR | NO_DATA_FOUND",
  "details": "string (optional)"
}
```

Mapping:
- `INVALID_FILE`: file missing, empty, or invalid.
- `UNSUPPORTED_FORMAT`: no parser found for extension.
- `PARSE_ERROR`: parser found but throws during execution.
- `NO_DATA_FOUND`: only if this is already existing CSV behavior.

Rules:
- No parser exception may leak unhandled past boundary.
- No format-specific error branching allowed.
- CSV observable error behavior must not be changed.

## Temp File Cleanup (Mandatory)
- If temp file is created, it must always be deleted in `finally`.
- Applies to all exit paths where file exists:
  - success
  - unsupported format after save
  - parse error
  - empty/no-data path
- If validation fails before file creation, cleanup is not required.

## XLSX Library Constraint
- Approved XLSX library: **ClosedXML**.
- No other XLSX library may be introduced.

## Parity Requirement (Mandatory)
For identical input data, CSV and XLSX results must be:
- Structurally equivalent.
- Semantically equivalent.
- Deterministically equivalent.

Proof requirement:
- Must be validated by automated deterministic comparison test.
- Parity cannot be asserted "by design"; it must be proven by tests.

## Blocked Actions (No Implementation Without Explicit Approval)
- Any change to `CsvParsingService` internals or signature.
- Any business logic inside parser implementations.
- Any change to pipeline order or semantics.
- Any change to API route, request field, parameter list, or DTO shape.
- Any non-deterministic or time-dependent logic.
- Any change altering observable CSV output (ordering/filtering/normalization/validation/error surface).
