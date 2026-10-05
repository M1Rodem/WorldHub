# WorldHub

WorldHub — desktop-приложение для управления Minecraft Dedicated Server.

Приложение позволяет запускать и останавливать сервер, синхронизировать его состояние между компьютерами, создавать резервные копии и восстанавливать предыдущие версии.

WorldHub работает с Dedicated Server напрямую и не требует установки Minecraft-мода.

---

## Возможности

- управление Minecraft Dedicated Server;
- запуск и безопасная остановка сервера;
- регистрация локальных серверов;
- Push / Pull;
- синхронизация через Google Drive;
- версионирование состояния сервера;
- Snapshot и Restore;
- хранение до 3 последних Snapshot;
- проверка целостности архивов;
- защита от конфликтов версий;
- связь между экземплярами WorldHub;
- работа через Radmin VPN;
- автоматическое обновление приложения.

---

## Как это работает

WorldHub управляет локальным Minecraft Dedicated Server.

                    WorldHub
                   /        \
                  /          \
          Radmin VPN       Google Drive
                                |
                                v
                           Server State
                                |
                           Snapshots

Radmin VPN используется для связи между компьютерами.

Google Drive используется для хранения состояния сервера.

Minecraft Client запускается отдельно через обычный Minecraft Launcher.

---

## Dedicated Server

WorldHub работает с уже существующим Minecraft Dedicated Server.

Например:

    D:\Minecraft\Servers\Survival

WorldHub может:

- добавить сервер;
- сохранить его конфигурацию;
- запустить сервер;
- отслеживать процесс;
- безопасно остановить сервер;
- удалить регистрацию сервера.

Minecraft Server должен быть остановлен перед операциями с его файлами.

---

## Push / Pull

### Push

Отправляет текущее состояние локального сервера в Google Drive.

    Local Server
         |
         v
    Google Drive

### Pull

Получает актуальное состояние сервера из Google Drive.

    Google Drive
         |
         v
    Local Server

WorldHub использует версии состояния сервера, чтобы не перезаписать более новую версию старой.

Пример:

    Local:  v15
    Cloud:  v16

    Push  -> запрещён
    Pull  -> доступен

---

## Snapshot

Snapshot — сохранённая версия состояния сервера.

WorldHub хранит до трёх последних Snapshot.

    snapshot-1
    snapshot-2
    snapshot-3

При создании нового Snapshot самая старая версия удаляется.

Snapshot можно использовать для восстановления сервера.

---

## Restore

Restore восстанавливает локальный сервер из выбранного Snapshot.

Перед восстановлением сервер должен быть остановлен.

Общий процесс:

    Snapshot
        |
        v
    Проверка
        |
        v
    Временное состояние
        |
        v
    Локальный сервер

---

## Безопасность

WorldHub не выполняет Push, Pull, Snapshot и Restore во время работы Dedicated Server.

Перед использованием загруженного архива выполняется проверка SHA-256.

Если облачная версия новее локальной, WorldHub не перезаписывает её старым состоянием.

---

## Network

WorldHub использует собственный TCP-протокол для связи между экземплярами приложения.

Radmin VPN используется как транспортная сеть.

    WorldHub A
        |
        | TCP
        |
    Radmin VPN
        |
        | TCP
        |
    WorldHub B

Сетевой слой отвечает за:

- подключение;
- handshake;
- проверку доступности;
- ping;
- получение статуса;
- восстановление соединения.
- подключение к локальному серверу (В Minecraft)

---

## Google Drive

Google Drive используется как облачное хранилище состояния сервера.

Структура:

    Google Drive
    └── WorldHub
        └── Servers
            └── <serverId>
                ├── manifest.json
                ├── current.zip
                └── snapshots
                    ├── snapshot-1.zip
                    ├── snapshot-2.zip
                    └── snapshot-3.zip

Google Drive не используется как live-файловая система Minecraft.

WorldHub сначала фиксирует состояние сервера, после чего загружает его в облако.

---

## Архитектура

    WorldHub
    |
    +-- WorldHub.App
    +-- WorldHub.Core
    +-- WorldHub.Infrastructure
    +-- WorldHub.Network
    +-- WorldHub.Sync
    +-- WorldHub.Updater
    |
    +-- Installer
    |
    +-- Build-Release.ps1
    +-- Version.props
    +-- Directory.Build.props
    +-- WorldHub.slnx

### WorldHub.App

Desktop-приложение и пользовательский интерфейс.

### WorldHub.Core

Основные модели и правила приложения.

### WorldHub.Infrastructure

Работа с файловой системой, локальным хранилищем и системными ресурсами.

### WorldHub.Network

TCP-соединения и протокол WorldHub.

### WorldHub.Sync

Push, Pull, версии, Snapshot, Restore и проверка целостности.

### WorldHub.Updater

Компонент обновления WorldHub.

### Installer

Установка приложения.

---

## Технологии

- C#
- .NET
- WPF
- TCP
- Google Drive API
- Google OAuth
- Radmin VPN

---

## Установка

1. Установить WorldHub через Installer.
2. Запустить приложение.
3. Добавить Minecraft Dedicated Server.
4. Указать параметры запуска сервера.
5. Подключить Google Account.
6. При необходимости подключиться к другому WorldHub через Radmin VPN.

---

## Требования

- Windows;
- Minecraft Dedicated Server;
- Google Account для облачной синхронизации;
- Radmin VPN для связи между компьютерами.

---

## Текущий статус

### Готово

- базовое desktop-приложение;
- архитектура проектов;
- TCP-соединение WorldHub;
- TCP listener;
- handshake;
- reconnect;
- определение Radmin VPN IP;
- настройка сетевого порта;
- Installer;
- Updater.

### В разработке

- управление Dedicated Server;
- регистрация серверов;
- Google Drive;
- Push / Pull;
- версии серверов;
- Snapshot;
- Restore;
- защита от конфликтов;
- интеграция компонентов.

---

## План развития

### Этап 1

Переработка существующей архитектуры под Dedicated Server.

### Этап 2

Управление локальным Minecraft Dedicated Server.

### Этап 3

Интеграция Google Drive.

### Этап 4

Push / Pull и версионирование.

### Этап 5

Snapshot и Restore.

### Этап 6

Связь нескольких экземпляров WorldHub.

### Этап 7

Полное тестирование между компьютерами.

Google Drive отвечает за облачное хранение состояния сервера.
