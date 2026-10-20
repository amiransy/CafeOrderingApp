@echo off
cd /d "%~dp0waiter"
echo Garson uygulamasi baslatiliyor...
echo Telefonunda Expo Go acik olsun. QR kod birazdan gelecek.
echo.
"C:\Program Files\nodejs\node.exe" "C:\Program Files\nodejs\node_modules\npm\bin\npm-cli.js" start
pause
