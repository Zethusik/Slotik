# Slotik Backend

Backend проекта Slotik на ASP.NET Core

Добавлена и протестирована оплата подписок через LiqPay

## LiqPay

Работает:

Basic и Pro  
оплата через LiqPay Sandbox  
callback от LiqPay  
проверка подписи суммы и валюты  
продление подписки  
переход Basic → Pro  
refund и `reversed`  
защита от повторных callback и refund

```text
Basic = 199 UAH
Pro = 399 UAH
```

## Настройка

В `appsettings.json` нужно добавить свои ключи:

```json
"CloudinarySettings": {
  "CloudName": "",
  "ApiKey": "",
  "ApiSecret": ""
},

"LiqPay": {
  "PublicKey": "",
  "PrivateKey": "",
  "ServerUrl": "https://YOUR-NGROK/api/Payment/liqpay/callback",
  "ResultUrl": "http://localhost:5173/payment/result",
  "BasicPriceUah": 199,
  "ProPriceUah": 399
}
```

Ключи Cloudinari и LiqPay получить отдельно  
В Git ключи не пушить

## Тестовые аккаунты

```text
Superadmin:
superadmin@slotik.local
SuperAdmin123!

Master:
master_kyiv_5@slotik.com
SuperAdmin123!

Master с истекшей подпиской:
masterexpired@slotik.com
SuperAdmin123!
```

## Ngrok

Для callback от LiqPay нужен ngrok:

```bash
ngrok config add-authtoken YOUR_TOKEN
ngrok http 5024
```

Полученный адрес поставить в:

```text
LiqPay -> ServerUrl
```

Пример:

```text
https://example.ngrok-free.dev/api/Payment/liqpay/callback
```

Проверить запросы можно тут:

```text
http://127.0.0.1:4040
```

## Тест оплаты

Запустить backend:

```bash
dotnet build
dotnet run
```

В Swagger зайти под Master и вызвать:

```text
POST /api/Payment/checkout

Basic: plan = 1
Pro: plan = 2
```

Из ответа взять `data` и `signature` и вставить в:

```text
DevTools/liqpay-test.html
```

После оплаты в ngrok должен прийти:

```text
POST /api/Payment/liqpay/callback 200 OK
```

Для теста refund зайти под Superadmin:

```text
POST /api/Payment/dev/refund/{paymentId}
```

Повторный refund должен вернуть `409 Conflict`

Для LiqPay используется библиотека:

```text
BouncyCastle.Cryptography
```

ПЕред push проверить чтоб в `appsettings.json` не осталось реальных ключей