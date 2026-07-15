import { useState } from "react";
import type { FormEvent } from "react";
import { isAxiosError } from "axios";
import { api } from "./services/api";
import "./App.css";

type ImportCategoryTotal = {
  category: string;
  total: number;
};

type ImportPreviewTransaction = {
  transactionDate: string;
  description: string;
  sourceCategory: string | null;
  rawAmount: number;
  transactionGroup: string;
  appCategory: string;
};

type StatementAnalysis = {
  fileName: string;
  accountName: string;
  institution: string;
  productName: string;
  statementKind: string;
  totalExpenses: number;
  totalPaymentsOrRefunds: number;
  netActivity: number;
  interestPaid: number;
  feesPaid: number;
  newBalance: number;
  minimumPaymentDue: number;
  paymentDueDate: string | null;
  pointsEarned: number | null;
  previousPointsBalance: number | null;
  newPointsBalance: number | null;
  cashBackEarned: number | null;
  cashBackRedeemed: number | null;
  cashBackBalance: number | null;
  estimatedRewardValue: number;
  effectiveRewardRate: number;
  purchaseInterestRate: number | null;
  cashAdvanceInterestRate: number | null;
  rewardSummary: string;
  paymentPriority: string;
  paymentPriorityReason: string;
  categoryTotals: ImportCategoryTotal[];
  transactions: ImportPreviewTransaction[];
  notes: string[];
};

type CategoryRecommendation = {
  category: string;
  recommendedAccount: string;
  reason: string;
};

type CategoryExpenseAdvice = {
  category: string;
  expense: number;
  estimatedPointsOrCashBackValue: number;
  pointsOrCashBack: string;
  cardUsed: string;
  advisedCardToUse: string;
  reason: string;
};

type PaymentPriority = {
  accountName: string;
  institution: string;
  newBalance: number;
  minimumPaymentDue: number;
  interestPaid: number;
  purchaseInterestRate: number | null;
  cashAdvanceInterestRate: number | null;
  priority: string;
  reason: string;
};

type MultiStatementComparison = {
  statements: StatementAnalysis[];
  categoryRecommendations: CategoryRecommendation[];
  categoryExpenseAdvice: CategoryExpenseAdvice[];
  paymentPriorities: PaymentPriority[];
};

const formatCurrency = (amount: number | null | undefined) =>
  new Intl.NumberFormat("en-CA", {
    style: "currency",
    currency: "CAD",
  }).format(amount ?? 0);

const formatNumber = (value: number | null | undefined) =>
  value === null || value === undefined ? "-" : new Intl.NumberFormat("en-CA").format(value);

const formatPercent = (value: number | null | undefined) =>
  value === null || value === undefined
    ? "-"
    : new Intl.NumberFormat("en-CA", { style: "percent", minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value);

const formatRate = (value: number | null | undefined) =>
  value === null || value === undefined ? "-" : `${value.toFixed(2)}%`;

const getPriorityClass = (priority: string) => {
  if (priority === "Pay First") return "priority-high";
  if (priority === "Pay Next") return "priority-medium";
  return "priority-low";
};

function App() {
  const [statementType, setStatementType] = useState("CreditCard");
  const [files, setFiles] = useState<File[]>([]);
  const [comparison, setComparison] = useState<MultiStatementComparison | null>(null);
  const [error, setError] = useState("");
  const [isAnalyzing, setIsAnalyzing] = useState(false);

  const handleAnalyze = async (event: FormEvent) => {
    event.preventDefault();
    setError("");
    setComparison(null);

    if (files.length === 0) {
      setError("Please select at least one statement file.");
      return;
    }

    if (files.length > 5) {
      setError("Please upload a maximum of 5 statements at a time.");
      return;
    }

    const unsupportedFile = files.find((file) => {
      const name = file.name.toLowerCase();
      return !name.endsWith(".pdf") && !name.endsWith(".csv");
    });

    if (unsupportedFile) {
      setError(`${unsupportedFile.name} is not supported. Upload PDF or CSV statements.`);
      return;
    }

    const formData = new FormData();
    formData.append("statementType", statementType);
    files.forEach((file) => formData.append("files", file));

    try {
      setIsAnalyzing(true);
      const response = await api.post<MultiStatementComparison>(
        "/api/imports/statements/compare",
        formData,
        {
          headers: {
            "Content-Type": "multipart/form-data",
          },
        }
      );
      setComparison(response.data);
    } catch (requestError) {
      if (isAxiosError(requestError)) {
        const responseData = requestError.response?.data;
        if (typeof responseData === "string" && responseData.trim()) {
          setError(responseData);
          return;
        }
        if (requestError.message) {
          setError(requestError.message);
          return;
        }
      }

      setError("Unable to analyze statements.");
    } finally {
      setIsAnalyzing(false);
    }
  };

  return (
    <main className="app">
      <section className="dashboard">
        <h1>Personal Statement Analyzer</h1>
        <p>
          Upload up to 5 credit card, line of credit, mortgage, investment, or bank account statements.
          The app compares expenses, payments, interest, rewards, cashback, and payment priority.
        </p>

        <section className="import-section">
          <h2>Analyze Multiple Statements</h2>

          <form className="import-form multi-import-form" onSubmit={handleAnalyze}>
            <label>
              Statement Type
              <select
                value={statementType}
                disabled={isAnalyzing}
                onChange={(event) => setStatementType(event.target.value)}
              >
                <option value="CreditCard">Credit Card</option>
                <option value="BankChecking">Bank / Checking</option>
                <option value="LineOfCredit">Line of Credit</option>
                <option value="Mortgage">Mortgage</option>
                <option value="Investment">Investment</option>
              </select>
            </label>

            <label>
              Statement Files
              <input
                type="file"
                accept=".csv,.pdf"
                multiple
                disabled={isAnalyzing}
                onChange={(event) => {
                  const selectedFiles = Array.from(event.target.files ?? []);
                  setFiles((currentFiles) => {
                    const mergedFiles = [...currentFiles];

                    for (const selectedFile of selectedFiles) {
                      const alreadyAdded = mergedFiles.some(
                        (existingFile) =>
                          existingFile.name === selectedFile.name &&
                          existingFile.size === selectedFile.size &&
                          existingFile.lastModified === selectedFile.lastModified
                      );

                      if (!alreadyAdded && mergedFiles.length < 5) {
                        mergedFiles.push(selectedFile);
                      }
                    }

                    return mergedFiles;
                  });
                  event.currentTarget.value = "";
                }}
              />
            </label>

            <button type="submit" disabled={isAnalyzing}>
              {isAnalyzing ? "Analyzing..." : "Analyze Statements"}
            </button>
          </form>

          <p className="import-note">
            Current optimized parsers support National Bank Mastercard and Scotia Momentum Visa statements.
            You can select multiple files at once, or add files one by one. Other PDFs are accepted, but may need custom parsing rules.
          </p>

          {files.length > 0 && (
            <div className="selected-files">
              <strong>Selected files:</strong>
              {files.map((file) => (
                <span key={`${file.name}-${file.size}-${file.lastModified}`}>
                  {file.name}
                  <button
                    type="button"
                    className="remove-file-button"
                    onClick={() =>
                      setFiles((currentFiles) =>
                        currentFiles.filter(
                          (currentFile) =>
                            !(
                              currentFile.name === file.name &&
                              currentFile.size === file.size &&
                              currentFile.lastModified === file.lastModified
                            )
                        )
                      )
                    }
                  >
                    ×
                  </button>
                </span>
              ))}
            </div>
          )}

          {error && <p className="error">{error}</p>}
        </section>

        {comparison && (
          <section className="results-layout">
            <section className="import-section">
              <h2>Payment Priority</h2>
              <div className="priority-list">
                {comparison.paymentPriorities.map((priority) => (
                  <div className="priority-card" key={priority.accountName}>
                    <div>
                      <span className={`priority-pill ${getPriorityClass(priority.priority)}`}>
                        {priority.priority}
                      </span>
                      <h3>{priority.accountName}</h3>
                      <p>{priority.reason}</p>
                    </div>
                    <div className="priority-numbers">
                      <span>Balance: <strong>{formatCurrency(priority.newBalance)}</strong></span>
                      <span>Minimum due: <strong>{formatCurrency(priority.minimumPaymentDue)}</strong></span>
                      <span>Interest paid: <strong>{formatCurrency(priority.interestPaid)}</strong></span>
                    </div>
                  </div>
                ))}
              </div>
            </section>


            <section className="import-section">
              <h2>Category Expense Advice</h2>
              <p className="recommendation-intro">
                This is the main table: each expense category shows how much was spent, estimated points or cash back value, which card was used, and which card should be used from now on.
              </p>
              <div className="expense-advice-table">
                <div className="expense-advice-row expense-advice-header">
                  <span>Category</span>
                  <span>Expense</span>
                  <span>Points / Cash back</span>
                  <span>Card used</span>
                  <span>Advised card to use</span>
                </div>
                {comparison.categoryExpenseAdvice.map((advice) => (
                  <div className="expense-advice-row" key={advice.category}>
                    <strong>{advice.category}</strong>
                    <span>{formatCurrency(advice.expense)}</span>
                    <span>
                      <strong>{formatCurrency(advice.estimatedPointsOrCashBackValue)}</strong>
                      <small>{advice.pointsOrCashBack}</small>
                    </span>
                    <span>{advice.cardUsed}</span>
                    <span>
                      <strong>{advice.advisedCardToUse}</strong>
                      <small>{advice.reason}</small>
                    </span>
                  </div>
                ))}
              </div>
            </section>

            {comparison.statements.map((statement) => (
              <section className="import-section" key={statement.fileName}>
                <div className="statement-header">
                  <div>
                    <h2>{statement.accountName}</h2>
                    <p>{statement.institution} · {statement.fileName}</p>
                  </div>
                  <span className={`priority-pill ${getPriorityClass(statement.paymentPriority)}`}>
                    {statement.paymentPriority}
                  </span>
                </div>

                <div className="import-summary-grid comparison-grid">
                  <div className="summary-card"><span>Total Expenses</span><strong>{formatCurrency(statement.totalExpenses)}</strong></div>
                  <div className="summary-card"><span>Payments Done</span><strong>{formatCurrency(statement.totalPaymentsOrRefunds)}</strong></div>
                  <div className="summary-card"><span>Interest Paid</span><strong>{formatCurrency(statement.interestPaid)}</strong></div>
                  <div className="summary-card"><span>New Balance</span><strong>{formatCurrency(statement.newBalance)}</strong></div>
                  <div className="summary-card"><span>Minimum Due</span><strong>{formatCurrency(statement.minimumPaymentDue)}</strong></div>
                  <div className="summary-card"><span>Points Earned</span><strong>{formatNumber(statement.pointsEarned)}</strong></div>
                  <div className="summary-card"><span>Cash Back Earned</span><strong>{statement.cashBackEarned === null ? "-" : formatCurrency(statement.cashBackEarned)}</strong></div>
                  <div className="summary-card"><span>Cash Back Balance</span><strong>{statement.cashBackBalance === null ? "-" : formatCurrency(statement.cashBackBalance)}</strong></div>
                  <div className="summary-card"><span>Estimated Reward Value</span><strong>{formatCurrency(statement.estimatedRewardValue)}</strong></div>
                  <div className="summary-card"><span>Effective Reward Rate</span><strong>{formatPercent(statement.effectiveRewardRate)}</strong></div>
                </div>

                {statement.rewardSummary && (
                  <div className="reward-summary-box">
                    <strong>Points vs cash back advice</strong>
                    <p>{statement.rewardSummary}</p>
                  </div>
                )}

                {statement.notes.length > 0 && (
                  <div className="notes-box">
                    {statement.notes.map((note) => <p key={note}>{note}</p>)}
                  </div>
                )}

                <h3>Expenses by Category</h3>
                <div className="category-total-table">
                  {statement.categoryTotals.map((categoryTotal) => (
                    <div className="category-total-row" key={categoryTotal.category}>
                      <span>{categoryTotal.category}</span>
                      <strong>{formatCurrency(categoryTotal.total)}</strong>
                    </div>
                  ))}


            <section className="import-section">
              <h2>Payment Details and Advice</h2>
              <p className="recommendation-intro">
                This section shows payments made during the statement period, interest applied, detected rates, and the practical payment advice for each card.
              </p>
              <div className="payment-advice-table">
                <div className="payment-advice-row payment-advice-header">
                  <span>Card</span>
                  <span>Payments made</span>
                  <span>Interest applied</span>
                  <span>Rates</span>
                  <span>Advice</span>
                </div>
                {comparison.statements.map((statement) => (
                  <div className="payment-advice-row" key={`payment-${statement.fileName}`}>
                    <strong>{statement.accountName}</strong>
                    <span>{formatCurrency(statement.totalPaymentsOrRefunds)}</span>
                    <span>{formatCurrency(statement.interestPaid)}</span>
                    <span>
                      <small>Purchases: {formatRate(statement.purchaseInterestRate)}</small>
                      <small>Cash advances: {formatRate(statement.cashAdvanceInterestRate)}</small>
                    </span>
                    <span>{statement.paymentPriorityReason}</span>
                  </div>
                ))}
              </div>
            </section>
                </div>

                <details className="preview-details">
                  <summary>Show transaction preview ({statement.transactions.length})</summary>
                  <div className="import-preview-table">
                    <div className="import-preview-row import-preview-header">
                      <span>Date</span><span>Description</span><span>Category</span><span>Group</span><span>Amount</span>
                    </div>
                    {statement.transactions.slice(0, 80).map((transaction, index) => (
                      <div className="import-preview-row compact-preview-row" key={`${statement.fileName}-${transaction.description}-${index}`}>
                        <span>{transaction.transactionDate}</span>
                        <span>{transaction.description}</span>
                        <span>{transaction.appCategory}</span>
                        <span>{transaction.transactionGroup}</span>
                        <span>{formatCurrency(transaction.rawAmount)}</span>
                      </div>
                    ))}
                  </div>
                </details>
              </section>
            ))}


          </section>
        )}
      </section>
    </main>
  );
}

export default App;
