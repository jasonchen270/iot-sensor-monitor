# IoT Sensor Monitor

ASP.NET Core service ingests ESP32 sensor frames over WebSocket, stores readings in Postgres, evaluates per-device thresholds, and broadcasts live updates to a React dashboard. Deploys on EC2 behind nginx.

## Local run
```
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

## Frame schema
```json
{ "deviceId": "esp32-front-door", "type": "door", "state": "open", "value": null, "ts": 1714600000000 }
```

## Thresholds
POST `/api/thresholds` `{ deviceId, metric: "state"|"value", op: "open"|">"|"<"|..., value, severity: "warn"|"crit" }`
- `state` matches the literal state (e.g., `op:"open"` fires when state == "open").
- `value` compares numeric values from the frame.

## ESP32
Edit `firmware/sensor_node/sensor_node.ino` Wi-Fi + `WS_HOST` constants, flash via Arduino IDE (libraries: WebSockets by Markus Sattler, ArduinoJson). Reed switch on GPIO 4 to GND, PIR OUT on GPIO 5.
