@echo off
title Aura Core Pro — First-Time Setup
echo.
echo   ╔═════════════════════════════════════════╗
echo   ║   AURA CORE PRO — FIRST-TIME SETUP     ║
echo   ╚═════════════════════════════════════════╝
echo.

REM ── Step 1: dotnet-ef ──
echo   [1/3] Checking dotnet-ef tool...
dotnet ef --version >nul 2>&1
if errorlevel 1 (
    echo         Installing dotnet-ef v8.0.11...
    dotnet tool install --global dotnet-ef --version 8.0.11
)
echo         OK
echo.

REM ── Step 2: Drop and recreate database ──
echo   [2/3] Setting up database...
set PGPASSWORD=10062005

REM Try to drop (ignore errors if doesn't exist)
psql -h localhost -U postgres -c "DROP DATABASE IF EXISTS auracoredb;" >nul 2>&1
psql -h localhost -U postgres -c "CREATE DATABASE auracoredb;" >nul 2>&1

REM Remove old migrations
if exist src\Backend\AuraCore.API.Infrastructure\Migrations (
    rmdir /s /q src\Backend\AuraCore.API.Infrastructure\Migrations
)
if exist src\Backend\AuraCore.API.Infrastructure\Data\Migrations (
    rmdir /s /q src\Backend\AuraCore.API.Infrastructure\Data\Migrations
)

REM Create and apply fresh migration
dotnet ef migrations add InitialCreate --project src\Backend\AuraCore.API.Infrastructure --startup-project src\Backend\AuraCore.API --output-dir Migrations >nul 2>&1
dotnet ef database update --project src\Backend\AuraCore.API.Infrastructure --startup-project src\Backend\AuraCore.API
if errorlevel 1 (
    echo         ERROR: Database setup failed!
    pause
    exit /b 1
)
echo         OK
echo.

REM ── Step 3: Instructions ──
echo   [3/3] Database ready!
echo.
echo   ══════════════════════════════════════════
echo   Setup complete!
echo   ══════════════════════════════════════════
echo.
echo   Now run these 3 commands in order:
echo.
echo   1. Start backend:
echo      start-backend.bat
echo.
echo   2. Register admin (new terminal):
echo      curl -X POST http://localhost:5000/api/auth/register -H "Content-Type: application/json" -d "{\"email\":\"admin@auracore.pro\",\"password\":\"Admin12345\"}"
echo.
echo   3. Promote to admin (same terminal):
echo      curl -X POST http://localhost:5000/api/setup/promote-admin -H "Content-Type: application/json" -d "{\"email\":\"admin@auracore.pro\"}"
echo.
echo   Then login with the desktop app!
echo.
pause
