# Personal Finance Tracker

A full-stack personal finance tracker built with .NET 8, React, PostgreSQL, and Entity Framework Core.

This project tracks income, expenses, categories, transactions, and dashboard summary totals. It was built as a $0 cost portfolio project using free developer tools and free hosting/database options.

## Tech Stack

### Backend
- ASP.NET Core 8 Web API
- Minimal APIs
- Entity Framework Core
- PostgreSQL
- Neon Postgres
- Swagger / OpenAPI

### Frontend
- React
- TypeScript
- Vite
- Axios
- CSS

### DevOps
- Git
- GitHub
- Planned deployment with free-tier hosting

## Features

- Create income and expense categories
- View all categories
- Create income and expense transactions
- View all transactions
- Dashboard summary with:
  - Total income
  - Total expenses
  - Balance
  - Transaction count
- React frontend connected to .NET backend
- PostgreSQL database integration using EF Core migrations

## API Endpoints

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/health` | API health check |
| POST | `/api/categories` | Create a category |
| GET | `/api/categories` | Get all categories |
| POST | `/api/transactions` | Create a transaction |
| GET | `/api/transactions` | Get all transactions |
| GET | `/api/reports/summary` | Get dashboard summary |

## Project Structure

```text
personal-finance-tracker/
  backend/
    FinanceTracker.Api/
  frontend/
    finance-tracker-ui/