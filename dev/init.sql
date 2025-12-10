\set ON_ERROR_STOP ON
BEGIN;

-- Drop existing schema and recreate
DROP SCHEMA IF EXISTS tenty CASCADE;
CREATE SCHEMA tenty;
SET search_path = tenty, public;

-- Ensure PostGIS extension is available for geometry types
CREATE EXTENSION IF NOT EXISTS postgis;

-- Define enum type for the different feature kinds.  Extendable via ALTER TYPE if more types are needed later.
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname='feature_kind') THEN
    CREATE TYPE feature_kind AS ENUM ('tent','toilet','power','water','gate','custom');
  END IF;
END$$;

-- -----------------------------------------------------------------------------
-- Users: store account information. Using IDENTITY for simple incremental keys.
-- Timestamp columns default to now() and are kept fresh via triggers defined later.
CREATE TABLE users (
    id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    first_name VARCHAR(50) NOT NULL,
    middle_name VARCHAR(50),
    last_name VARCHAR(50) NOT NULL,
    email VARCHAR(254) NOT NULL UNIQUE,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
);

-- Refresh tokens for session management
CREATE TABLE refresh_tokens (
  id BIGSERIAL PRIMARY KEY,
  user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  token_hash TEXT NOT NULL,
  expires_at TIMESTAMPTZ NOT NULL,
  created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
);

-- Camps: top‑level and nested camps.  loc stores the camp centre as a point in WGS84.
CREATE TABLE camps (
    id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    owner INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    camp_name VARCHAR(50) NOT NULL,
    loc geometry(Point,4326) NOT NULL,
    is_sub_camp BOOLEAN NOT NULL DEFAULT FALSE,
    parent_id INTEGER NULL REFERENCES camps(id) ON DELETE CASCADE,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_camp_owner_name UNIQUE (owner, camp_name),
    CONSTRAINT camps_sub_flag_consistency CHECK ((parent_id IS NULL AND is_sub_camp = FALSE) OR (parent_id IS NOT NULL AND is_sub_camp = TRUE)),
    CHECK (parent_id IS NULL OR parent_id != id)
);

-- Mapping users to camps with permissions
CREATE TABLE camp_users (
    user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    camp_id INTEGER NOT NULL REFERENCES camps(id) ON DELETE CASCADE,
    user_permission SMALLINT NOT NULL,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (user_id, camp_id)
);

-- Additional details per camp (one row per camp)
CREATE TABLE camp_details (
    id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    camp_id INTEGER NOT NULL REFERENCES camps(id) ON DELETE CASCADE,
    start_date TIMESTAMPTZ NOT NULL,
    end_date TIMESTAMPTZ NOT NULL,
    description TEXT,
    city VARCHAR(50),
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_camp_details UNIQUE(camp_id)
);

-- Free‑form detail entries per camp
CREATE TABLE detail_list_entries (
    id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    camp_details_id INTEGER NOT NULL REFERENCES camp_details(id) ON DELETE CASCADE,
    title VARCHAR(50) NOT NULL,
    content TEXT NOT NULL,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
);

-- Zones within a camp delineated by polygons
CREATE TABLE camp_zones (
    id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    camp_id INTEGER NOT NULL REFERENCES camps(id) ON DELETE CASCADE,
    name VARCHAR(50) NOT NULL,
    is_camp_delimitation BOOLEAN NOT NULL DEFAULT FALSE,
    color VARCHAR(20),
    geom geometry(Polygon, 4326),
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_camp_zone_name UNIQUE (camp_id, name),
    CONSTRAINT camp_zones_geom_valid CHECK(geom IS NULL OR ST_IsValid(geom))
);

-- Named base layers or maps for a camp
CREATE TABLE shown_maps (
    id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    camp_id INTEGER NOT NULL REFERENCES camps(id) ON DELETE CASCADE,
    map_name VARCHAR(50) NOT NULL,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
);

-- Features table: stores geometry plus structured properties for camp elements
CREATE TABLE features (
    id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    camp_id INTEGER NOT NULL REFERENCES camps(id) ON DELETE CASCADE,
    kind feature_kind NOT NULL,
    name VARCHAR(50),
    geom geometry(Geometry, 4326) NOT NULL,
    -- rotation/heading of the feature in degrees 0–360
    heading_deg DOUBLE PRECISION CHECK (heading_deg >= 0 AND heading_deg < 360),
    -- common dimensions in metres for rectangular objects; NULL for points/lines
    length_m DOUBLE PRECISION CHECK (length_m IS NULL OR length_m > 0),
    width_m  DOUBLE PRECISION CHECK (width_m  IS NULL OR width_m  > 0),
    -- optional styling
    color VARCHAR(20),
    icon VARCHAR(50),
    -- additional arbitrary properties
    props JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CHECK (GeometryType(geom) IN ('POINT','LINESTRING','POLYGON'))
);

-- Additional free‑form detail entries per feature (similar to camp detail_list_entries)
CREATE TABLE feature_details (
    id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    feature_id INTEGER NOT NULL REFERENCES features(id) ON DELETE CASCADE,
    title VARCHAR(50) NOT NULL,
    content TEXT NOT NULL,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
);

-- Trigger function to keep updated_at fresh on update
CREATE OR REPLACE FUNCTION update_updated_at()
RETURNS trigger AS $$
BEGIN
  NEW.updated_at := CURRENT_TIMESTAMP;
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Attach the trigger to all tables in the tenty schema having an updated_at column
DO $$
DECLARE
  tbl text;
BEGIN
  FOR tbl IN SELECT tablename FROM pg_tables WHERE schemaname = 'tenty'
  LOOP
    BEGIN
      EXECUTE format('CREATE TRIGGER %I_updated_at BEFORE UPDATE ON tenty.%I FOR EACH ROW EXECUTE FUNCTION tenty.update_updated_at();', tbl, tbl);
    EXCEPTION WHEN duplicate_object THEN
      -- if trigger already exists, ignore
      NULL;
    END;
  END LOOP;
END
$$;

-- Create spatial indexes to speed up spatial queries on features and zones
CREATE INDEX IF NOT EXISTS idx_features_geom ON features USING GIST (geom);
CREATE INDEX IF NOT EXISTS idx_camp_zones_geom ON camp_zones USING GIST (geom);

-- -----------------------------------------------------------------------------
-- Seed data
-- Example users
INSERT INTO users (first_name, middle_name, last_name, email) VALUES
  ('Anna', 'Maria', 'Müller', 'anna.mueller@example.com'),
  ('Lukas', NULL, 'Schmidt', 'lukas.schmidt@example.com'),
  ('Sophie', 'Christine', 'Weber', 'sophie.weber@example.com'),
  ('Tim', NULL, 'Fischer', 'tim.fischer@example.com'),
  ('Laura', NULL, 'Becker', 'laura.becker@example.com');

-- Example camps
INSERT INTO camps (owner, camp_name, loc, is_sub_camp, parent_id)
VALUES
  (1, 'Hauptlager', ST_GeomFromText('POINT(7.438637222 46.951081111)',4326), FALSE, NULL);

INSERT INTO camps (owner, camp_name, loc, is_sub_camp, parent_id)
VALUES
  (2, 'Nebenlager Nord', ST_GeomFromText('POINT(7.439950828 46.951530871)',4326), TRUE, (SELECT id FROM camps WHERE camp_name='Hauptlager')),
  (3, 'Bergcamp', ST_GeomFromText('POINT(7.445205253 46.953990000)',4326), FALSE, NULL);

-- Associate users with camps
INSERT INTO camp_users (user_id, camp_id, user_permission)
VALUES
  (1, (SELECT id FROM camps WHERE camp_name='Hauptlager'), 2),
  (2, (SELECT id FROM camps WHERE camp_name='Hauptlager'), 1),
  (4, (SELECT id FROM camps WHERE camp_name='Nebenlager Nord'), 2);

-- Camp details
INSERT INTO camp_details (camp_id, start_date, end_date, description, city)
VALUES
  ((SELECT id FROM camps WHERE camp_name='Hauptlager'), '2026-07-10 09:00+02'::timestamptz, '2026-07-17 18:00+02'::timestamptz, 'Sommerzeltlager Hauptlager', 'Bern'),
  ((SELECT id FROM camps WHERE camp_name='Nebenlager Nord'), '2026-07-11 10:00+02'::timestamptz, '2026-07-15 16:00+02'::timestamptz, 'Kleines Nebencamp', 'Thun');

-- Detail list entries per camp
INSERT INTO detail_list_entries (camp_details_id, title, content)
VALUES
  ((SELECT id FROM camp_details WHERE camp_id = (SELECT id FROM camps WHERE camp_name='Hauptlager') LIMIT 1), 'Packliste', 'Zelt, Schlafsack, Wechselkleidung'),
  ((SELECT id FROM camp_details WHERE camp_id = (SELECT id FROM camps WHERE camp_name='Hauptlager') LIMIT 1), 'Ablauf', 'Begrüßung, Lagerfeuer, Spiele'),
  ((SELECT id FROM camp_details WHERE camp_id = (SELECT id FROM camps WHERE camp_name='Nebenlager Nord') LIMIT 1), 'Regeln', 'Nachtruhe ab 23 Uhr');

-- Zones for camps
INSERT INTO camp_zones (camp_id, name, is_camp_delimitation, color, geom)
VALUES
  ((SELECT id FROM camps WHERE camp_name='Hauptlager'),
   'Zeltwiese A', TRUE, '#A3D39C',
   ST_GeomFromText('POLYGON((7.438637222 46.951081111, 7.439294025 46.951081111, 7.439294025 46.951530871, 7.438637222 46.951530871, 7.438637222 46.951081111))',4326)
  ),
  ((SELECT id FROM camps WHERE camp_name='Bergcamp'),
   'Lagergrenze', TRUE, '#FF0000',
   ST_GeomFromText('POLYGON((7.438637222 46.951081111, 7.441264434 46.951081111, 7.441264434 46.953435, 7.438637222 46.953435, 7.438637222 46.951081111))',4326)
  ),
  ((SELECT id FROM camps WHERE camp_name='Nebenlager Nord'),
   'Zeltplatz Nord', TRUE, '#CCCCFF',
   ST_GeomFromText('POLYGON((7.439950828 46.951530871, 7.440607631 46.951530871, 7.440607631 46.951979631, 7.439950828 46.951979631, 7.439950828 46.951530871))',4326)
  );

-- Named maps for camps
INSERT INTO shown_maps (camp_id, map_name)
VALUES
  ((SELECT id FROM camps WHERE camp_name='Hauptlager'), 'Übersichtskarte'),
  ((SELECT id FROM camps WHERE camp_name='Hauptlager'), 'Notfallplan'),
  ((SELECT id FROM camps WHERE camp_name='Nebenlager Nord'), 'Geländeplan');

-- Example features
-- 1) Tent polygon with length/width and heading
INSERT INTO features (camp_id, kind, name, geom, heading_deg, length_m, width_m, color, icon, props)
VALUES
  ((SELECT id FROM camps WHERE camp_name='Hauptlager'), 'tent', 'Zelt Alpha',
   ST_GeomFromText('POLYGON((7.4388 46.9512, 7.4389 46.9512, 7.4389 46.9513, 7.4388 46.9513, 7.4388 46.9512))', 4326),
   90.0, 8.0, 5.0, '#00FF00', 'tent-a', '{"capacity": 6}'::jsonb),
  -- 2) Water line represented as a linestring
  ((SELECT id FROM camps WHERE camp_name='Hauptlager'), 'water', 'Wasserleitung 1',
   ST_GeomFromText('LINESTRING(7.4387 46.9510, 7.4393 46.9510)', 4326),
   NULL, NULL, NULL, '#0000FF', 'pipe', '{"diameter_mm": 50}'::jsonb),
  -- 3) Gate as point
  ((SELECT id FROM camps WHERE camp_name='Nebenlager Nord'), 'gate', 'Haupteingang',
   ST_GeomFromText('POINT(7.43996 46.95154)', 4326),
   270.0, NULL, NULL, '#FFA500', 'gate', NULL);

-- Feature details examples
INSERT INTO feature_details (feature_id, title, content)
VALUES
  ((SELECT id FROM features WHERE name='Zelt Alpha' LIMIT 1), 'Hinweis', 'Dieses Zelt ist für die Lagerleitung reserviert.'),
  ((SELECT id FROM features WHERE name='Haupteingang' LIMIT 1), 'Öffnungszeiten', 'Der Haupteingang ist zwischen 07:00 und 22:00 Uhr geöffnet.');

COMMIT;