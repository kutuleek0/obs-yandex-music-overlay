OBS Music Overlay (Яндекс Музыка)

Простой оверлей «Сейчас играет» для OBS. Работает из коробки на Windows (десктоп‑приложение Яндекс Музыки) и как браузерный источник в OBS.

Быстрый старт (Windows)
1) Скачайте релиз для Windows (папка dist/win-x64) или соберите сами:
   - Установите Node.js 18+ и .NET SDK 8
   - npm install
   - npm run build:win
2) Запустите obs-music-overlay.exe. Откроется сервер на http://localhost:3000
3) В OBS добавьте Browser Source и укажите URL: http://localhost:3000/?w=560
   - Параметр w — ширина плашки в пикселях (минимум 300)

Linux / macOS
- Сервер можно упаковать (npm run build:linux / build:macos), но получения трека нет. На Linux можно интегрировать playerctl — при запросе добавьте issue.

Настройка
- Только Яндекс Музыка: по умолчанию фильтр по AUMID содержит «yandex» и «music». Можно переопределить переменной YM_ALLOW.
- Ширина: параметр URL w=..., например ?w=420

Запуск из исходников (Windows)
1) npm install
2) dotnet build smtc-helper/smtc-helper.csproj -c Release
3) npm run start
4) В OBS: Browser Source -> http://localhost:3000/?w=560

Поддержка
Если что-то не работает — создайте issue с логом консоли и скриншотом.


