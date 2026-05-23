# IoT Sensor Monitor

An ASP.NET Core service that ingests ESP32 sensor frames over WebSocket, stores readings in PostgreSQL, evaluates per-device thresholds, and broadcasts live updates to a React dashboard.

## Prerequisites

- .NET 10 SDK
- Node 18+
- PostgreSQL 16
- Arduino IDE with the WebSockets (Markus Sattler) and ArduinoJson libraries (only to flash real ESP32 hardware)

## Build & run

```bash
# 1. Postgres
brew services start postgresql@16
createdb sensors

# 2. Service
cd service && dotnet run
# listens on http://localhost:5080

# 3. Web
cd web && npm run dev

# 4. Simulator (no hardware)
cd scripts && npm i && node simulate.mjs
```

The simulator feeds synthetic frames so you can run the full stack without hardware. To use a real device, edit the Wi-Fi and `WS_HOST` constants in `firmware/sensor_node/sensor_node.ino` and flash it via the Arduino IDE (reed switch on GPIO 4 to GND, PIR OUT on GPIO 5).
