[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "         CAFE SIPARIS - TUM SISTEM BASLATILIYOR             " -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

$root = (Resolve-Path "$PSScriptRoot\..").Path

# 1. Onceki oturumlari ve portlari temizle
Write-Host "[1/5] Onceki oturumlar ve portlar kontrol ediliyor..." -ForegroundColor Yellow
Get-Process ngrok -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
$conns = Get-NetTCPConnection -LocalPort 5088 -ErrorAction SilentlyContinue
foreach ($c in $conns) {
    Stop-Process -Id $c.OwningProcess -Force -ErrorAction SilentlyContinue
}

# 2. Kasa API'sini baslat
Write-Host "[2/5] Kasa ve Siparis Sunucusu (API) baslatiliyor..." -ForegroundColor Yellow
Start-Process -FilePath "cmd.exe" -ArgumentList "/c cd /d `"$root\src\CafeSiparis.Api`" && dotnet run" -WindowStyle Minimized

# Port 5088 & Health Endpoint'in hazir olmasini bekle
$apiReady = $false
for ($i = 0; $i -lt 25; $i++) {
    Start-Sleep -Seconds 1
    try {
        $res = Invoke-RestMethod -Uri "http://127.0.0.1:5088/api/health" -TimeoutSec 2 -ErrorAction SilentlyContinue
        if ($res.ok -eq $true) { $apiReady = $true; break }
    } catch {}
}

if ($apiReady) {
    Write-Host "      [OK] Kasa API aktif (http://127.0.0.1:5088/station)" -ForegroundColor Green
} else {
    Write-Host "      [...] API baslatilmaya devam ediyor..." -ForegroundColor DarkYellow
}

# 3. Ngrok Sabit Guvenli Tuneli baslat
$staticDomain = "henchman-ninth-shanty.ngrok-free.dev"
$staticUrl = "https://$staticDomain"
Write-Host "[3/5] Kalici Ngrok tüneli baslatiliyor ($staticUrl)..." -ForegroundColor Yellow

Start-Process -FilePath "$root\ngrok.exe" -ArgumentList "http --url=$staticDomain 5088" -WindowStyle Minimized
Start-Sleep -Seconds 3

Write-Host "      [OK] Kalici internet adresi devrede: $staticUrl" -ForegroundColor Green

# 4. Garson kodundaki api.ts dosyasini kontrol et
Write-Host "[4/5] Garson mobil ayarlari kontrol ediliyor..." -ForegroundColor Yellow
$apiFile = "$root\waiter\src\api.ts"
$code = Get-Content $apiFile -Raw -Encoding UTF8
$code = $code -replace 'export const DEFAULT_API_URL = ".*?";', "export const DEFAULT_API_URL = `"$staticUrl`";"
Set-Content -Path $apiFile -Value $code -Encoding UTF8 -NoNewline
Write-Host "      [OK] Mobil sabit adres senkronize!" -ForegroundColor Green

# Tarayicida kasa ekranini 127.0.0.1 ile ac (IPv6 / localhost sorunlarini onler)
Start-Process "http://127.0.0.1:5088/station"

Write-Host ""
Write-Host "============================================================" -ForegroundColor Green
Write-Host "          ☕ SISTEM BASARIYLA AYAGA KALKTI ☕               " -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green
Write-Host " [KASA EKRANI]   (Bilgisayar) : http://127.0.0.1:5088/station" -ForegroundColor White
Write-Host " [GARSON EKRANI] (Telefon)    : $staticUrl/garson/" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Green
Write-Host ""
Write-Host "IPHONE'DA TEK TIKLA KULLANMA:" -ForegroundColor Yellow
Write-Host " 1. Telefonunuzdan (Safari) su adresi acin:" -ForegroundColor White
Write-Host "    $staticUrl/garson/" -ForegroundColor Cyan
Write-Host " 2. Safari'deki 'Paylas' simgesine basip 'Ana Ekrana Ekle' deyin." -ForegroundColor White
Write-Host " 3. Telefonunuzda 'Garson' ikonu olusur; bir daha hic ugrasmazsiniz!" -ForegroundColor White
Write-Host ""
Write-Host "------------------------------------------------------------" -ForegroundColor DarkGray
Write-Host "(Istege Bagli) Expo Go uzerinden kullanmak isterseniz:" -ForegroundColor Gray
Write-Host "Expo Dev Server simdi baslatiliyor..." -ForegroundColor Gray
Write-Host "------------------------------------------------------------" -ForegroundColor DarkGray
Write-Host ""

Set-Location "$root\waiter"
npm run start:tunnel
