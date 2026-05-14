ALTER TABLE "Partidos"
ADD COLUMN IF NOT EXISTS "Estado" text NOT NULL DEFAULT 'Pendiente';

UPDATE "Partidos"
SET "Estado" = CASE
    WHEN "Finalizado" = TRUE THEN 'Finalizado'
    ELSE 'Pendiente'
END
WHERE "Estado" IS NULL OR "Estado" = '';
