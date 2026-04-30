# YURT — Modern Coffee Ordering System

A full-stack, production-ready coffee ordering system with real-time updates, built with .NET 8 Clean Architecture and Angular 21.

## Architecture

```text
yurt/
├── backend/          # .NET 8 Clean Architecture solution
│   └── src/
│       ├── Yurt.Domain/          # Entities, enums, base classes
│       ├── Yurt.Application/     # Business logic, services, interfaces
│       ├── Yurt.Infrastructure/  # EF Core, JWT, BCrypt, SignalR hubs
│       └── Yurt.WebApi/          # ASP.NET Core controllers, middleware
└── frontend/         # Angular 21 monorepo
    └── projects/
        ├── yurt-customer/   # Customer mobile-first web app (port 4200)
        ├── yurt-admin/      # Admin desktop-first web app (port 4300)
        ├── shared-models/   # TypeScript interfaces & enums
        ├── shared-api/      # API service, auth state, SignalR service
        └── shared-ui/       # Button, Badge, Card, Toast, Skeleton, Pipes
```

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or newer
- [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) (or SQL Server Express / LocalDB)
- [Node.js 18+](https://nodejs.org/)
- [Angular CLI 21](https://angular.dev/): `npm install -g @angular/cli`

## Backend Setup

### 1. Configure the database

Edit `backend/src/Yurt.WebApi/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "YurtDb": "Server=localhost;Database=Yurt;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Jwt": {
    "Secret": "your-super-secret-key-at-least-32-characters-long",
    "Issuer": "yurt-api",
    "Audience": "yurt-client",
    "ExpiryDays": 30
  }
}
```

### 2. Apply migrations & seed data

```bash
cd backend

# Run EF Core migration (creates DB + all tables)
dotnet ef database update \
  --project src/Yurt.Infrastructure \
  --startup-project src/Yurt.WebApi

# The app seeds data on first start automatically
```

> **Note:** If `dotnet-ef` is not in your PATH, use the full path:
> `~/.dotnet/tools/dotnet-ef` (Linux/Mac) or `$env:USERPROFILE\.dotnet\tools\dotnet-ef.exe` (Windows)

### 3. Start the API

```bash
cd backend
dotnet run --project src/Yurt.WebApi
# API runs on http://localhost:5000
# Swagger UI: http://localhost:5000/swagger
```

## Frontend Setup

```bash
cd frontend
npm install
```

### Start the customer app

```bash
ng serve yurt-customer --port 4200
# Open http://localhost:4200
```

### Start the admin app

```bash
ng serve yurt-admin --port 4300
# Open http://localhost:4300
```

## Default Credentials

### Admin accounts (seeded)

| Username | Password    | Role   |
| -------- | ----------- | ------ |
| admin    | Admin@123!  | Admin  |
| worker1  | Worker@123! | Worker |

### Customer accounts

Register via the customer app using any mobile number + 4-digit PIN.

## Features

### Customer App (mobile-first)

- Register/Login with mobile number + 4-digit PIN
- Browse locations, select pickup location
- Browse menu with category filters and search
- Add items to cart, place orders
- Real-time order status updates via SignalR
- View active, history, and declined orders
- Save favorite menu items
- Profile page with order history link

### Admin App (desktop-first)

- Secure admin login (username/password)
- **Live Orders** view with real-time SignalR updates
  - Accept orders with ETA
  - Decline orders with reason
  - Progress orders through: Preparing → Ready → Completed
  - Update payment method/status
- **Menu Management**: Add/edit/delete menu items and categories
- **Locations Management**: Add/edit/delete café locations, toggle active status

## API Endpoints

| Method | Path                           | Auth     | Description                |
| ------ | ------------------------------ | -------- | -------------------------- |
| POST   | /api/auth/register             | —        | Register customer          |
| POST   | /api/auth/login                | —        | Customer login             |
| GET    | /api/auth/me                   | Customer | Get profile                |
| POST   | /api/admin/auth/login          | —        | Admin login                |
| GET    | /api/locations                 | —        | Active locations           |
| GET    | /api/menu/categories           | —        | Menu categories            |
| GET    | /api/menu                      | —        | Menu items (filter/search) |
| POST   | /api/orders                    | Customer | Place an order             |
| GET    | /api/orders/active             | Customer | Active orders              |
| GET    | /api/orders/history            | Customer | Order history              |
| GET    | /api/favorites                 | Customer | Favorites                  |
| POST   | /api/admin/orders/{id}/accept  | Admin    | Accept order               |
| POST   | /api/admin/orders/{id}/decline | Admin    | Decline order              |
| POST   | /api/admin/orders/{id}/status  | Admin    | Update status              |
| GET    | /api/admin/menu/items          | Admin    | All menu items             |
| ...    | (full CRUD for menu/locations) | Admin    |                            |

**Swagger UI:** <http://localhost:5000/swagger>

## Real-Time (SignalR)

**Hub URL:** `ws://localhost:5000/hubs/orders`

Events emitted to customers:

- `OrderCreated` — new order placed
- `OrderUpdated` — status changed
- `OrderDeclined` — order declined with reason
- `PaymentUpdated` — payment status/method updated

Events emitted to location groups (for admin):

- All of the above, scoped to the relevant location

## Tech Stack

| Layer     | Technology                                     |
| --------- | ---------------------------------------------- |
| Backend   | .NET 8, ASP.NET Core, Clean Architecture       |
| Database  | SQL Server, EF Core 8, Migrations              |
| Auth      | JWT Bearer, BCrypt (PIN hashing)               |
| Real-time | SignalR                                        |
| Frontend  | Angular 21, Signals API, Standalone components |
| Styling   | Tailwind CSS v4                                |
| Validation| FluentValidation 11                            |
| Logging   | Serilog                                        |
| Docs      | Swagger / OpenAPI                              |

## Building for Production

```bash
# Backend
cd backend
dotnet publish src/Yurt.WebApi -c Release -o publish/

# Frontend
cd frontend
ng build yurt-customer --configuration production
ng build yurt-admin --configuration production
```
