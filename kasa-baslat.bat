@echo off
cd /d "%~dp0src\CafeSiparis.Api"
echo ===================================================
echo Kasa API ve Adisyon Sunucusu Baslatiliyor...
echo Kasa Ekrani: http://localhost:5088/station
echo ===================================================
echo.
dotnet run
pause
