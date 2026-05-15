# YURT — Modern Coffee Ordering System

A full-stack coffee ordering platform with a mobile-first customer experience, a desktop-first admin console, real-time order updates, and a clean .NET 8 / Angular 21 architecture.

## What this app does

Yurt is designed for café operations and customers who want fast pickup ordering. It supports:

- Customer registration and login using mobile number + 4-digit PIN
- Browsing active café locations and searchable menu categories
- Adding favorites and building orders with a mobile-friendly checkout flow
- Real-time order tracking via SignalR updates
- Admin order management, menu management, location management, promotions, and analytics

## Current capabilities

### Customer app features

- Mobile-first ordering experience
- Register and login with mobile number + PIN
- Browse multiple café locations and pick a pickup store
- Filter the menu by category and search by item name
- Add menu items to cart and place orders quickly
- View active orders, past order history, and declined orders
- Save favorite menu items for repeat ordering
- Receive real-time order status updates from the backend
- Customer profile view and order summary access

### Admin app features

- Admin login with username/password
- Live order dashboard with SignalR-powered updates
- Accept or decline incoming orders
- Set order ETA, update status, and mark orders completed
- Update payment status and payment method flags
- Manage menu categories and menu items with full CRUD
- Manage café locations and toggle active availability
- Create, update, and remove promotions
- Query analytics data for business insights

### Backend features

- Clean Architecture with separate Domain / Application / Infrastructure / WebApi layers
- ASP.NET Core 8 API with JWT role-based authentication
- SQL Server support via EF Core 8 and migrations
- BCrypt password hashing for admin accounts and secure customer PIN flows
- SignalR order hub for live order notifications
- Serilog logging and developer-friendly error handling
- Swagger/OpenAPI API documentation
- Data seeding for sample locations, menu items, and admin users

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
        ├── yurt-customer/   # Customer mobile-first web app
        ├── yurt-admin/      # Admin desktop-first web app
        ├── shared-models/   # TypeScript interfaces & enums
        ├── shared-api/      # API service, auth state, SignalR service
        └── shared-ui/       # UI library components and utilities
```

## Getting started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or newer
- SQL Server, SQL Server Express, or LocalDB
- [Node.js 18+](https://nodejs.org/)
- [Angular CLI 21](https://angular.dev/): `npm install -g @angular/cli@21`

### Backend setup

1. Configure the database in `backend/src/Yurt.WebApi/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=YurtDb;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Jwt": {
    "Secret": "CHANGE_THIS_TO_A_STRONG_SECRET_KEY_AT_LEAST_32_CHARS",
    "Issuer": "yurt-api",
    "Audience": "yurt-clients",
    "ExpiryDays": "7"
  },
  "AllowedOrigins": [
    "http://localhost:4200",
    "http://localhost:4300"
  ]
}
```

2. Restore packages and apply migrations:

```bash
cd backend
dotnet restore
dotnet ef database update --project src/Yurt.Infrastructure --startup-project src/Yurt.WebApi
```

3. Run the API:

```bash
dotnet run --project src/Yurt.WebApi
```

The API should be available at `http://localhost:5000` and Swagger at `http://localhost:5000/swagger`.

### Frontend setup

1. Install dependencies:

```bash
cd frontend
npm install
```

2. Run the customer app:

```bash
ng serve yurt-customer --port 4200
```

3. Run the admin app:

```bash
ng serve yurt-admin --port 4300
```

### Build production assets

```bash
# Backend
cd backend
dotnet publish src/Yurt.WebApi/Yurt.WebApi.csproj -c Release -o publish

# Frontend
cd frontend
ng build yurt-customer --configuration production
ng build yurt-admin --configuration production
```

## Default seeded accounts

### Admin accounts

| Username | Password    | Role   |
| -------- | ----------- | ------ |
| admin    | Admin@123!  | Admin  |
| worker1  | Worker@123! | Worker |

### Customer accounts

- Customers can register in the customer app using a mobile number and a 4-digit PIN.

## Additional documentation

- `docs/setup.md` — local setup instructions
- `docs/deployment-guide.md` — deployment guidance and hosting strategy
- `docs/production-deployment.md` — production deployment plan and budget guidance
- `docs/DEPLOYMENT.md` — existing deployment and conversion guide

## Tech Stack

| Layer     | Technology                                     |
| --------- | ---------------------------------------------- |
| Backend   | .NET 8, ASP.NET Core, Clean Architecture       |
| Database  | SQL Server, EF Core 8, Migrations              |
| Auth      | JWT Bearer, BCrypt, role-based policies        |
| Real-time | SignalR                                        |
| Frontend  | Angular 21, Tailwind CSS, Standalone components |
| Logging   | Serilog                                        |
| Docs      | Swagger / OpenAPI                              |

## Coming soon

These are logical next features for the project:

- Payment gateway integration (Stripe / PayPal / card payments)
- iOS native mobile support and App Store publishing
- Push notifications for customers and staff
- Delivery driver module and delivery order support
- Loyalty, rewards, and gift card workflows
- Multi-location inventory and time-slot scheduling
- Extended admin role management and permission controls
- Offline-capable PWA experience for customers
- Better analytics dashboards and sales reports

## Notes

- The admin frontend is designed for desktop use while the customer app is mobile-first.
- The backend seeds sample locations, menu items, and admin users automatically.
- The customer app must point to a reachable backend URL for real-time updates to work.
- Use HTTPS in production and secure the `Jwt:Secret` setting.
