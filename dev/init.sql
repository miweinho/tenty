-- Drop tables if exist for clean setup
DROP TABLE IF EXISTS DelimitationPoints;
DROP TABLE IF EXISTS Camps;

CREATE TABLE Camps (
    Id SERIAL PRIMARY KEY,
    campName VARCHAR(50) NOT NULL,
    LocationX DECIMAL(9,2) NOT NULL,  -- 7 Vorkomma + 2 Nachkomma
    LocationY DECIMAL(9,2) NOT NULL,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE DelimitationPoints (
    Id SERIAL PRIMARY KEY,
    CampId INTEGER NOT NULL,
    X DECIMAL(9,2) NOT NULL,
    Y DECIMAL(9,2) NOT NULL,
    PointOrder INTEGER NOT NULL,
    FOREIGN KEY (CampId) REFERENCES Camps(Id) ON DELETE CASCADE
);


-- Indices for performance
CREATE INDEX idx_camps_location ON Camps(LocationX, LocationY);
CREATE INDEX idx_delimitation_camp ON DelimitationPoints(CampId);
CREATE INDEX idx_delimitation_order ON DelimitationPoints(CampId, PointOrder);

-- Sample data
INSERT INTO Camps (LocationX, LocationY) VALUES
(2600000.00, 1200000.00),
(2700000.00, 1300000.00);

-- Sample delimitation points for first camp (forms a triangle)
INSERT INTO DelimitationPoints (CampId, X, Y, PointOrder) VALUES
(1, 2600000.00, 1200000.00, 1),
(1, 2600100.00, 1200100.00, 2),
(1, 2600100.00, 1200000.00, 3);

-- Sample delimitation points for second camp (forms a rectangle)
INSERT INTO DelimitationPoints (CampId, X, Y, PointOrder) VALUES
(2, 2700000.00, 1300000.00, 1),
(2, 2700200.00, 1300000.00, 2),
(2, 2700200.00, 1300200.00, 3),
(2, 2700000.00, 1300200.00, 4);
