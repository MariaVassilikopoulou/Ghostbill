import {
  memo,
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type DragEvent,
  type FormEvent,
  type KeyboardEvent,
} from "react";
import type { AnalysisResult, RecurringGroup } from "./types/api";
import { useAnalysisMemo } from "./useAnalysisMemo";
import { useFileUpload } from "./useFileUpload";
import "./App.css";

const mockAnalysisResult: AnalysisResult = {
  ghosts: [
    {
      merchantName: "spotify",
      transactions: [
        { date: "2026-01-01", description: "Spotify Premium", amount: -109 },
        { date: "2026-02-01", description: "Spotify Premium", amount: -109 },
        { date: "2026-03-01", description: "Spotify Premium", amount: -109 },
      ],
      averageAmount: -109,
      monthlyAmount: -109,
      yearlyCost: -1308,
      occurrenceCount: 3,
      category: "Ghost",
    },
    {
      merchantName: "streamflix",
      transactions: [
        { date: "2026-01-03", description: "Streamflix", amount: -149 },
        { date: "2026-02-03", description: "Streamflix", amount: -149 },
        { date: "2026-03-03", description: "Streamflix", amount: -159 },
      ],
      averageAmount: -152.3,
      monthlyAmount: -152.3,
      yearlyCost: -1828,
      occurrenceCount: 3,
      category: "Ghost",
    },
  ],
  regulars: [
    {
      merchantName: "electricity",
      transactions: [
        { date: "2026-01-15", description: "Utility Electric", amount: -499 },
        { date: "2026-02-15", description: "Utility Electric", amount: -519 },
        { date: "2026-03-15", description: "Utility Electric", amount: -510 },
      ],
      averageAmount: -509.3,
      monthlyAmount: -509.3,
      yearlyCost: -6112,
      occurrenceCount: 3,
      category: "Regular",
    },
  ],
  transactions: [
    { date: "2026-01-01", description: "Spotify Premium", amount: -109 },
    { date: "2026-01-03", description: "Streamflix", amount: -149 },
    { date: "2026-01-15", description: "Utility Electric", amount: -499 },
  ],
  totalTransactionsAnalyzed: 3,
  totalMonthlyGhostCost: 261,
};

function formatAmount(amount: number | undefined | null) {
  const num = amount ?? 0;
  const abs = Math.abs(num);
  return abs % 1 === 0 ? abs.toFixed(0) : abs.toFixed(2);
}

function formatMerchant(name: string) {
  return name.length <= 3
    ? name.toUpperCase()
    : name.charAt(0).toUpperCase() + name.slice(1);
}

function createSparklinePath(values: number[]): string {
  if (values.length === 0) {
    return "M0,10 L24,10";
  }

  if (values.length === 1) {
    return `M0,${10 - values[0] * 10} L24,${10 - values[0] * 10}`;
  }

  const step = 24 / (values.length - 1);
  return values
    .map((value, index) => {
      const x = (index * step).toFixed(2);
      const y = (10 - value * 10).toFixed(2);
      return `${index === 0 ? "M" : "L"}${x},${y}`;
    })
    .join(" ");
}

function useCountUp(value: number) {
  const [display, setDisplay] = useState(0);
  const rafRef = useRef<number | null>(null);

  useEffect(() => {
    if (rafRef.current) {
      cancelAnimationFrame(rafRef.current);
    }

    const start = performance.now();
    const from = display;
    const duration = 420;

    const tick = (now: number) => {
      const progress = Math.min((now - start) / duration, 1);
      const eased = 1 - Math.pow(1 - progress, 3);
      setDisplay(from + (value - from) * eased);

      if (progress < 1) {
        rafRef.current = requestAnimationFrame(tick);
      }
    };

    rafRef.current = requestAnimationFrame(tick);

    return () => {
      if (rafRef.current) {
        cancelAnimationFrame(rafRef.current);
      }
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [value]);

  return display;
}

const StatsCard = memo(function StatsCard(props: {
  label: string;
  value: number;
  suffix?: string;
}) {
  const count = useCountUp(props.value);
  return (
    <article className="stat-card">
      <p className="stat-value">
        {formatAmount(count)}
        {props.suffix ?? ""}
      </p>
      <p className="stat-label">{props.label}</p>
    </article>
  );
});

const Sparkline = memo(function Sparkline(props: { values: number[] }) {
  const path = useMemo(() => createSparklinePath(props.values), [props.values]);

  return (
    <svg className="sparkline" viewBox="0 0 24 10" aria-hidden="true" focusable="false">
      <path className="sparkline-track" d="M0,10 L24,10" />
      <path className="sparkline-line" d={path} />
    </svg>
  );
});

const MerchantRow = memo(function MerchantRow(props: {
  group: RecurringGroup;
  maxYearly: number;
  sparklineValues: number[];
}) {
  const normalizedYearly = Math.abs(props.group.yearlyCost);
  const percent = props.maxYearly <= 0 ? 0 : Math.min((normalizedYearly / props.maxYearly) * 100, 100);
  const isGhost = props.group.category === "Ghost";

  return (
    <article className="merchant-row">
      <div className="merchant-info">
        <div className="merchant-avatar">{props.group.merchantName.trim().slice(0, 2).toUpperCase()}</div>
        <div>
          <p className="merchant-name">{formatMerchant(props.group.merchantName)}</p>
          <p className="occurrence-text">{props.group.occurrenceCount} charges detected</p>
        </div>
      </div>
      <div className="merchant-right">
        <p className="merchant-tag">{isGhost ? "ghost" : "regular"}</p>
        <p className={`merchant-amount merchant-amount--primary ${isGhost ? "ghost-amount" : "regular-amount"}`}>
          {formatAmount(props.group.monthlyAmount)} SEK / month
        </p>
        <p className="merchant-amount--secondary">≈ {formatAmount(Math.round(props.group.yearlyCost))} SEK / year</p>
        <Sparkline values={props.sparklineValues} />
        <div className="progress-wrap" aria-hidden="true">
          <span className="progress-fill" style={{ width: `${percent}%` }} />
        </div>
      </div>
    </article>
  );
});

function DragZone(props: {
  fileName: string | null;
  fileSizeLabel: string | null;
  csvEstimatedRows: number | null;
  dragActive: boolean;
  loading: boolean;
  progress: number;
  onPick: (file: File | null) => Promise<void>;
  onDragState: (active: boolean) => void;
  onReset: () => void;
}) {
  const onDragEnter = (event: DragEvent<HTMLLabelElement>) => {
    event.preventDefault();
    props.onDragState(true);
  };

  const onDragOver = (event: DragEvent<HTMLLabelElement>) => {
    event.preventDefault();
    props.onDragState(true);
  };

  const onDragLeave = (event: DragEvent<HTMLLabelElement>) => {
    event.preventDefault();
    props.onDragState(false);
  };

  const onDrop = (event: DragEvent<HTMLLabelElement>) => {
    event.preventDefault();
    props.onDragState(false);
    void props.onPick(event.dataTransfer.files?.[0] ?? null);
  };

  const onKeyDown = (event: KeyboardEvent<HTMLLabelElement>) => {
    if (event.key !== "Enter" && event.key !== " ") {
      return;
    }

    event.preventDefault();
    const input = document.getElementById("csv-file-input") as HTMLInputElement | null;
    input?.click();
  };

  const showHintPulse = !props.fileName && !props.dragActive && !props.loading;

  return (
    <label
      htmlFor="csv-file-input"
      className={`upload-box ${props.dragActive ? "upload-box--active" : ""} ${showHintPulse ? "upload-box--hint" : ""}`}
      onDragEnter={onDragEnter}
      onDragOver={onDragOver}
      onDragLeave={onDragLeave}
      onDrop={onDrop}
      tabIndex={0}
      onKeyDown={onKeyDown}
      role="button"
      aria-label="Upload transaction file"
    >
      <div>
        <p className="upload-title">{props.dragActive ? "Drop to scan" : props.fileName ?? "Drop CSV/XLSX to scan"}</p>
        <p className="upload-subtitle">Fast upload, safe parsing, ghost detection.</p>

        {props.fileName && (
          <div className="file-preview" aria-live="polite">
            <p className="file-preview__line"><strong>File:</strong> {props.fileName}</p>
            <p className="file-preview__line"><strong>Size:</strong> {props.fileSizeLabel}</p>
            <p className="file-preview__line">
              <strong>Rows (est):</strong> {props.csvEstimatedRows === null ? "N/A" : props.csvEstimatedRows}
            </p>
            <button
              type="button"
              className="reset-button"
              onClick={(event) => {
                event.preventDefault();
                props.onReset();
              }}
            >
              Reset Upload
            </button>
          </div>
        )}

        {props.loading && (
          <div className="upload-progress" aria-hidden="true">
            <span className="upload-progress__fill" style={{ width: `${props.progress}%` }} />
          </div>
        )}
      </div>
    </label>
  );
}

function App() {
  const devMode = useMemo(() => {
    const params = new URLSearchParams(window.location.search);
    return params.get("dev") === "1";
  }, []);

  const {
    file,
    result,
    loading,
    dragActive,
    progress,
    toast,
    fileSizeLabel,
    csvEstimatedRows,
    setResult,
    dismissToast,
    resetUpload,
    onFileSelected,
    setDragState,
    analyze,
  } = useFileUpload();

  const { hasRecurring, topSpendingMerchants, maxRecurringYearly, trendMap, revealDelayMap } = useAnalysisMemo(result);

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    await analyze();
  };

  const useMock = useCallback(() => {
    if (devMode) {
      setResult(mockAnalysisResult);
    }
  }, [devMode, setResult]);

  const rowKey = useCallback((group: RecurringGroup) => `${group.category}:${group.merchantName}`, []);

  return (
    <main className="app">
      <header className="header">
        <h1>
          <span className="ghost-icon" aria-hidden="true">
            <span className="ghost-body"></span>
          </span>
          <span className="wordmark-ghost">Ghost</span>
          <span className="wordmark-bill">bill</span>
        </h1>
        <p className="subtitle">Find what's bleeding your wallet</p>
      </header>

      <form onSubmit={handleSubmit} className="upload-form">
        <input
          id="csv-file-input"
          className="file-input"
          type="file"
          accept=".csv,.xlsx"
          onChange={(event) => {
            void onFileSelected(event.target.files?.[0] ?? null);
          }}
        />

        <DragZone
          fileName={file?.name ?? null}
          fileSizeLabel={fileSizeLabel}
          csvEstimatedRows={csvEstimatedRows}
          dragActive={dragActive}
          loading={loading}
          progress={progress}
          onPick={onFileSelected}
          onDragState={setDragState}
          onReset={resetUpload}
        />

        <button className="analyze-button" type="submit" disabled={loading}>
          {loading ? "Analyzing..." : "Analyze"}
        </button>

        {devMode && (
          <button className="mock-button" type="button" onClick={useMock} disabled={loading}>
            Load Mock Result (?dev=1)
          </button>
        )}
      </form>

      {toast && (
        <aside className="toast" role="status" aria-live="polite">
          <span>{toast}</span>
          <button type="button" className="toast-close" onClick={dismissToast} aria-label="Dismiss message">
            ×
          </button>
        </aside>
      )}

      {loading && (
        <section className="results" aria-live="polite">
          <div className="stats-row">
            <article className="stat-card skeleton skeleton-card" />
            <article className="stat-card skeleton skeleton-card" />
          </div>
          <div className="cards">
            <article className="merchant-row skeleton skeleton-row" />
            <article className="merchant-row skeleton skeleton-row" />
            <article className="merchant-row skeleton skeleton-row" />
          </div>
        </section>
      )}

      {result && !loading && (
        <section className="results" aria-live="polite">
          <div className="stats-row">
            <StatsCard label="Total Transactions Analyzed" value={result.totalTransactionsAnalyzed} />
            {hasRecurring && <StatsCard label="Repeated Charges" value={result.totalMonthlyGhostCost} suffix=" SEK" />}
          </div>

          {hasRecurring ? (
            <>
              <section className="group-section">
                <h2 className="section-heading">Ghosts</h2>
                <div className="cards">
                  {result.ghosts.map((ghost) => {
                    const key = rowKey(ghost);
                    const trend = trendMap.get(key);
                    return (
                      <div className="row-enter" style={{ animationDelay: `${revealDelayMap.get(key) ?? 0}ms` }} key={key}>
                        <MerchantRow group={ghost} maxYearly={maxRecurringYearly} sparklineValues={trend?.normalized ?? [0.5, 0.5]} />
                      </div>
                    );
                  })}
                </div>
              </section>

              <section className="group-section">
                <h2 className="section-heading">Regulars</h2>
                <div className="cards">
                  {result.regulars.map((regular) => {
                    const key = rowKey(regular);
                    const trend = trendMap.get(key);
                    return (
                      <div className="row-enter" style={{ animationDelay: `${revealDelayMap.get(key) ?? 0}ms` }} key={key}>
                        <MerchantRow group={regular} maxYearly={maxRecurringYearly} sparklineValues={trend?.normalized ?? [0.5, 0.5]} />
                      </div>
                    );
                  })}
                </div>
              </section>
            </>
          ) : (
            <section className="group-section">
              <h2 className="section-heading">No ghosts found</h2>
              <p className="subtitle">No ghosts found—upload CSV/XLSX to scan.</p>
              <div className="cards">
                {topSpendingMerchants.length === 0 ? (
                  <article className="merchant-row">
                    <div className="merchant-info">
                      <div>
                        <p className="occurrence-text">No spending merchant data available yet.</p>
                      </div>
                    </div>
                  </article>
                ) : (
                  topSpendingMerchants.map((merchant) => (
                    <article className="merchant-row" key={merchant.merchantName}>
                      <div className="merchant-info">
                        <div className="merchant-avatar">{merchant.merchantName.trim().slice(0, 2).toUpperCase()}</div>
                        <div>
                          <p className="merchant-name">{formatMerchant(merchant.merchantName)}</p>
                          <p className="occurrence-text">Top spend</p>
                        </div>
                      </div>
                      <div className="merchant-right">
                        <p className="merchant-amount regular-amount">{formatAmount(merchant.totalSpent)} SEK</p>
                      </div>
                    </article>
                  ))
                )}
              </div>
            </section>
          )}
        </section>
      )}
    </main>
  );
}

export default App;
