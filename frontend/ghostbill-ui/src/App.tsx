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
  groups.map(g => ({
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
          onChange={(event) => {
            const selectedFile = event.target.files?.[0] ?? null;
            setFile(selectedFile);
          }}
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
            <article className="stat-card">
              <p className="stat-value">{result.totalMonthlyGhostCost} SEK</p>
              <p className="stat-label">Total estimated recurring cost</p>
            </article>
          </div>

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
                      <p className="merchant-name">
                        {formatMerchant(ghost.merchantName)}
                      </p>
                      <p className="occurrence-text">
                        {ghost.occurrenceCount} charges detected
                      </p>
                    </div>
                  </div>
                  <div className="merchant-right">
                    <p className="merchant-tag">
                      <span className="ghost-icon ghost-icon--small" aria-hidden="true">
                        <span className="ghost-body"></span>
                      </span>
                      ghost
                    </p>
                    <p className="merchant-amount ghost-amount">
                      −{formatAmount(ghost.averageAmount)} SEK
                    </p>
                    <p className="merchant-amount ghost-amount">
                     monthly_amount: −{ghost.monthlyAmount !== undefined ? formatAmount(ghost.monthlyAmount) : "0"} SEK
                    </p>
                    <p className="merchant-amount ghost-amount">
                      yearly_cost:   −{ghost.yearlyCost !== undefined ? formatAmount(ghost.yearlyCost) : "0"} SEK
                    </p>
                  </div>
                </article>
              ))}
            </div>
          </section>

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
                      <p className="merchant-name">
                        {formatMerchant(regular.merchantName)}
                      </p>
                      <p className="occurrence-text">
                        {regular.occurrenceCount} charges detected
                      </p>
                    </div>
                  </div>
                  <div className="merchant-right">
                    <p className="merchant-tag">regular</p>
                    <p className="merchant-amount regular-amount">
                      −{formatAmount(regular.averageAmount)} SEK
                    </p>
                    <p className="merchant-amount regular-amount">
                     monthly_amount: −{regular.monthlyAmount !== undefined ? formatAmount(regular.monthlyAmount) : "0"} SEK
                    </p>
                    <p className="merchant-amount regular-amount">
                      yearly_cost:   −{regular.yearlyCost !== undefined ? formatAmount(regular.yearlyCost) : "0"} SEK
                    </p>
                  </div>
                </article>
              ))}
            </div>
          </section>
        </section>
      )}
    </main>
  );
}

export default App;

