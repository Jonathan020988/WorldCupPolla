# Configurar Gmail SMTP

## 1. Preparar Gmail

1. Entra a tu cuenta de Google.
2. Activa la verificacion en 2 pasos.
3. Abre la pagina de contrasenas de aplicacion:
   https://myaccount.google.com/apppasswords
4. Crea una contrasena de aplicacion para `WorldCup Polla`.
5. Copia la clave de 16 caracteres. Usala sin espacios.

Google indica que las contrasenas de aplicacion solo estan disponibles con verificacion en 2 pasos activa.

## 2. Guardar los secretos localmente

Desde la carpeta raiz del proyecto, ejecuta estos comandos cambiando el correo y la clave:

```powershell
dotnet user-secrets set "SmtpSettings:User" "tu-correo@gmail.com" --project WorldCup.Api
dotnet user-secrets set "SmtpSettings:Password" "clave16caracteres" --project WorldCup.Api
dotnet user-secrets set "SmtpSettings:FromEmail" "tu-correo@gmail.com" --project WorldCup.Api
```

No guardes la contrasena de aplicacion en `appsettings.json`.

## 3. Probar

1. Ejecuta la API y la web.
2. Entra al panel `Admin`.
3. Usa la seccion `Prueba de correo SMTP`.
4. Revisa bandeja de entrada y spam.

## 4. Produccion

En el servidor, configura estas variables de entorno:

```text
SmtpSettings__Host=smtp.gmail.com
SmtpSettings__Port=587
SmtpSettings__EnableSsl=true
SmtpSettings__User=tu-correo@gmail.com
SmtpSettings__Password=clave16caracteres
SmtpSettings__FromEmail=tu-correo@gmail.com
SmtpSettings__FromName=WorldCup Polla
```
