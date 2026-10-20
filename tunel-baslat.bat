@echo off
cd /d "%~dp0"
echo ===================================================
echo Cloudflare API Tuneli Baslatiliyor...
echo Bu tunel sayesinde guvenlik duvari veya IP ayarlari
echo olmadan iPhone telefonunuz kasaya dogrudan baglanir.
echo ===================================================
echo.
cloudflared.exe tunnel --url http://localhost:5088
pause
