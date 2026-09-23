$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

Write-Host "==============================================="
Write-Host " Event Arena · Event Sourcing + CQRS"
Write-Host "==============================================="
Write-Host "Levantando la solucion con Docker Compose..."
Write-Host ""

docker compose pull
docker compose build
docker compose up -d

Write-Host ""
Write-Host "Esperando a que Service A y Service B respondan..."
$ready = $false
for ($i = 1; $i -le 40; $i++) {
    $codeA = curl.exe -s -o NUL -w "%{http_code}" --max-time 2 http://localhost:3001/health
    $codeB = curl.exe -s -o NUL -w "%{http_code}" --max-time 2 http://localhost:3002/health
    if ($codeA -eq "200" -and $codeB -eq "200") {
        $ready = $true
        break
    }
    Start-Sleep -Seconds 3
}

docker compose ps
Write-Host ""

if ($ready) {
    Write-Host "Listo. Abrir el frontend y probar el escenario:"
} else {
    Write-Host "Los contenedores ya estan arriba, pero la salud tardó. Reintentar en unos segundos."
}

Write-Host "  Frontend:    http://localhost:8080"
Write-Host "  Service A:   http://localhost:3001/health"
Write-Host "  Service B:   http://localhost:3002/health"
Write-Host "  EventStore:  http://localhost:2113"
Write-Host ""
Write-Host "Para detener: docker compose down"
