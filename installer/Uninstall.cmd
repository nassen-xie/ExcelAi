@echo off
rem Excel AI アシスタントを外します（設定・キャッシュ・ログは残します）
chcp 65001 >nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Uninstall.ps1"
echo.
pause
