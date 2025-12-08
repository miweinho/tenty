// Minimal GeoJSON DTO used by other DTOs
export interface GeoJsonGeometryDto {
  type: string;
  coordinates: any;
}

export interface GeoJsonFeatureDto {
  type: 'Feature';
  geometry: GeoJsonGeometryDto;
  properties?: Record<string, any>;
}

// Basic DTOs
export interface UserDto {
  id: number;
  firstName: string;
  middleName?: string | null;
  lastName: string;
  email: string;
}

export interface CampUserDto {
  user: UserDto;
  permission: number; // 0=Read,1=Write,2=Admin (matches DbUserPermission)
}

export interface CampZoneDto {
  name: string;
  isCampDelimitation: boolean;
  color: string; // hex string
  zone: GeoJsonFeatureDto;
}

export interface DetailListEntryDto {
  title: string;
  content: string;
}

export interface CampDetailDto {
  startDate: string; // ISO date string
  endDate: string;   // ISO date string
  description?: string | null;
  city: string;
  detailList?: DetailListEntryDto[] | null;
}

export interface CampDto {
    id: number;
    ownerId: number;
    campName: string;
    location: GeoJsonFeatureDto;
    isSubCamp: boolean;
    parentId: number | null;
    createdAt: string;
    updatedAt: string;
}

export interface FullCampDto {
  id: number;
  owner: UserDto;
  name: string;
  location: GeoJsonFeatureDto;
  userList: CampUserDto[];
  delimitation: CampZoneDto;
  campDetail: CampDetailDto;
  isSubCamp: boolean;
  subCamp: number[]; // child camp ids
  shownMaps: string[];
  campZones: CampZoneDto[];
}

export interface CreateCampDto {
  name: string;
  location: GeoJsonFeatureDto;
  delimitation: CampZoneDto;
  campDetail: CampDetailDto;
  isSubCamp: boolean;
  parentId: number;
  subCamp: number[];
  shownMaps?: string[] | null;
  campZones?: CampZoneDto[] | null;
}

// New / DB insert DTOs
export interface NewDbUserDto {
  firstName: string;
  middleName?: string | null;
  lastName: string;
  email: string;
}

export interface NewDbCampDto {
  ownerId: number;
  campName: string;
  location: string; // WKT or GeoJSON string depending on backend contract
  isSubCamp: boolean;
  parentId?: number | null;
}

export interface NewDbCampZoneDto {
  campId: number;
  name: string;
  isCampDelimitation: boolean;
  color?: string | null;
  zone: string; // GeoJSON/WKT string
}

export enum DbUserPermission {
  Read = 0,
  Write = 1,
  Admin = 2
}

export interface NewDbCampUserDto {
  userId: number;
  campId: number;
  userPermission: DbUserPermission;
}

export interface NewDbCampDetailDto {
  campId: number;
  startDate: string; // ISO
  endDate: string;   // ISO
  description?: string | null;
  city?: string | null;
}

export interface NewDbDetailListEntryDto {
  campDetailsId: number;
  title: string;
  content: string;
}

export interface NewDbShownMapsDto {
  campId: number;
  mapName: string;
}

export interface NewDbFullCampDto {
  campDto: NewDbCampDto;
  campZones: NewDbCampZoneDto[];
  campDetails: NewDbCampDetailDto;
  campDetailListEntries?: NewDbDetailListEntryDto[] | null;
}

// Auth / Token DTOs
export interface LoginDTO {
  mailAddress: string | null;
}

export interface TokenDTO {
  token: string;
}

export interface CampRoles {
  admin: number[]; // user ids
  write: number[];
  read: number[];
}

export interface TokenPair {
  accessToken: string;
  accessExpiresAt: string;  // ISO
  refreshToken: string;
  refreshExpiresAt: string; // ISO
}

export interface DbRefreshToken {
  token: string;
  userId: number;
  expiresAt: string; // ISO
}

export interface RefreshTokenDTO {
  token: string;
}