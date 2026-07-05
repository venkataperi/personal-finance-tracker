import { useEffect, useState } from "react";
import { api } from "./services/api";
import "./App.css";

type Summary = {
  totalIncome: number;
  totalExpenses: number;
  balance: number;
  transactionCount: number;
};

type Transaction = {
  id: string;
  categoryId: string;
  categoryName: string;
  amount: number;
  type: string;
  transactionDate: string;
  notes: string | null;
  createdAtUtc: string;
};

function App() {
  const [summary, setSummary] = useState<Summary | null>(null);
  const [transactions, setTransactions] = useState<Transaction[]>([]);
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

    api
      .get<Transaction[]>("/api/transactions")
      .then((response) => {
        setTransactions(response.data);
      })
      .catch(() => {
        setError("Unable to load transactions from API.");
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

        {transactions.length > 0 && (
          <section className="transactions-section">
            <h2>Recent Transactions</h2>

            <div className="transactions-table">
              <div className="transactions-row transactions-header">
                <span>Date</span>
                <span>Category</span>
                <span>Type</span>
                <span>Amount</span>
                <span>Notes</span>
              </div>

              {transactions.map((transaction) => (
                <div className="transactions-row" key={transaction.id}>
                  <span>{transaction.transactionDate}</span>
                  <span>{transaction.categoryName}</span>
                  <span>{transaction.type}</span>
                  <span>${transaction.amount}</span>
                  <span>{transaction.notes ?? "-"}</span>
                </div>
              ))}
            </div>
          </section>
        )}
      </section>
    </main>
  );
}

export default App;