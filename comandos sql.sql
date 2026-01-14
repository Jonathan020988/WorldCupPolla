SELECT 
  p."Id",
  e1."Nombre" AS Local,
  e2."Nombre" AS Visitante,
  p."Fase",
  p."Finalizado"
FROM "Partidos" p
JOIN "Equipos" e1 ON e1."Id" = p."LocalId"
JOIN "Equipos" e2 ON e2."Id" = p."VisitanteId"
ORDER BY p."Id";

INSERT INTO "Partidos" ("Fecha","Fase","LocalId","VisitanteId","Finalizado") VALUES
(NOW(),'Grupos',46,47,false),
(NOW(),'Grupos',46,48,false),
(NOW(),'Grupos',46,49,false),
(NOW(),'Grupos',47,48,false),
(NOW(),'Grupos',47,49,false),
(NOW(),'Grupos',48,49,false);

SELECT column_name
FROM information_schema.columns
WHERE table_name = 'Equipos';


SELECT "PenalesLocal", "PenalesVisitante"
FROM "Partidos"
LIMIT 1;


ALTER TABLE "Partidos"
ADD COLUMN "PenalesLocal" integer;

ALTER TABLE "Partidos"
ADD COLUMN "PenalesVisitante" integer;


SELECT "Id", "Fase", "LocalId", "VisitanteId"
FROM "Partidos"
ORDER BY "Id";

SELECT id, fecha, fase
FROM "Partidos"
WHERE fecha > NOW() + INTERVAL '2 hours';

DELETE FROM "Partidos"
WHERE "Fase" IN (
  'Dieciseisavos',
  'Octavos',
  'Cuartos',
  'Semifinales',
  'Final',
  'TercerPuesto'
);

SELECT id, nombre FROM "Equipos" ORDER BY id;

SELECT * FROM "Partidos";
SELECT "GolesLocal", "GolesVisitante" FROM "Partidos";
SELECT "LocalId", "VisitanteId" FROM "Partidos";

----partidos con horarios t fases tabla----
SELECT "Id", "Fase", "LocalId", "VisitanteId", "Fecha"
FROM "Partidos"
ORDER BY "Id";

SELECT * FROM "Predicciones";


SELECT table_name
FROM information_schema.tables
WHERE table_schema = 'public';

SELECT * FROM predicciones;


SELECT table_name
FROM information_schema.tables
WHERE table_schema = 'public';

CREATE TABLE "Predicciones" (
    "Id" SERIAL PRIMARY KEY,

    "PollaId" INTEGER NOT NULL,
    "UsuarioId" INTEGER NOT NULL,
    "PartidoId" INTEGER NOT NULL,

    "GolesLocal" INTEGER,
    "GolesVisitante" INTEGER,

    "PrediceTiempoExtra" BOOLEAN NOT NULL DEFAULT FALSE,
    "PredicePenales" BOOLEAN NOT NULL DEFAULT FALSE,

    "PrediceClasificadoId" INTEGER,

    "PuntosTotales" INTEGER NOT NULL DEFAULT 0,
    "PuntosMarcador" INTEGER NOT NULL DEFAULT 0,
    "PuntosClasificacion" INTEGER NOT NULL DEFAULT 0,
    "PuntosPodio" INTEGER NOT NULL DEFAULT 0,

    "Bloqueada" BOOLEAN NOT NULL DEFAULT FALSE,
    "FechaCreacion" TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),

    CONSTRAINT "FK_Predicciones_Pollas"
        FOREIGN KEY ("PollaId") REFERENCES "Pollas" ("Id") ON DELETE CASCADE,

    CONSTRAINT "FK_Predicciones_Usuarios"
        FOREIGN KEY ("UsuarioId") REFERENCES "Usuarios" ("Id") ON DELETE CASCADE,

    CONSTRAINT "FK_Predicciones_Partidos"
        FOREIGN KEY ("PartidoId") REFERENCES "Partidos" ("Id") ON DELETE CASCADE
);

SELECT * FROM "Pollas";

---mostrar tablas----
SELECT table_name
FROM information_schema.tables
WHERE table_schema = 'public';

---- crear usuario en pollas-----
INSERT INTO "Usuarios" ("Nombre", "Email", "PasswordHash")
VALUES (
  'Admin',
  'admin@polla.com',
  'HASH_DE_PRUEBA'
);


----columnas de usuario----
SELECT column_name, is_nullable
FROM information_schema.columns
WHERE table_name = 'Usuarios';

-- trae los usuarios creados-----
SELECT * FROM "Usuarios";

---insertar la polla---
INSERT INTO "Pollas" (
  "Nombre",
  "CreadorId",
  "FechaCreacion",
  "PermitirEmpatesEnEliminatoria"
)
VALUES (
  'Polla Mundial 2026',
  1,
  NOW(),
  true
);

--tabla predicciones--
SELECT * FROM "Predicciones";



--------tablas por nombre----
SELECT column_name
FROM information_schema.columns
WHERE table_name = 'Pollas';


----resetear partido---------
UPDATE "Partidos"
SET "Finalizado" = false,
    "GolesLocal" = NULL,
    "GolesVisitante" = NULL
WHERE "Id" = 80;

-------------cambiar fecha del partido--------------
UPDATE "Partidos"
SET 
    "Fecha" = NOW() + INTERVAL '2 days',
    "Finalizado" = false,
    "GolesLocal" = NULL,
    "GolesVisitante" = NULL
WHERE "Id" = 80;

---ID PARTIDOS QUE EXISTEN-----
SELECT "Id", "Fecha", "Finalizado"
FROM "Partidos"
ORDER BY "Id";

----borra todos los partidos de la fse de grupos-----
DELETE FROM "Partidos"
WHERE "Fase" = 'Grupos';

--- muetra partidos----
SELECT COUNT(*) FROM "Partidos";

---- grupos id de equipos-----
SELECT "Id", "Nombre", "Grupo"
FROM "Equipos"
WHERE "Grupo" = 'A';

---- genera todos los partidos de tdos los grupos------
SELECT "Grupo", COUNT(*) 
FROM "Equipos"
GROUP BY "Grupo"
ORDER BY "Grupo";

------------------cruces de equipos---------
INSERT INTO "Partidos" ("Fecha", "Fase", "LocalId", "VisitanteId", "Finalizado")
SELECT
    NOW() + INTERVAL '10 days',
    'Grupos',
    e1."Id",
    e2."Id",
    false
FROM "Equipos" e1
JOIN "Equipos" e2
  ON e1."Grupo" = e2."Grupo"
 AND e1."Id" < e2."Id";

-----ver partios por grupo---------
SELECT e."Grupo", COUNT(*)
FROM "Partidos" p
JOIN "Equipos" e ON p."LocalId" = e."Id"
GROUP BY e."Grupo"
ORDER BY e."Grupo";



