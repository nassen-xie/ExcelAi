@echo off
rem Excel AI アシスタントを入れます（管理者の権限は要りません）
chcp 65001 >nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install.ps1"
echo.
pause
