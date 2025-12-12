# Tenty Frontend

Angular-based frontend for the Tenty camp planning application.

## Tech Stack

- **Angular 18+** (Standalone Components)
- **TypeScript**
- **OpenLayers** (Map rendering & interactions)
- **Angular Material** (UI Components)
- **RxJS** (Reactive State Management)

## Installation

```bash
npm install
```

## Development

```bash
npm start
```

The application runs on `http://localhost:4200/`.

API calls target `http://localhost:5000/api` in development. Ensure the backend is running via `dotnet run` and the database container is up (see `tenty/dev/docker-compose.yml`).

## Build

```bash
npm run build
```

Production Build:
```bash
npm run build -- --configuration production
```

## Project Structure

```
src/app/
├── components/
│   ├── auth/              # Authentication
│   ├── forms/             # Forms (Create/Show Camp)
│   ├── maps/              # Map Components
│   │   ├── camp-main-map-view/
│   │   ├── pick-location-map-view/
│   │   └── pick-zone-map-view/
│   └── pages/             # Pages & Layout
│       ├── overlay/       # App shell with navigation
│       ├── map/           # Base map component
│       └── mapview/       # Base map view logic
├── service/
│   ├── camps/             # Camp service (API communication)
│   ├── campMapHelperService/  # Map helper utilities
│   ├── drawingService/    # Drawing logic for features
│   └── httpService/       # HTTP client wrapper
└── models/                # TypeScript DTOs & Interfaces
```

## Key Components

### Maps
- **CampMainMapView**: Main map displaying camps, features & zones
- **PickLocationMapView**: Interactive map for selecting camp locations
- **PickZoneMapView**: Draw & edit camp zones (polygons)

### Forms
- **CreateCampForm**: Multi-step form for camp creation
- **ShowCampForm**: Display & edit existing camps

### Pages
- **Overlay**: App shell with drawer navigation & toolbar

## Services

### CampService
Central service layer for camp operations:
- `loadCampList()` / `refreshCampsList()`
- `createCamp()` / `updateCamp()` / `deleteCamp()`
- `loadCamp()` / `getCamp$()`
- Observable-based state management via `camps$`

### DrawingService
OpenLayers feature management:
- `addFeature()` / `clearAll()`
- `startDrawing()` / `stopDrawing()`
- `featureClick$` Observable for feature interactions

### CampMapHelperService
Helper functions for map rendering:
- `featureFromDto()`: DTO → OpenLayers Feature
- `createZoneFeaturesFromCamp()`: Render camp zones on map
- Style functions for camp elements

## Map Interactions

The application uses OpenLayers for:
- **Feature selection**: Click events on camp features
- **Polygon drawing**: Draw zones (delimitation)
- **Point placement**: Select camp location
- **Transform interaction** (planned): Rotate & scale icons

## Authentication

- JWT-based auth via `Auth` service
- Route guards protect secured areas
- Token storage in LocalStorage

### Test Users

Use these email addresses to test the application (seeded from `dev/init.sql`):

- `anna.mueller@example.com`
- `lukas.schmidt@example.com`
- `sophie.weber@example.com`
- `tim.fischer@example.com`
- `laura.becker@example.com`

## Environment Variables

```typescript
// src/environments/environment.ts
export const environment = {
  production: false,
  apiUrl: 'http://localhost:5000/api'
};
```

Alternatively, if using the Angular proxy, verify that `proxy.conf.json` routes `/api` to `http://localhost:5000`.

## Scripts

```json
{
  "start": "ng serve",
  "build": "ng build",
  "test": "ng test",
  "lint": "ng lint"
}
```

## Development Notes

- **Standalone Components**: All components use `standalone: true`
- **Reactive Forms**: FormGroup/FormControl for all forms
- **RxJS Observables**: Asynchronous data flows via services
- **Material Design**: Consistent UI via Angular Material
- **TypeScript strict mode**: Type safety at all levels

## Known Limitations

- Transform interaction (ol-ext) not yet implemented
- Asymmetric scaling of icons in planning
- Feature details not fully implemented yet
