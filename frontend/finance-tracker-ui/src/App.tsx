import { useEffect, useState } from "react";
import { api } from "./services/api";
import "./App.css";

type Summary = {
  totalIncome: number;
  totalExpenses: number;
  balance: number;
  transactionCount: number;
};

function App() {
  const [summary, setSummary] = useState<Summary | null>(null);
  const [error, setError] = useState("");

  useEffect(() => {
    api
      .get<Summary>("/api/reports/summary")
      .then((response) => {
        setSummary(response.data);
      })
      .catch(() => {
        setError("Unable to load summary from API.");
      });
  }, []);

  return (
    <main className="app">
      <section className="dashboard">
        <h1>Personal Finance Tracker</h1>
        <p>Track income, expenses, and balance from your .NET backend.</p>

        {error && <p className="error">{error}</p>}

        {!summary && !error && <p>Loading dashboard...</p>}

        {summary && (
          <div className="summary-grid">
            <div className="summary-card">
              <span>Total Income</span>
              <strong>${summary.totalIncome}</strong>
            </div>

            <div className="summary-card">
              <span>Total Expenses</span>
              <strong>${summary.totalExpenses}</strong>
            </div>

            <div className="summary-card">
              <span>Balance</span>
              <strong>${summary.balance}</strong>
            </div>

            <div className="summary-card">
              <span>Transactions</span>
              <strong>{summary.transactionCount}</strong>
            </div>
          </div>
        )}
      </section>
    </main>
  );
}

export default App;