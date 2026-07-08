import { useState } from "react";
import type { FormEvent } from "react";
import { isAxiosError } from "axios";
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

const formatCurrency = (amount: number) =>
  new Intl.NumberFormat("en-CA", {
    style: "currency",
    currency: "CAD",
  }).format(amount);

const getFileExtension = (fileName: string) =>
  fileName.slice(fileName.lastIndexOf(".")).toLowerCase();

function App() {
  const [importStatementType, setImportStatementType] = useState("CreditCard");
  const [importFile, setImportFile] = useState<File | null>(null);
  const [importSummary, setImportSummary] = useState<ImportSummary | null>(null);
  const [importError, setImportError] = useState("");
  const [importPreview, setImportPreview] = useState<ImportPreviewTransaction[]>([]);
  const [isAnalyzing, setIsAnalyzing] = useState(false);

  const handleImportSummary = async (event: FormEvent) => {
    event.preventDefault();
    setImportError("");
    setImportSummary(null);
    setImportPreview([]);

    if (!importFile) {
      setImportError("Please select a CSV or PDF file.");
      return;
    }

    const fileExtension = getFileExtension(importFile.name);

    if (fileExtension !== ".csv" && fileExtension !== ".pdf") {
      setImportError("Please select a supported CSV or PDF statement file.");
      return;
    }

    const summaryEndpoint =
      fileExtension === ".pdf"
        ? "/api/imports/statement/pdf/summary"
        : "/api/imports/statement/summary";

    const previewEndpoint =
      fileExtension === ".pdf"
        ? "/api/imports/statement/pdf/preview"
        : "/api/imports/statement/preview";

    const formData = new FormData();
    formData.append("statementType", importStatementType);
    formData.append("file", importFile);

    try {
      setIsAnalyzing(true);

      const summaryResponse = await api.post<ImportSummary>(
        summaryEndpoint,
        formData,
        {
          headers: {
            "Content-Type": "multipart/form-data",
          },
        }
      );

      const previewResponse = await api.post<ImportPreviewTransaction[]>(
        previewEndpoint,
        formData,
        {
          headers: {
            "Content-Type": "multipart/form-data",
          },
        }
      );

      setImportSummary(summaryResponse.data);
      setImportPreview(previewResponse.data);
    } catch (error) {
      if (isAxiosError(error)) {
        const responseData = error.response?.data;

        if (typeof responseData === "string" && responseData.trim()) {
          setImportError(responseData);
          return;
        }

        if (error.response?.status === 404) {
          setImportError("PDF import endpoint was not found. Please make sure the backend is restarted and the PDF endpoints exist.");
          return;
        }

        if (error.message) {
          setImportError(error.message);
          return;
        }
      }

      setImportError("Unable to analyze statement.");
    } finally {
      setIsAnalyzing(false);
    }
  };

  const netActivityClassName =
    importSummary && importSummary.netActivity >= 0
      ? "amount-positive"
      : "amount-negative";

  return (
    <main className="app">
      <section className="dashboard">
        <h1>Statement Analyzer</h1>
        <p>Upload a CSV or PDF statement to preview transactions and spending by category.</p>

        <section className="import-section">
          <h2>Analyze Statement</h2>

          <form className="import-form" onSubmit={handleImportSummary}>
            <label>
              Statement Type
              <select
                value={importStatementType}
                disabled={isAnalyzing}
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
                accept=".csv,.pdf"
                disabled={isAnalyzing}
                onChange={(event) => {
                  setImportFile(event.target.files?.[0] ?? null);
                }}
              />
            </label>

            <button type="submit" disabled={isAnalyzing}>
              {isAnalyzing ? "Analyzing..." : "Analyze"}
            </button>
          </form>

          <p className="import-note">
            CSV analysis is available now. PDF uploads are detected and will be parsed in the next PDF story.
          </p>

          {importError && <p className="error">{importError}</p>}

          {importSummary && (
            <div className="import-results">
              <div className="import-summary-grid">
                <div className="summary-card">
                  <span>Total Expenses</span>
                  <strong>{formatCurrency(importSummary.totalExpenses)}</strong>
                </div>

                <div className="summary-card">
                  <span>Payments / Refunds</span>
                  <strong>{formatCurrency(importSummary.totalPaymentsOrRefunds)}</strong>
                </div>

                <div className="summary-card">
                  <span>Net Activity</span>
                  <strong className={netActivityClassName}>
                    {formatCurrency(importSummary.netActivity)}
                  </strong>
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
                    <strong>{formatCurrency(categoryTotal.total)}</strong>
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
                        <span>{formatCurrency(transaction.rawAmount)}</span>
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