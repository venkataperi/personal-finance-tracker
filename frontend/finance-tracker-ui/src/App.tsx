import { useState } from "react";
import type { FormEvent } from "react";
import { api } from "./services/api";
import "./App.css";

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
  const [importStatementType, setImportStatementType] = useState("CreditCard");
  const [importFile, setImportFile] = useState<File | null>(null);
  const [importSummary, setImportSummary] = useState<ImportSummary | null>(null);
  const [importError, setImportError] = useState("");
  const [importPreview, setImportPreview] = useState<ImportPreviewTransaction[]>([]);

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
        <h1>Statement Analyzer</h1>
        <p>Upload a CSV statement to preview transactions and spending by category.</p>

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
                      <div
                        className="import-preview-row"
                        key={`${transaction.description}-${index}`}
                      >
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
      </section>
    </main>
  );
}

export default App;
