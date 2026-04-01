import { useState } from "react";
import type { FormEvent } from "react";
import { analyzeTransactions } from "./services/api";
import type { AnalysisResult, RecurringGroup } from "./types/api";
import "./App.css";

function App() {
  const [file, setFile] = useState<File | null>(null);
  const [result, setResult] = useState<AnalysisResult | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const formatAmount = (amount: number | undefined | null) => {
    const num = amount ?? 0;
    const abs = Math.abs(num);
    return abs % 1 === 0 ? abs.toFixed(0) : abs.toFixed(2);
  };

  const formatMerchant = (name: string) =>
    name.length <= 3
      ? name.toUpperCase()
      : name.charAt(0).toUpperCase() + name.slice(1);

  const hasRecurring = result
    ? result.ghosts.length > 0 || result.regulars.length > 0
    : false;


const topSpendingMerchants: { merchantName: string; totalSpent: number }[] = result
  ? (() => {
      const recurringGroups = [...result.ghosts, ...result.regulars];

      if (recurringGroups.length > 0) {
        const totals: Record<string, number> = recurringGroups.reduce((acc, g) => {
          const total = g.transactions.reduce((sum, t) => sum + Math.abs(t.amount), 0);
          acc[g.merchantName] = (acc[g.merchantName] ?? 0) + total;
          return acc;
        }, {} as Record<string, number>);

        return Object.entries(totals)
          .filter(([name]) => name.trim() !== "")
          .sort((a, b) => b[1] - a[1])
          .slice(0, 3)
          .map(([merchantName, totalSpent]) => ({ merchantName, totalSpent }));
      }

      // fallback on raw transactions
      const totals: Record<string, number> = (result.transactions ?? []).reduce((acc, t) => {
        const name = t.description.trim();
        if (!name) return acc; // skip empty
        acc[name] = (acc[name] ?? 0) + Math.abs(t.amount);
        return acc;
      }, {} as Record<string, number>);

      return Object.entries(totals)
        .sort((a, b) => b[1] - a[1])
        .slice(0, 3)
        .map(([merchantName, totalSpent]) => ({ merchantName, totalSpent }));
    })()
  : [];

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError(null);

    if (!file) {
      setError("Please select a CSV file");
      return;
    }

    setLoading(true);

    try {
      const mapRecurringGroups = (groups: RecurringGroup[]) =>
        groups.map((g) => ({
          ...g,
          monthly_amount: g.monthlyAmount,
          yearly_cost: g.yearlyCost,
        }));

      const analysisResult = await analyzeTransactions(file);
      setResult({
        ...analysisResult,
        ghosts: mapRecurringGroups(analysisResult.ghosts),
        regulars: mapRecurringGroups(analysisResult.regulars),
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : "Analysis failed");
    } finally {
      setLoading(false);
    }
  };

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
          accept=".csv"
          onChange={(event) => setFile(event.target.files?.[0] ?? null)}
        />
        <label htmlFor="csv-file-input" className="upload-box">
          {file ? file.name : "Drop your Swedbank CSV here"}
        </label>
        <button className="analyze-button" type="submit" disabled={loading}>
          Analyze
        </button>
      </form>

      {loading && <p className="loading">Loading...</p>}
      {error && <p className="error">{error}</p>}

      {result && (
        <section className="results">
          <div className="stats-row">
            <article className="stat-card">
              <p className="stat-value">{result.totalTransactionsAnalyzed}</p>
              <p className="stat-label">Total Transactions Analyzed</p>
            </article>
            {hasRecurring && (
              <article className="stat-card">
                <p className="stat-value">{result.totalMonthlyGhostCost} SEK</p>
                <p className="stat-label">Repeated Charges</p>
              </article>
            )}
          </div>

          {hasRecurring ? (
            <>
              {/* Ghosts Section */}
              <section className="group-section">
                <h2 className="section-heading">Ghosts</h2>
                <div className="cards">
                  {result.ghosts.map((ghost) => (
                    <article className="merchant-row" key={ghost.merchantName}>
                      <div className="merchant-info">
                        <div className="merchant-avatar">
                          {ghost.merchantName.trim().slice(0, 2).toUpperCase()}
                        </div>
                        <div>
                          <p className="merchant-name">{formatMerchant(ghost.merchantName)}</p>
                          <p className="occurrence-text">{ghost.occurrenceCount} charges detected</p>
                        </div>
                      </div>
                      <div className="merchant-right">
                        <p className="merchant-tag">
                          <span className="ghost-icon ghost-icon--small" aria-hidden="true">
                            <span className="ghost-body"></span>
                          </span>
                          ghost
                        </p>
                        <p className="merchant-amount ghost-amount merchant-amount--primary">
                          {formatAmount(ghost.monthlyAmount)} SEK / month
                        </p>
                        <p className="merchant-amount--secondary">
                          ≈ {formatAmount(Math.round(ghost.yearlyCost))} SEK / year
                        </p>
                      </div>
                    </article>
                  ))}
                </div>
              </section>

              {/* Regulars Section */}
              <section className="group-section">
                <h2 className="section-heading">Regulars</h2>
                <div className="cards">
                  {result.regulars.map((regular) => (
                    <article className="merchant-row" key={regular.merchantName}>
                      <div className="merchant-info">
                        <div className="merchant-avatar">
                          {regular.merchantName.trim().slice(0, 2).toUpperCase()}
                        </div>
                        <div>
                          <p className="merchant-name">{formatMerchant(regular.merchantName)}</p>
                          <p className="occurrence-text">{regular.occurrenceCount} charges detected</p>
                        </div>
                      </div>
                      <div className="merchant-right">
                        <p className="merchant-tag">regular</p>
                        <p className="merchant-amount regular-amount merchant-amount--primary">
                          {formatAmount(regular.monthlyAmount)} SEK / month
                        </p>
                        <p className="merchant-amount--secondary">
                          ≈ {formatAmount(Math.round(regular.yearlyCost))} SEK / year
                        </p>
                      </div>
                    </article>
                  ))}
                </div>
              </section>
            </>
          ) : (
            // ✅ No recurring charges fallback
            <section className="group-section">
              <h2 className="section-heading">Top spending</h2>
              <p className="subtitle">No repeated charges were detected in this upload.</p>
              <div className="cards">
                {topSpendingMerchants.length === 0 ? (
                  <article className="merchant-row">
                    <div className="merchant-info">
                      <div>
                        <p className="occurrence-text">No spending merchant data available.</p>
                      </div>
                    </div>
                  </article>
                ) : (
                  topSpendingMerchants.map((merchant) => (
                    <article className="merchant-row" key={merchant.merchantName}>
                      <div className="merchant-info">
                        <div className="merchant-avatar">
                          {merchant.merchantName.trim().slice(0, 2).toUpperCase()}
                        </div>
                        <div>
                          <p className="merchant-name">{formatMerchant(merchant.merchantName)}</p>
                          <p className="occurrence-text">Top spend</p>
                        </div>
                      </div>
                      <div className="merchant-right">
                        <p className="merchant-amount regular-amount">
                          {formatAmount(merchant.totalSpent)} SEK
                        </p>
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
