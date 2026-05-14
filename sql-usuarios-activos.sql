ALTER TABLE "Usuarios"
ADD COLUMN IF NOT EXISTS "Activo" boolean NOT NULL DEFAULT true;

UPDATE "Usuarios"
SET "Activo" = true
WHERE "Activo" IS NULL;
