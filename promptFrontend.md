# Ghostbill Frontend Enhancement — Codex Task Specification (Deterministic, Backend-Frozen)

## 1. Goal
Upgrade the Ghostbill frontend (React + TypeScript + Vite + CSS) UX, performance, and safety while preserving full backend compatibility.

**Success criteria (“Done”):**
- Smooth drag-and-drop upload flow
- No stale API results
- Fast rendering with large datasets (~10k rows)
- Clear loading, empty, and error states
- Visually polished, lightweight UI

## 2. Environment Context
- React 18 (hooks only)
- TypeScript
- Vite build system
- Plain CSS only (no Tailwind, no frameworks)
- Existing backend API: `/services/api.ts` (DO NOT MODIFY)

## 3. Inputs (Existing Data Contracts)
- `AnalysisResult` and `Transaction` unchanged
- **Do NOT:** add fields, infer new backend capabilities, or change response handling logic

## 4. Constraints (Strict)
- Backend freeze: no API, DTOs, endpoints, or request structure changes
- No new dependencies
- No business logic changes (ghost/regular detection unchanged)
- Only frontend files listed in Output section may be modified

## 5. Required Architecture
### 5.1 State Separation
Implement three independent state domains:
- **fileState:** selectedFile, preview (name, size, estimatedRows), uploadStatus
- **analysisState:** loading, result, error
- **requestState:** requestId (number), AbortController

### 5.2 Request Safety
- Latest-request-wins: increment requestId per upload, abort previous request, ignore older responses

### 5.3 Memoization Rules
- Use `useMemo` for derived statistics and grouped merchant data
- Wrap merchant row component in `React.memo`

## 6. UX Behavior Specification
### 6.1 File Upload
- Drag & Drop Zone:
  - Overlay on drag enter
  - Hover/active visual state
  - MIME validation: CSV / XLSX
- File Preview (before upload):
  - File name
  - File size
  - Estimated CSV row count (best-effort, prefix-based)
- Reset Button:
  - Clears file selection and preview
  - Clears progress and drag state
  - Preserves optional result panel unless explicitly reset

### 6.2 Loading State
- Show skeleton loaders for stats and rows
- Do NOT show spinner-only UI

### 6.3 Empty State
- Display “No ghosts found — upload CSV/XLSX to scan”
- Include simple CSS-only visual placeholder
- Drag-zone pulses gently when idle, stops on drag/upload

### 6.4 Error Handling
- Only handle: `INVALID_FILE`, `PARSE_ERROR`
- Display via CSS-only toast/snackbar and aria-live region

### 6.5 Animations
**Allowed:**
- Stats count-up (RAF or CSS)
- Row reveal (staggered by spend rank or index)
- Gradient progress bars
- Subtle pulse animation for empty drag zone

**Not allowed:**
- Heavy animation libraries
- Complex motion systems

### 6.6 Merchant Row Requirements
- Display merchant name, spend value, relative spend bar (gradient)
- Mini sparkline (SVG only, no libraries)
  - Lightweight, reflects relative trend (no precise scaling required)

## 7. Performance Requirements
- Handle ~10k rows with no noticeable lag
- Minimal rerenders, stable keys, no layout thrashing

## 8. Accessibility
- `aria-live` for loading, errors, and results
- Keyboard-accessible upload
- Visible focus states

## 9. Responsiveness
- Mobile: tap-to-upload, vertical layout
- Desktop: drag-and-drop fully functional, hover visible

## 10. Dev Mode
- URL contains `?dev=1` → skip API calls, use inline mock `AnalysisResult`

## 11. Output Format
Return **only** these files, fully functional:
1. `frontend/ghostbill-ui/src/App.tsx`
2. `frontend/ghostbill-ui/src/App.css`
3. `frontend/ghostbill-ui/src/useFileUpload.ts`
4. `frontend/ghostbill-ui/src/useAnalysisMemo.ts`
5. `CHANGELOG.md` (max 5 lines)

## 12. Deliverable Rules
- No explanations or markdown outside files
- No partial code or TODOs
- Must compile without errors
- Preserve all existing logic and backend-freeze rules

## 13. Acceptance Test (Mental Simulation)
- Drag CSV/XLSX → preview shown
- Upload → skeleton loaders → animated results
- Rows staggered by spend
- Sparkline shows trends
- Reset clears file/preview/progress instantly
- Multiple uploads → no stale data
- Works on mobile + desktop

## 14. Implementation Priorities
Correctness > performance > visuals > polish
Determinism > creativity
Simplicity > abstraction

**End of specification**