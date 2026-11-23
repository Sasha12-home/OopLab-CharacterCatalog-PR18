[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = "Stop"

Write-Host "==============================================================" -ForegroundColor Cyan
Write-Host "  ФІНАЛЬНА ЗБІРКА ТА ПЕРЕВІРКА ПРОЄКТУ (ПР 18 - Finalization)  " -ForegroundColor Cyan
Write-Host "==============================================================" -ForegroundColor Cyan

$releaseRoot = Join-Path $PSScriptRoot "release"
$guiDistDir  = Join-Path $releaseRoot "OopLab.CharacterCatalog-v1.0-win-x64"
$cliDistDir  = Join-Path $guiDistDir "cli"
$zipPath     = Join-Path $releaseRoot "OopLab.CharacterCatalog-v1.0-win-x64.zip"

Write-Host "`n[1/5] Запуск автоматизованих тестів перевірки архітектури та логіки..." -ForegroundColor Yellow
dotnet run --project "$PSScriptRoot\tests\OopLab.Verification\OopLab.Verification.csproj" -c Release
if ($LASTEXITCODE -ne 0) { throw "Автоматизована перевірка завершилася з помилкою (код $LASTEXITCODE)" }

if (Test-Path $releaseRoot) {
    Remove-Item $releaseRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $guiDistDir | Out-Null

Write-Host "`n[2/5] Компіляція WPF GUI у конфігурації Release (win-x64)..." -ForegroundColor Yellow
dotnet publish "$PSScriptRoot\src\OopLab.Gui\OopLab.Gui.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -p:DebugType=none `
    -o $guiDistDir
if ($LASTEXITCODE -ne 0) { throw "Помилка збірки OopLab.Gui (код $LASTEXITCODE)" }

Write-Host "`n[3/5] Компіляція CLI у конфігурації Release (win-x64)..." -ForegroundColor Yellow
dotnet publish "$PSScriptRoot\src\OopLab.Cli\OopLab.Cli.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -p:DebugType=none `
    -o $cliDistDir
if ($LASTEXITCODE -ne 0) { throw "Помилка збірки OopLab.Cli (код $LASTEXITCODE)" }

Write-Host "`n[4/5] Копіювання дизайн-документа та інструкції користувача..." -ForegroundColor Yellow
Copy-Item "$PSScriptRoot\README.md" -Destination (Join-Path $guiDistDir "README.md")
Copy-Item "$PSScriptRoot\docs\DESIGN_DOCUMENT.md" -Destination (Join-Path $guiDistDir "DESIGN_DOCUMENT.md")

$runHelp = @"
КАТАЛОГ ПЕРСОНАЖІВ (Rick & Morty API) — v1.0.0 Final Release
=============================================================

1. ЗАПУСК ГРАФІЧНОГО ДОДАТКУ (GUI):
   Запустіть файл OopLab.Gui.exe подвійним кліком.

2. ЗАПУСК КОНСОЛЬНОГО ІНТЕРФЕЙСУ (CLI):
   Відкрийте термінал у папці cli\ та виконайте: OopLab.Cli.exe

3. СИСТЕМНІ ВИМОГИ:
   - ОС: Windows 10 / Windows 11 (x64)
   - Середовище виконання: .NET 8.0 Desktop Runtime (x64)
     (Завантажити: https://dotnet.microsoft.com/download/dotnet/8.0)

4. ЗБЕРЕЖЕННЯ ДАНИХ ТА КЕШУ:
   Усі завантажені дані та буферизовані портрети зберігаються у:
   %LocalAppData%\OopLab.CharacterCatalog\
"@
Set-Content -Path (Join-Path $guiDistDir "ІНСТРУКЦІЯ_ЗАПУСКУ.txt") -Value $runHelp -Encoding UTF8

Write-Host "`n[5/5] Пакування фінального релізу в ZIP-архів..." -ForegroundColor Yellow
Compress-Archive -Path "$guiDistDir\*" -DestinationPath $zipPath -Force

Write-Host "`n==============================================================" -ForegroundColor Green
Write-Host "  ФІНАЛЬНА ЗБІРКА ТА ПЕРЕВІРКА УСПІШНО ЗАВЕРШЕНІ!" -ForegroundColor Green
Write-Host "  Папка релізу (.exe + .dll): $guiDistDir" -ForegroundColor White
Write-Host "  Архів для GitHub Release:    $zipPath" -ForegroundColor Green
Write-Host "==============================================================" -ForegroundColor Green
