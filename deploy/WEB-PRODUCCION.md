# Publicacion web - WorldCup Polla

Esta guia deja separados los valores locales de los valores reales de produccion.

## 1. Preparar PostgreSQL

Para publicar rapido y con menos riesgo, crea una base PostgreSQL de produccion y restaura una copia de la base local ya probada.

Despues ejecuta, si aplica, el script:

```sql
deploy/sql/actualizaciones-produccion.sql
```

La API tambien aplica estos ajustes al iniciar, pero el script sirve para revisar o ejecutar manualmente antes del primer arranque.

## 2. Variables de entorno de la API

Configura estos valores en el hosting donde corra `WorldCup.Api`:

```powershell
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=Host=TU_HOST;Port=5432;Database=TU_DB;Username=TU_USUARIO;Password=TU_PASSWORD;SSL Mode=Require;Trust Server Certificate=true
Jwt__Key=GENERAR_UNA_CLAVE_DE_32_CARACTERES_O_MAS
Jwt__Issuer=WorldCupPolla
Jwt__Audience=WorldCupPollaUsers
ApiAccess__RequireKey=true
ApiAccess__Key=LA_MISMA_LLAVE_PRIVADA_DE_LA_WEB
AdminSettings__Emails__0=monolin020988@gmail.com
SmtpSettings__Host=smtp.gmail.com
SmtpSettings__Port=587
SmtpSettings__EnableSsl=true
SmtpSettings__User=CORREO_GMAIL_DE_LA_APP
SmtpSettings__Password=PASSWORD_DE_APLICACION_DE_GMAIL
SmtpSettings__FromEmail=CORREO_GMAIL_DE_LA_APP
SmtpSettings__FromName=WorldCup Polla
```

Generar llaves:

```powershell
[Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```

Usa una llave para `Jwt__Key` y otra diferente para `ApiAccess__Key`.

## 3. Variables de entorno de la Web

Configura estos valores en el hosting donde corra `WorldCup.App.Web`:

```powershell
ASPNETCORE_ENVIRONMENT=Production
ApiBaseUrl=https://URL-DE-TU-API/
ApiAccess__Key=LA_MISMA_LLAVE_PRIVADA_DE_LA_API
```

La URL de la API debe terminar en `/`.

## 4. Comandos de publicacion

```powershell
dotnet publish WorldCup.Api/WorldCup.Api.csproj -c Release -o publish/api
dotnet publish WorldCup.App.Web/WorldCup.App.Web.csproj -c Release -o publish/web
```

## 5. Pruebas despues de publicar

1. Abrir `https://URL-DE-TU-API/health`.
2. Abrir `https://URL-DE-TU-WEB/health`.
3. Registrar un usuario nuevo y confirmar correo.
4. Iniciar sesion.
5. Crear una polla con valor y metodo de pago.
6. Invitar un correo real.
7. Llenar predicciones.
8. Entrar como admin y guardar un marcador real.
9. Revisar ranking y detalle de puntos.

## 6. Reglas importantes

- No publiques `appsettings.Development.json` con datos reales.
- No pongas claves reales en el repositorio.
- La API en produccion queda protegida con `X-WorldCup-Api-Key`; la Web la envia automaticamente.
- Swagger solo queda activo en desarrollo.
- Si falta una configuracion obligatoria, la app falla al iniciar para evitar publicarse mal configurada.
