CREATE TABLE IF NOT EXISTS "PrediccionesPodio" (
    "Id" SERIAL PRIMARY KEY,
    "PollaId" INTEGER NOT NULL,
    "UsuarioId" INTEGER NOT NULL,
    "CampeonId" INTEGER NOT NULL,
    "SubcampeonId" INTEGER NOT NULL,
    "TerceroId" INTEGER NOT NULL,
    "Bloqueada" BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_PrediccionesPodio_Polla_Usuario"
ON "PrediccionesPodio" ("PollaId", "UsuarioId");

CREATE TABLE IF NOT EXISTS "PrediccionesTerceros" (
    "Id" SERIAL PRIMARY KEY,
    "PollaId" INTEGER NOT NULL,
    "UsuarioId" INTEGER NOT NULL,
    "Grupo" VARCHAR(5) NOT NULL
);

CREATE INDEX IF NOT EXISTS "IX_PrediccionesTerceros_Polla_Usuario"
ON "PrediccionesTerceros" ("PollaId", "UsuarioId");
