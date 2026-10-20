# Cafe Sipariş

Çalışanların kullandığı sipariş sistemi: garson telefonu, ortak sunucu ve kasa/bar adisyon yazdırma.

## Parçalar

- `waiter` — Garson telefonu için Expo (React Native) uygulaması. Masa seçilir, kategorilerden ürün eklenir, sipariş onaylanır.
- `src/CafeSiparis.Api` — Menü, masalar ve siparişler. Kasa ekranı: `http://KASA-IP:5088/station`

## Çalıştırma

1. Kasa/bar bilgisayarında API:

```powershell
dotnet run --project src/CafeSiparis.Api
```

2. Aynı ağdaki tarayıcıda kasa ekranını aç: `http://localhost:5088/station`

   Yazıcı bu bilgisayara bağlıysa sipariş gelince yazdırma penceresi açılır.

3. Garson uygulaması:

```powershell
cd waiter
npm start
```

Telefonda Expo Go ile QR kodu okut. Ayarlardan API adresini kasa bilgisayarının yerel IP’si yap: `http://192.168.x.x:5088`

Windows Güvenlik Duvarı 5088 portunu engelliyorsa telefon bağlanamaz; özel ağ için gelen kural ekle.

## Not

Termal yazıcı (ESC/POS) henüz yok; kasa tarayıcısı Windows yazıcısına basar. Bunu sonra doğrudan yazıcıya bağlayabiliriz.