import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { api } from "./services/api";
import "./App.css";

type Summary = {
  totalIncome: number;
  totalExpenses: number;
  balance: number;
  transactionCount: number;
};

type Category = {
  id: string;
  name: string;
  type: string;
  createdAtUtc: string;
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

type ImportCategoryTotal = {
  category: string;
  total: number;
};

type ImportSummary = {
  totalExpenses: number;
  totalPaymentsOrRefunds: number;
  netActivity: number;
  transactionCount: number;
  categoryTotals: ImportCategoryTotal[];
};

type ImportPreviewTransaction = {
  transactionDate: string;
  description: string;
  sourceCategory: string | null;
  rawAmount: number;
  transactionGroup: string;
  appCategory: string;
};

function App() {
  const [summary, setSummary] = useState<Summary | null>(null);
  const [categories, setCategories] = useState<Category[]>([]);
  const [transactions, setTransactions] = useState<Transaction[]>([]);
  const [categoryId, setCategoryId] = useState("");
  const [amount, setAmount] = useState("");
  const [type, setType] = useState("Expense");
  const [transactionDate, setTransactionDate] = useState("2026-07-05");
  const [notes, setNotes] = useState("");
  const [error, setError] = useState("");
  const [successMessage, setSuccessMessage] = useState("");

  const [importStatementType, setImportStatementType] = useState("CreditCard");
  const [importFile, setImportFile] = useState<File | null>(null);
  const [importSummary, setImportSummary] = useState<ImportSummary | null>(null);
  const [importError, setImportError] = useState("");

  const [importPreview, setImportPreview] = useState<ImportPreviewTransaction[]>([]);

  const loadDashboard = () => {
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

    api
      .get<Category[]>("/api/categories")
      .then((response) => {
        setCategories(response.data);

        if (!categoryId && response.data.length > 0) {
          setCategoryId(response.data[0].id);
        }
      })
      .catch(() => {
        setError("Unable to load categories from API.");
      });
  };

  useEffect(() => {
    loadDashboard();
  }, []);

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setError("");
    setSuccessMessage("");

    if (!categoryId) {
      setError("Please select a category.");
      return;
    }

    if (!amount || Number(amount) <= 0) {
      setError("Please enter an amount greater than zero.");
      return;
    }

    try {
      await api.post("/api/transactions", {
        categoryId,
        amount: Number(amount),
        type,
        transactionDate,
        notes,
      });

      setAmount("");
      setNotes("");
      setSuccessMessage("Transaction added successfully.");
      loadDashboard();
    } catch {
      setError("Unable to add transaction.");
    }
  };

  const handleImportSummary = async (event: FormEvent) => {
    event.preventDefault();
    setImportError("");
    setImportSummary(null);
    setImportPreview([]);
  
    if (!importFile) {
      setImportError("Please select a CSV file.");
      return;
    }
  
    const formData = new FormData();
    formData.append("statementType", importStatementType);
    formData.append("file", importFile);
  
    try {
      const summaryResponse = await api.post<ImportSummary>(
        "/api/imports/statement/summary",
        formData,
        {
          headers: {
            "Content-Type": "multipart/form-data",
          },
        }
      );
  
      const previewResponse = await api.post<ImportPreviewTransaction[]>(
        "/api/imports/statement/preview",
        formData,
        {
          headers: {
            "Content-Type": "multipart/form-data",
          },
        }
      );
  
      setImportSummary(summaryResponse.data);
      setImportPreview(previewResponse.data);
    } catch {
      setImportError("Unable to analyze statement.");
    }
  };

  return (
    <main className="app">
      <section className="dashboard">
        <h1>Personal Finance Tracker</h1>
        <p>Track income, expenses, and balance from your .NET backend.</p>

        {error && <p className="error">{error}</p>}
        {successMessage && <p className="success">{successMessage}</p>}

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

        <section className="form-section">
          <h2>Add Transaction</h2>

          <form className="transaction-form" onSubmit={handleSubmit}>
            <label>
              Category
              <select
                value={categoryId}
                onChange={(event) => setCategoryId(event.target.value)}
              >
                {categories.map((category) => (
                  <option key={category.id} value={category.id}>
                    {category.name}
                  </option>
                ))}
              </select>
            </label>

            <label>
              Type
              <select
                value={type}
                onChange={(event) => setType(event.target.value)}
              >
                <option value="Expense">Expense</option>
                <option value="Income">Income</option>
              </select>
            </label>

            <label>
              Amount
              <input
                type="number"
                min="0"
                step="0.01"
                value={amount}
                onChange={(event) => setAmount(event.target.value)}
                placeholder="Enter amount"
              />
            </label>

            <label>
              Date
              <input
                type="date"
                value={transactionDate}
                onChange={(event) => setTransactionDate(event.target.value)}
              />
            </label>

            <label className="notes-field">
              Notes
              <input
                type="text"
                value={notes}
                onChange={(event) => setNotes(event.target.value)}
                placeholder="Optional notes"
              />
            </label>

            <button type="submit">Add Transaction</button>
          </form>
        </section>

        <section className="import-section">
  <h2>Analyze Statement</h2>

  <form className="import-form" onSubmit={handleImportSummary}>
    <label>
      Statement Type
      <select
        value={importStatementType}
        onChange={(event) => setImportStatementType(event.target.value)}
      >
        <option value="CreditCard">Credit Card</option>
        <option value="BankChecking">Bank / Checking</option>
        <option value="Investment">Investment</option>
      </select>
    </label>

    <label>
      Statement File
      <input
        type="file"
        accept=".csv"
        onChange={(event) => {
          setImportFile(event.target.files?.[0] ?? null);
        }}
      />
    </label>

    <button type="submit">Analyze</button>
  </form>

  {importError && <p className="error">{importError}</p>}

  {importSummary && (
    <div className="import-results">
      <div className="import-summary-grid">
        <div className="summary-card">
          <span>Total Expenses</span>
          <strong>${importSummary.totalExpenses}</strong>
        </div>

        

        <div className="summary-card">
          <span>Payments / Refunds</span>
          <strong>${importSummary.totalPaymentsOrRefunds}</strong>
        </div>

        <div className="summary-card">
          <span>Net Activity</span>
          <strong>${importSummary.netActivity}</strong>
        </div>

        <div className="summary-card">
          <span>Imported Rows</span>
          <strong>{importSummary.transactionCount}</strong>
        </div>
      </div>

      <h3>Category Totals</h3>

      <div className="category-total-table">
        {importSummary.categoryTotals.map((categoryTotal) => (
          <div className="category-total-row" key={categoryTotal.category}>
            <span>{categoryTotal.category}</span>
            <strong>${categoryTotal.total}</strong>
          </div>
        ))}
      </div>

      {importPreview.length > 0 && (
  <>
    <h3>Imported Transaction Preview</h3>

    <div className="import-preview-table">
      <div className="import-preview-row import-preview-header">
        <span>Date</span>
        <span>Description</span>
        <span>Source Category</span>
        <span>App Category</span>
        <span>Group</span>
        <span>Amount</span>
      </div>

      {importPreview.map((transaction, index) => (
        <div className="import-preview-row" key={`${transaction.description}-${index}`}>
          <span>{transaction.transactionDate}</span>
          <span>{transaction.description}</span>
          <span>{transaction.sourceCategory ?? "-"}</span>
          <span>{transaction.appCategory}</span>
          <span>{transaction.transactionGroup}</span>
          <span>${transaction.rawAmount}</span>
        </div>
      ))}
    </div>
  </>
)}  
    </div>
  )}
</section>

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