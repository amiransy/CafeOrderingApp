@echo off
chcp 65001 >nul
title Cafe Siparis - Tum Sistem Baslatici
powershell -ExecutionPolicy Bypass -File "%~dp0scripts\baslat.ps1"
pause
