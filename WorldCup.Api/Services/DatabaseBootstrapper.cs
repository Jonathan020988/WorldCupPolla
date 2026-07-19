using Microsoft.EntityFrameworkCore;
using WorldCup.Api.Data;

namespace WorldCup.Api.Services;

public static class DatabaseBootstrapper
{
    public static async Task AplicarAjustesCompatibilidadAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "Usuarios"
            ADD COLUMN IF NOT EXISTS "Activo" boolean NOT NULL DEFAULT true;

            UPDATE "Usuarios"
            SET "Activo" = true
            WHERE "Activo" IS NULL;

            ALTER TABLE "Usuarios"
            ADD COLUMN IF NOT EXISTS "EmailConfirmado" boolean NOT NULL DEFAULT true;

            ALTER TABLE "Usuarios"
            ADD COLUMN IF NOT EXISTS "EmailConfirmadoEn" timestamp with time zone NULL;

            ALTER TABLE "Usuarios"
            ADD COLUMN IF NOT EXISTS "MaximoMiembrosPorPolla" integer NOT NULL DEFAULT 5;

            ALTER TABLE "Usuarios"
            ADD COLUMN IF NOT EXISTS "CuposIlimitados" boolean NOT NULL DEFAULT false;

            ALTER TABLE "Usuarios"
            ADD COLUMN IF NOT EXISTS "AceptaTerminos" boolean NOT NULL DEFAULT false;

            ALTER TABLE "Usuarios"
            ADD COLUMN IF NOT EXISTS "AceptaPoliticaPrivacidad" boolean NOT NULL DEFAULT false;

            ALTER TABLE "Usuarios"
            ADD COLUMN IF NOT EXISTS "AceptaTratamientoDatos" boolean NOT NULL DEFAULT false;

            ALTER TABLE "Usuarios"
            ADD COLUMN IF NOT EXISTS "VersionLegalAceptada" text NULL;

            ALTER TABLE "Usuarios"
            ADD COLUMN IF NOT EXISTS "LegalAceptadoEn" timestamp with time zone NULL;

            ALTER TABLE "Usuarios"
            ADD COLUMN IF NOT EXISTS "LegalAceptadoIp" text NULL;

            ALTER TABLE "Usuarios"
            ADD COLUMN IF NOT EXISTS "LegalAceptadoUserAgent" text NULL;

            UPDATE "Usuarios"
            SET "CuposIlimitados" = true,
                "MaximoMiembrosPorPolla" = 100000
            WHERE lower(trim("Nombre")) = lower(trim('jonathan ramirez ocampo'))
               OR lower(trim("Email")) = lower(trim('monolin020988@gmail.com'));

            ALTER TABLE "Pollas"
            ADD COLUMN IF NOT EXISTS "ValorInscripcion" numeric(12,2) NULL;

            ALTER TABLE "Pollas"
            ADD COLUMN IF NOT EXISTS "MetodoPago" text NULL;

            ALTER TABLE "Pollas"
            ADD COLUMN IF NOT EXISTS "PremioPrimerLugar" numeric(12,2) NULL;

            ALTER TABLE "Pollas"
            ADD COLUMN IF NOT EXISTS "PremioSegundoLugar" numeric(12,2) NULL;

            ALTER TABLE "Pollas"
            ADD COLUMN IF NOT EXISTS "PremioTercerLugar" numeric(12,2) NULL;

            ALTER TABLE "Pollas"
            ADD COLUMN IF NOT EXISTS "InscripcionesAbiertas" boolean NOT NULL DEFAULT true;

            ALTER TABLE "PollaMiembros"
            ADD COLUMN IF NOT EXISTS "ValorAPagar" numeric(12,2) NULL;

            ALTER TABLE "PollaMiembros"
            ADD COLUMN IF NOT EXISTS "AbonoPagado" numeric(12,2) NOT NULL DEFAULT 0;

            ALTER TABLE "PollaMiembros"
            ADD COLUMN IF NOT EXISTS "NotaPago" text NULL;

            ALTER TABLE "PollaMiembros"
            ADD COLUMN IF NOT EXISTS "ObservacionAdmin" text NULL;

            ALTER TABLE "PollaMiembros"
            ADD COLUMN IF NOT EXISTS "PagoActualizadoEn" timestamp with time zone NULL;

            ALTER TABLE "PollaMiembros"
            ADD COLUMN IF NOT EXISTS "PagoNotificadoEn" timestamp with time zone NULL;

            ALTER TABLE "Partidos"
            ADD COLUMN IF NOT EXISTS "Estado" text NOT NULL DEFAULT 'Pendiente';

            ALTER TABLE "Partidos"
            ADD COLUMN IF NOT EXISTS "PenalesLocal" integer NULL;

            ALTER TABLE "Partidos"
            ADD COLUMN IF NOT EXISTS "PenalesVisitante" integer NULL;

            ALTER TABLE "Partidos"
            ADD COLUMN IF NOT EXISTS "TiempoExtra" boolean NOT NULL DEFAULT false;

            ALTER TABLE "Partidos"
            ADD COLUMN IF NOT EXISTS "GolesExtraLocal" integer NULL;

            ALTER TABLE "Partidos"
            ADD COLUMN IF NOT EXISTS "GolesExtraVisitante" integer NULL;

            ALTER TABLE "Partidos"
            ADD COLUMN IF NOT EXISTS "ClasificadoId" integer NULL;

            ALTER TABLE "Partidos"
            ADD COLUMN IF NOT EXISTS "NumeroPartidoFifa" integer NULL;

            ALTER TABLE "Partidos"
            ADD COLUMN IF NOT EXISTS "MarcadorEnVivoLocal" integer NULL;

            ALTER TABLE "Partidos"
            ADD COLUMN IF NOT EXISTS "MarcadorEnVivoVisitante" integer NULL;

            ALTER TABLE "Partidos"
            ADD COLUMN IF NOT EXISTS "EstadoMarcadorEnVivo" text NULL;

            ALTER TABLE "Partidos"
            ADD COLUMN IF NOT EXISTS "MinutoMarcadorEnVivo" text NULL;

            ALTER TABLE "Partidos"
            ADD COLUMN IF NOT EXISTS "MarcadorEnVivoActualizadoEn" timestamp with time zone NULL;

            ALTER TABLE "Partidos"
            ADD COLUMN IF NOT EXISTS "FuenteMarcadorEnVivo" text NULL;

            ALTER TABLE "Partidos"
            ADD COLUMN IF NOT EXISTS "IdExternoMarcadorEnVivo" text NULL;

            UPDATE "Partidos"
            SET "NumeroPartidoFifa" = "Id"
            WHERE "NumeroPartidoFifa" IS NULL
              AND "Id" BETWEEN 1 AND 104;

            CREATE INDEX IF NOT EXISTS "IX_Partidos_NumeroPartidoFifa"
            ON "Partidos" ("NumeroPartidoFifa");

            UPDATE "Partidos"
            SET "Estado" = CASE
                WHEN "Finalizado" = true THEN 'Finalizado'
                ELSE 'Pendiente'
            END
            WHERE "Estado" IS NULL OR "Estado" = '';

            ALTER TABLE "Predicciones"
            ADD COLUMN IF NOT EXISTS "PrediceTiempoExtra" boolean NOT NULL DEFAULT false;

            ALTER TABLE "Predicciones"
            ADD COLUMN IF NOT EXISTS "PredicePenales" boolean NOT NULL DEFAULT false;

            ALTER TABLE "Predicciones"
            ADD COLUMN IF NOT EXISTS "PrediceClasificadoId" integer NULL;

            ALTER TABLE "Predicciones"
            ADD COLUMN IF NOT EXISTS "PuntosTotales" integer NOT NULL DEFAULT 0;

            ALTER TABLE "Predicciones"
            ADD COLUMN IF NOT EXISTS "PuntosMarcador" integer NOT NULL DEFAULT 0;

            ALTER TABLE "Predicciones"
            ADD COLUMN IF NOT EXISTS "PuntosClasificacion" integer NOT NULL DEFAULT 0;

            ALTER TABLE "Predicciones"
            ADD COLUMN IF NOT EXISTS "PuntosPodio" integer NOT NULL DEFAULT 0;

            ALTER TABLE "Predicciones"
            ADD COLUMN IF NOT EXISTS "Grupo" text NULL;

            ALTER TABLE "Predicciones"
            ADD COLUMN IF NOT EXISTS "PrediceSegundoId" integer NULL;

            ALTER TABLE "Predicciones"
            ADD COLUMN IF NOT EXISTS "Bloqueada" boolean NOT NULL DEFAULT false;

            ALTER TABLE "Predicciones"
            ADD COLUMN IF NOT EXISTS "FechaCreacion" timestamp with time zone NOT NULL DEFAULT now();

            CREATE TABLE IF NOT EXISTS "PasswordResetTokens" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "UsuarioId" integer NOT NULL REFERENCES "Usuarios"("Id") ON DELETE CASCADE,
                "TokenHash" text NOT NULL,
                "ExpiraEn" timestamp with time zone NOT NULL,
                "Usado" boolean NOT NULL DEFAULT false,
                "CreadoEn" timestamp with time zone NOT NULL DEFAULT now()
            );

            CREATE INDEX IF NOT EXISTS "IX_PasswordResetTokens_TokenHash"
            ON "PasswordResetTokens" ("TokenHash");

            CREATE TABLE IF NOT EXISTS "EmailVerificationTokens" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "UsuarioId" integer NOT NULL REFERENCES "Usuarios"("Id") ON DELETE CASCADE,
                "TokenHash" text NOT NULL,
                "ExpiraEn" timestamp with time zone NOT NULL,
                "CreadoEn" timestamp with time zone NOT NULL DEFAULT now(),
                "Usado" boolean NOT NULL DEFAULT false
            );

            CREATE INDEX IF NOT EXISTS "IX_EmailVerificationTokens_TokenHash"
            ON "EmailVerificationTokens" ("TokenHash");

            CREATE TABLE IF NOT EXISTS "PollaInvitaciones" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "PollaId" integer NOT NULL REFERENCES "Pollas"("Id") ON DELETE CASCADE,
                "RemitenteId" integer NOT NULL REFERENCES "Usuarios"("Id") ON DELETE RESTRICT,
                "EmailInvitado" text NOT NULL,
                "Estado" text NOT NULL DEFAULT 'Pendiente',
                "FechaEnvio" timestamp with time zone NOT NULL DEFAULT now(),
                "UsuarioAceptadoId" integer NULL REFERENCES "Usuarios"("Id") ON DELETE SET NULL
            );

            CREATE INDEX IF NOT EXISTS "IX_PollaInvitaciones_PollaId"
            ON "PollaInvitaciones" ("PollaId");

            CREATE INDEX IF NOT EXISTS "IX_PollaInvitaciones_EmailInvitado"
            ON "PollaInvitaciones" ("EmailInvitado");

            DELETE FROM "PollaMiembros" duplicado
            USING "PollaMiembros" conservar
            WHERE duplicado."PollaId" = conservar."PollaId"
              AND duplicado."UsuarioId" = conservar."UsuarioId"
              AND duplicado."Id" > conservar."Id";

            CREATE UNIQUE INDEX IF NOT EXISTS "UX_Usuarios_Email_Normalizado"
            ON "Usuarios" (lower(trim("Email")));

            CREATE UNIQUE INDEX IF NOT EXISTS "UX_Usuarios_Nombre_Normalizado"
            ON "Usuarios" (lower(trim("Nombre")));

            CREATE UNIQUE INDEX IF NOT EXISTS "UX_PollaMiembros_Polla_Usuario"
            ON "PollaMiembros" ("PollaId", "UsuarioId");

            CREATE TABLE IF NOT EXISTS "PrediccionesPodio" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "PollaId" integer NOT NULL,
                "UsuarioId" integer NOT NULL,
                "CampeonId" integer NOT NULL,
                "SubcampeonId" integer NOT NULL,
                "TerceroId" integer NOT NULL,
                "Bloqueada" boolean NOT NULL DEFAULT false
            );

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_PrediccionesPodio_Polla_Usuario"
            ON "PrediccionesPodio" ("PollaId", "UsuarioId");

            CREATE TABLE IF NOT EXISTS "PrediccionesTerceros" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "PollaId" integer NOT NULL,
                "UsuarioId" integer NOT NULL,
                "Grupo" varchar(5) NOT NULL
            );

            CREATE INDEX IF NOT EXISTS "IX_PrediccionesTerceros_Polla_Usuario"
            ON "PrediccionesTerceros" ("PollaId", "UsuarioId");

            CREATE TABLE IF NOT EXISTS "AdminReaperturasPrediccion" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "PollaId" integer NOT NULL REFERENCES "Pollas"("Id") ON DELETE CASCADE,
                "UsuarioId" integer NOT NULL REFERENCES "Usuarios"("Id") ON DELETE CASCADE,
                "PartidoId" integer NULL,
                "Fase" text NOT NULL,
                "Tipo" text NOT NULL,
                "Activa" boolean NOT NULL DEFAULT true,
                "AdminUsuarioId" integer NOT NULL REFERENCES "Usuarios"("Id") ON DELETE RESTRICT,
                "FechaCreacion" timestamp with time zone NOT NULL DEFAULT now(),
                "FechaActualizacion" timestamp with time zone NOT NULL DEFAULT now()
            );

            ALTER TABLE "AdminReaperturasPrediccion"
            ADD COLUMN IF NOT EXISTS "PartidoId" integer NULL;

            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1
                    FROM pg_constraint
                    WHERE conname = 'FK_AdminReaperturasPrediccion_Partidos_PartidoId'
                ) THEN
                    ALTER TABLE "AdminReaperturasPrediccion"
                    ADD CONSTRAINT "FK_AdminReaperturasPrediccion_Partidos_PartidoId"
                    FOREIGN KEY ("PartidoId") REFERENCES "Partidos"("Id") ON DELETE CASCADE;
                END IF;
            END $$;

            DROP INDEX IF EXISTS "UX_AdminReaperturasPrediccion_PollaUsuarioFaseTipo";

            CREATE UNIQUE INDEX IF NOT EXISTS "UX_AdminReaperturasPrediccion_Fase"
            ON "AdminReaperturasPrediccion" ("PollaId", "UsuarioId", "Fase", "Tipo")
            WHERE "PartidoId" IS NULL;

            CREATE UNIQUE INDEX IF NOT EXISTS "UX_AdminReaperturasPrediccion_Partido"
            ON "AdminReaperturasPrediccion" ("PollaId", "UsuarioId", "Fase", "Tipo", "PartidoId")
            WHERE "PartidoId" IS NOT NULL;

            CREATE INDEX IF NOT EXISTS "IX_AdminReaperturasPrediccion_Activas"
            ON "AdminReaperturasPrediccion" ("PollaId", "UsuarioId", "Activa");

            CREATE TABLE IF NOT EXISTS "SolicitudesAmpliacionCupos" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "UsuarioId" integer NOT NULL REFERENCES "Usuarios"("Id") ON DELETE CASCADE,
                "Celular" text NOT NULL,
                "CantidadUsuariosSolicitada" integer NOT NULL,
                "PlanNombre" text NOT NULL,
                "ValorPlan" numeric(12,2) NOT NULL,
                "Estado" text NOT NULL DEFAULT 'Pendiente',
                "CodigoHabilitacion" text NULL,
                "MaximoMiembrosAutorizado" integer NULL,
                "FechaSolicitud" timestamp with time zone NOT NULL DEFAULT now(),
                "FechaCodigo" timestamp with time zone NULL,
                "FechaActivacion" timestamp with time zone NULL,
                "AdminUsuarioId" integer NULL REFERENCES "Usuarios"("Id") ON DELETE RESTRICT
            );

            CREATE INDEX IF NOT EXISTS "IX_SolicitudesAmpliacionCupos_Usuario"
            ON "SolicitudesAmpliacionCupos" ("UsuarioId");

            CREATE UNIQUE INDEX IF NOT EXISTS "UX_SolicitudesAmpliacionCupos_Codigo"
            ON "SolicitudesAmpliacionCupos" ("CodigoHabilitacion");

            CREATE INDEX IF NOT EXISTS "IX_SolicitudesAmpliacionCupos_Estado"
            ON "SolicitudesAmpliacionCupos" ("Estado", "FechaSolicitud");

            CREATE TABLE IF NOT EXISTS "AlertasUsuario" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "UsuarioId" integer NOT NULL REFERENCES "Usuarios"("Id") ON DELETE CASCADE,
                "AdminUsuarioId" integer NULL REFERENCES "Usuarios"("Id") ON DELETE SET NULL,
                "PollaId" integer NULL REFERENCES "Pollas"("Id") ON DELETE SET NULL,
                "Titulo" text NOT NULL,
                "Mensaje" text NOT NULL,
                "TipoDestino" text NOT NULL DEFAULT 'Predicciones',
                "Link" text NOT NULL DEFAULT '/predicciones',
                "EtiquetaAccion" text NOT NULL DEFAULT 'Ir a predicciones',
                "Estado" text NOT NULL DEFAULT 'Pendiente',
                "FechaCreacion" timestamp with time zone NOT NULL DEFAULT now(),
                "FechaVista" timestamp with time zone NULL,
                "FechaCierre" timestamp with time zone NULL
            );

            ALTER TABLE "AlertasUsuario"
            ALTER COLUMN "PollaId" DROP NOT NULL;

            CREATE INDEX IF NOT EXISTS "IX_AlertasUsuario_Usuario_Estado_Fecha"
            ON "AlertasUsuario" ("UsuarioId", "Estado", "FechaCreacion");

            CREATE TABLE IF NOT EXISTS "VisualizacionesPrediccion" (
                "Id" bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "PollaId" integer NOT NULL REFERENCES "Pollas"("Id") ON DELETE CASCADE,
                "UsuarioObjetivoId" integer NOT NULL REFERENCES "Usuarios"("Id") ON DELETE CASCADE,
                "PartidoId" integer NOT NULL REFERENCES "Partidos"("Id") ON DELETE CASCADE,
                "UsuarioVisualizadorId" integer NOT NULL REFERENCES "Usuarios"("Id") ON DELETE CASCADE,
                "FechaVisualizacion" timestamp with time zone NOT NULL DEFAULT now()
            );

            CREATE INDEX IF NOT EXISTS "IX_VisualizacionesPrediccion_Consulta"
            ON "VisualizacionesPrediccion"
                ("PollaId", "UsuarioObjetivoId", "PartidoId", "FechaVisualizacion");

            CREATE INDEX IF NOT EXISTS "IX_VisualizacionesPrediccion_Visualizador"
            ON "VisualizacionesPrediccion"
                ("UsuarioVisualizadorId", "FechaVisualizacion");

            CREATE TABLE IF NOT EXISTS "RankingsPartidosPublicacion" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "PartidoId" integer NOT NULL REFERENCES "Partidos"("Id") ON DELETE CASCADE,
                "Publicado" boolean NOT NULL DEFAULT false,
                "FechaCalculo" timestamp with time zone NOT NULL DEFAULT now(),
                "FechaPublicacion" timestamp with time zone NULL,
                "AdminCalculoId" integer NULL REFERENCES "Usuarios"("Id") ON DELETE SET NULL,
                "AdminPublicacionId" integer NULL REFERENCES "Usuarios"("Id") ON DELETE SET NULL
            );

            CREATE UNIQUE INDEX IF NOT EXISTS "UX_RankingsPartidosPublicacion_Partido"
            ON "RankingsPartidosPublicacion" ("PartidoId");

            CREATE TABLE IF NOT EXISTS "RankingsPartidosAuditoriaDetalle" (
                "Id" bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "PartidoId" integer NOT NULL REFERENCES "Partidos"("Id") ON DELETE CASCADE,
                "PollaId" integer NOT NULL REFERENCES "Pollas"("Id") ON DELETE CASCADE,
                "UsuarioId" integer NOT NULL REFERENCES "Usuarios"("Id") ON DELETE CASCADE,
                "TienePrediccion" boolean NOT NULL DEFAULT false,
                "GolesLocalPrediccion" integer NULL,
                "GolesVisitantePrediccion" integer NULL,
                "PuntosPrevios" integer NOT NULL DEFAULT 0,
                "PuntosCambio" integer NOT NULL DEFAULT 0,
                "PuntosRanking" integer NOT NULL DEFAULT 0,
                "PuntosMarcadorCierre" integer NOT NULL DEFAULT 0,
                "PuntosClasificacionCierre" integer NOT NULL DEFAULT 0,
                "PuntosPodioCierre" integer NOT NULL DEFAULT 0,
                "FechaCalculo" timestamp with time zone NOT NULL DEFAULT now()
            );

            CREATE UNIQUE INDEX IF NOT EXISTS "UX_RankingsPartidosAuditoriaDetalle_Fila"
            ON "RankingsPartidosAuditoriaDetalle" ("PartidoId", "PollaId", "UsuarioId");

            CREATE INDEX IF NOT EXISTS "IX_RankingsPartidosAuditoriaDetalle_Polla"
            ON "RankingsPartidosAuditoriaDetalle" ("PartidoId", "PollaId", "PuntosRanking");
            """);
    }
}
