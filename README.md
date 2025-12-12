# Tenty

A fullstack application for planning and managing summer camps with interactive map-based features.

## Overview

Tenty is a comprehensive camp planning solution that allows users to:
- Create and manage camps (main camps and sub-camps)
- Define camp zones and boundaries on interactive maps
- Place and manage camp features (tents, facilities, infrastructure)
- Collaborate with team members with role-based permissions
- Plan camp details, dates, and logistics

## Tech Stack

### Backend
- **F#** with ASP.NET Core
- **PostgreSQL** with PostGIS for spatial data
- **JWT** authentication
- RESTful API

### Frontend
- **Angular 20** (Standalone Components)
- **OpenLayers 10** for interactive mapping
- **Angular Material** for UI components
- **RxJS** for reactive state management

## Project Structure

```
tenty/
├── backend/
│   └── backend-tenty/         # F# ASP.NET Core API
│       ├── Controller/        # API endpoints
│       ├── Service/           # Business logic
│       ├── Model/             # Domain models & DTOs
│       ├── Database/          # Database access layer
│       └── README.md          # Backend documentation
├── frontend/
│   └── tenty-frontend/        # Angular application
│       ├── src/app/
│       │   ├── components/    # UI components
│       │   ├── service/       # Frontend services
│       │   └── models/        # TypeScript DTOs
│       └── README.md          # Frontend documentation
└── dev/
    ├── docker-compose.yml     # Local PostgreSQL + PostGIS
    └── init.sql               # Database schema & seed data
```

## Getting Started

### Prerequisites
- **Node.js** 18+ and npm
- **.NET 9 SDK**
- **Docker** (for local database)

### Quick Start

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd tenty
   ```

2. **Start the database**
   ```bash
   cd dev
   docker compose up -d
   ```

3. **Run the backend**
   ```bash
   cd backend/backend-tenty
   dotnet run
   ```
   Backend runs on `http://localhost:5000` (Swagger at `/swagger`)

4. **Run the frontend**
   ```bash
   cd frontend/tenty-frontend
   npm install
   npm start
   ```
   Frontend runs on `http://localhost:4200`

5. **Login with test users**
   Use any of these emails:
   - `anna.mueller@example.com`
   - `lukas.schmidt@example.com`
   - `sophie.weber@example.com`

## Documentation

- **[Backend Documentation](backend/backend-tenty/README.md)** - API endpoints, services, architecture
- **[Frontend Documentation](frontend/tenty-frontend/README.md)** - Components, services, development guide

## Features

### Implemented
- ✅ User authentication (JWT-based)
- ✅ Camp creation and management
- ✅ Interactive map with OpenLayers
- ✅ Zone drawing (polygons for camp boundaries)
- ✅ Location picking (camp center point)
- ✅ Multi-step camp creation form
- ✅ Camp details (dates, description, lists)
- ✅ User permissions and camp sharing

### In Progress
- 🚧 Feature placement (tents, facilities)
- 🚧 Transform interaction for icons (rotate/scale)
- 🚧 Feature service and API integration
- 🚧 Real-time collaboration

## Database Schema

The application uses PostgreSQL with PostGIS extension. Key tables:
- `users` - User accounts
- `camps` - Main and sub-camps
- `camp_zones` - Polygon zones within camps
- `features` - Camp elements (tents, facilities) with geometry
- `camp_details` - Additional camp information

See [`dev/init.sql`](dev/init.sql) for complete schema and seed data.

## Development

### Database Migrations
Database schema is managed through SQL migration scripts in `dev/init.sql`.

### API
Backend API documentation can be accessed at `http://localhost:5000/swagger` when running in development mode.

### Environment Configuration
- Backend: `backend/backend-tenty/appsettings.Development.json`
- Frontend: `frontend/tenty-frontend/src/environments/environment.ts`
