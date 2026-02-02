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



----------ver tabla equipos y datos completos y fechas------------

SELECT
    p."Id",
    el."Nombre" AS "Equipo Local",
    ev."Nombre" AS "Equipo Visitante",
    p."Fecha",
    p."Fase"
FROM "Partidos" p
JOIN "Equipos" el ON el."Id" = p."LocalId"
JOIN "Equipos" ev ON ev."Id" = p."VisitanteId"
ORDER BY p."Fecha";
-------------crear vista para solo llmarala con un join----------
CREATE VIEW vista_partidos_detalle AS
SELECT
    p."Id",
    el."Nombre" AS "EquipoLocal",
    ev."Nombre" AS "EquipoVisitante",
    p."FechaHora",
    p."Fase"
FROM "Partidos" p
JOIN "Equipos" el ON el."Id" = p."LocalId"
JOIN "Equipos" ev ON ev."Id" = p."VisitanteId";


----este join. ver arriba------
SELECT * FROM vista_partidos_detalle;

--------dif
SELECT id, nombre FROM "Equipos" ORDER BY id;
SELECT * FROM "Equipos";
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

SELECT * FROM "Partidos";

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
SELECT * FROM "Pollas";



--------tablas por nombre----
SELECT column_name
FROM information_schema.columns
WHERE table_name = 'Usuarios';


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

CREATE TABLE "PrediccionesGrupo" (
    "Id" SERIAL PRIMARY KEY,
    "PollaId" INTEGER NOT NULL,
    "UsuarioId" INTEGER NOT NULL,
    "Grupo" VARCHAR(5) NOT NULL,
    "PrimeroId" INTEGER NOT NULL,
    "SegundoId" INTEGER NOT NULL,
    "TerceroId" INTEGER,
    "Bloqueada" BOOLEAN NOT NULL DEFAULT FALSE
);



------PARTIOS FINALIZADOS-----
SELECT "Id", "Fecha", "Finalizado"
FROM "Partidos"
WHERE "Finalizado" = true;

-------finalizar partido------
UPDATE "Partidos"
SET 
  "GolesLocal" = 2,
  "GolesVisitante" = 1,
  "Finalizado" = true
WHERE "Id" = 190;

---estado del partido
SELECT "Id", "Finalizado"
FROM "Partidos"
WHERE "Id" = 190;

----predicciones partidos----
SELECT *
FROM "Predicciones"
WHERE "PartidoId" = 118;

Resumen FINAL de la polla =
Grupos + Dieciseisavos + Octavos + Cuartos + Semis + Final + Tercer puesto

INSERT INTO "Pollas" ("Nombre", "FechaCreacion")
VALUES ('Polla Mundial 2026', NOW());
-------------------predicciones---------------------
-----corregir fecha de partidos---
UPDATE "Partidos"
SET "Fecha" = NOW() + INTERVAL '2 days'
WHERE "Id" = 118;

---------Todos los partidos de grupos:---------
UPDATE "Partidos"
SET "Fecha" = NOW() + INTERVAL '5 days'
WHERE "Fase" = 'Grupos';

------Fecha exacta (manual):---------
UPDATE "Partidos"
SET "Fecha" = '2026-01-20 20:00:00'
WHERE "Id" = 118;

--------DESBLOQUEAR una predicción específica-------
UPDATE "Predicciones"
SET "Bloqueada" = false
WHERE "Id" = 9;

----O por partido:------
UPDATE "Predicciones"
SET "Bloqueada" = false
WHERE "PartidoId" = 118;

-------O por usuario:------------
UPDATE "Predicciones"
SET "Bloqueada" = false
WHERE "UsuarioId" = 1;

-----------------MARCAR / DESMARCAR un partido como finalizado---------

UPDATE "Partidos"
SET "Finalizado" = false
WHERE "Id" = 118;

-------------O todos:---------
UPDATE "Partidos"
SET "Finalizado" = false;

------------RESET TOTAL (DESARROLLO PURO)

---------Si quieres volver a cero sin borrar datos:


UPDATE "Predicciones"
SET
    "Bloqueada" = false,
    "PuntosTotales" = 0,
    "PuntosMarcador" = 0,
    "PuntosClasificacion" = 0,
    "PuntosPodio" = 0;

---------Y partidos:-----

UPDATE "Partidos"
SET
    "Finalizado" = false,
    "GolesLocal" = NULL,
    "GolesVisitante" = NULL,
    "Fecha" = NOW() + INTERVAL '7 days';

--------ELIMINAR PREDICCIONES (SI ALGO QUEDÓ RARO)

-------Por partido:

DELETE FROM "Predicciones"
WHERE "PartidoId" = 118;

----------Por usuario:

DELETE FROM "Predicciones"
WHERE "UsuarioId" = 1;

select from "Predicciones"


------ ver tablas----
SELECT table_name
FROM information_schema.tables
WHERE table_schema = 'public'
ORDER BY table_name;


------borrar usuarios---
DELETE FROM "Usuarios";

SELECT * FROM "Usuarios";

SELECT * FROM "Pollas" ORDER BY "Id" DESC;

SELECT id, email, nombre
FROM "Usuarios";

----este join. ver arriba------
SELECT * FROM vista_partidos_detalle;

--------------------modificar fechas de todos los partidos--------------
-- GRUPO A
UPDATE "Partidos" SET "Fecha" = '2026-06-24 20:00:00.200879' WHERE "Id" = 117;
UPDATE "Partidos" SET "Fecha" = '2026-06-18 20:00:00.200879' WHERE "Id" = 118;
UPDATE "Partidos" SET "Fecha" = '2026-06-11 14:00:00.200879' WHERE "Id" = 119;
UPDATE "Partidos" SET "Fecha" = '2026-06-18 11:00:00.200879' WHERE "Id" = 120;
UPDATE "Partidos" SET "Fecha" = '2026-06-24 20:00:00.200879' WHERE "Id" = 121;
UPDATE "Partidos" SET "Fecha" = '2026-06-11 21:00:00.200879' WHERE "Id" = 122;

-- GRUPO B
UPDATE "Partidos" SET "Fecha" = '2026-06-24 14:00:00.200879' WHERE "Id" = 123;
UPDATE "Partidos" SET "Fecha" = '2026-06-18 17:00:00.200879' WHERE "Id" = 124;
UPDATE "Partidos" SET "Fecha" = '2026-06-12 14:00:00.200879' WHERE "Id" = 125;
UPDATE "Partidos" SET "Fecha" = '2026-06-18 14:00:00.200879' WHERE "Id" = 126;
UPDATE "Partidos" SET "Fecha" = '2026-06-24 14:00:00.200879' WHERE "Id" = 127;
UPDATE "Partidos" SET "Fecha" = '2026-06-13 14:00:00.200879' WHERE "Id" = 128;

-- GRUPO C
UPDATE "Partidos" SET "Fecha" = '2026-06-24 17:00:00.200879' WHERE "Id" = 129;
UPDATE "Partidos" SET "Fecha" = '2026-06-19 20:00:00.200879' WHERE "Id" = 130;
UPDATE "Partidos" SET "Fecha" = '2026-06-13 17:00:00.200879' WHERE "Id" = 131;
UPDATE "Partidos" SET "Fecha" = '2026-06-19 17:00:00.200879' WHERE "Id" = 132;
UPDATE "Partidos" SET "Fecha" = '2026-06-24 17:00:00.200879' WHERE "Id" = 133;
UPDATE "Partidos" SET "Fecha" = '2026-06-13 20:00:00.200879' WHERE "Id" = 134;

-- GRUPO D
UPDATE "Partidos" SET "Fecha" = '2026-06-25 21:00:00.200879' WHERE "Id" = 135;
UPDATE "Partidos" SET "Fecha" = '2026-06-19 14:00:00.200879' WHERE "Id" = 136;
UPDATE "Partidos" SET "Fecha" = '2026-06-12 20:00:00.200879' WHERE "Id" = 137;
UPDATE "Partidos" SET "Fecha" = '2026-06-19 23:00:00.200879' WHERE "Id" = 138;
UPDATE "Partidos" SET "Fecha" = '2026-06-25 21:00:00.200879' WHERE "Id" = 139;
UPDATE "Partidos" SET "Fecha" = '2026-06-13 23:00:00.200879' WHERE "Id" = 140;

-- GRUPO E
UPDATE "Partidos" SET "Fecha" = '2026-06-25 15:00:00.200879' WHERE "Id" = 141;
UPDATE "Partidos" SET "Fecha" = '2026-06-20 15:00:00.200879' WHERE "Id" = 142;
UPDATE "Partidos" SET "Fecha" = '2026-06-14 12:00:00.200879' WHERE "Id" = 143;
UPDATE "Partidos" SET "Fecha" = '2026-06-20 19:00:00.200879' WHERE "Id" = 144;
UPDATE "Partidos" SET "Fecha" = '2026-06-25 15:00:00.200879' WHERE "Id" = 145;
UPDATE "Partidos" SET "Fecha" = '2026-06-14 18:00:00.200879' WHERE "Id" = 146;

-- GRUPO F
UPDATE "Partidos" SET "Fecha" = '2026-06-25 18:00:00.200879' WHERE "Id" = 147;
UPDATE "Partidos" SET "Fecha" = '2026-06-20 12:00:00.200879' WHERE "Id" = 148;
UPDATE "Partidos" SET "Fecha" = '2026-06-14 15:00:00.200879' WHERE "Id" = 149;
UPDATE "Partidos" SET "Fecha" = '2026-06-20 23:00:00.200879' WHERE "Id" = 150;
UPDATE "Partidos" SET "Fecha" = '2026-06-25 18:00:00.200879' WHERE "Id" = 151;
UPDATE "Partidos" SET "Fecha" = '2026-06-14 21:00:00.200879' WHERE "Id" = 152;

-- GRUPO G
UPDATE "Partidos" SET "Fecha" = '2026-06-26 22:00:00.200879' WHERE "Id" = 153;
UPDATE "Partidos" SET "Fecha" = '2026-06-21 14:00:00.200879' WHERE "Id" = 154;
UPDATE "Partidos" SET "Fecha" = '2026-06-15 14:00:00.200879' WHERE "Id" = 155;
UPDATE "Partidos" SET "Fecha" = '2026-06-21 20:00:00.200879' WHERE "Id" = 156;
UPDATE "Partidos" SET "Fecha" = '2026-06-26 22:00:00.200879' WHERE "Id" = 157;
UPDATE "Partidos" SET "Fecha" = '2026-06-15 20:00:00.200879' WHERE "Id" = 158;

-- GRUPO H
UPDATE "Partidos" SET "Fecha" = '2026-06-26 19:00:00.200879' WHERE "Id" = 159;
UPDATE "Partidos" SET "Fecha" = '2026-06-21 11:00:00.200879' WHERE "Id" = 160;
UPDATE "Partidos" SET "Fecha" = '2026-06-15 11:00:00.200879' WHERE "Id" = 161;
UPDATE "Partidos" SET "Fecha" = '2026-06-21 17:00:00.200879' WHERE "Id" = 162;
UPDATE "Partidos" SET "Fecha" = '2026-06-26 19:00:00.200879' WHERE "Id" = 163;
UPDATE "Partidos" SET "Fecha" = '2026-06-15 17:00:00.200879' WHERE "Id" = 164;

-- GRUPO I
UPDATE "Partidos" SET "Fecha" = '2026-06-26 14:00:00.200879' WHERE "Id" = 165;
UPDATE "Partidos" SET "Fecha" = '2026-06-22 16:00:00.200879' WHERE "Id" = 166;
UPDATE "Partidos" SET "Fecha" = '2026-06-16 14:00:00.200879' WHERE "Id" = 167;
UPDATE "Partidos" SET "Fecha" = '2026-06-22 19:00:00.200879' WHERE "Id" = 168;
UPDATE "Partidos" SET "Fecha" = '2026-06-26 14:00:00.200879' WHERE "Id" = 169;
UPDATE "Partidos" SET "Fecha" = '2026-06-16 17:00:00.200879' WHERE "Id" = 170;

-- GRUPO J
UPDATE "Partidos" SET "Fecha" = '2026-06-27 21:00:00.200879' WHERE "Id" = 171;
UPDATE "Partidos" SET "Fecha" = '2026-06-22 12:00:00.200879' WHERE "Id" = 172;
UPDATE "Partidos" SET "Fecha" = '2026-06-16 20:00:00.200879' WHERE "Id" = 173;
UPDATE "Partidos" SET "Fecha" = '2026-06-22 22:00:00.200879' WHERE "Id" = 174;
UPDATE "Partidos" SET "Fecha" = '2026-06-27 21:00:00.200879' WHERE "Id" = 175;
UPDATE "Partidos" SET "Fecha" = '2026-06-16 23:00:00.200879' WHERE "Id" = 176;

-- GRUPO K (Colombia)
UPDATE "Partidos" SET "Fecha" = '2026-06-27 18:30:00.200879' WHERE "Id" = 177;
UPDATE "Partidos" SET "Fecha" = '2026-06-23 12:00:00.200879' WHERE "Id" = 178;
UPDATE "Partidos" SET "Fecha" = '2026-06-17 12:00:00.200879' WHERE "Id" = 179;
UPDATE "Partidos" SET "Fecha" = '2026-06-23 21:00:00.200879' WHERE "Id" = 180;
UPDATE "Partidos" SET "Fecha" = '2026-06-27 18:30:00.200879' WHERE "Id" = 181;
UPDATE "Partidos" SET "Fecha" = '2026-06-17 21:00:00.200879' WHERE "Id" = 182;

-- GRUPO L
UPDATE "Partidos" SET "Fecha" = '2026-06-27 16:00:00.200879' WHERE "Id" = 183;
UPDATE "Partidos" SET "Fecha" = '2026-06-23 15:00:00.200879' WHERE "Id" = 184;
UPDATE "Partidos" SET "Fecha" = '2026-06-17 15:00:00.200879' WHERE "Id" = 185;
UPDATE "Partidos" SET "Fecha" = '2026-06-23 18:00:00.200879' WHERE "Id" = 186;
UPDATE "Partidos" SET "Fecha" = '2026-06-27 16:00:00.200879' WHERE "Id" = 187;
UPDATE "Partidos" SET "Fecha" = '2026-06-17 18:00:00.200879' WHERE "Id" = 188;

