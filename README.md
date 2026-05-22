# IoT Sensor Monitor

ASP.NET Core service ingests ESP32 sensor frames over WebSocket, stores readings in Postgres, evaluates per-device thresholds, and broadcasts live updates to a React dashboard. Deploys on EC2 behind nginx.

## Layout
- `service/`: ASP.NET Core minimal API + EF Core (Postgres). WS `/ingest`, WS `/live`, REST `/api/*`.
- `web/`: Vite + React dashboard.
- `firmware/sensor_node/`: ESP32 Arduino sketch (reed switch + PIR).
- `scripts/simulate.mjs`: fake ESP32 for local dev.
- `deploy/`: nginx config, systemd unit, deploy script.

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

## Deploy to EC2
```
HOST=ec2-x-x-x-x.compute.amazonaws.com KEY=~/.ssh/key.pem ./deploy/deploy.sh
```
Update the DB password in `deploy/sensor-monitor.service` and the postgres `CREATE USER` line in `deploy.sh` before running.

## ESP32
Edit `firmware/sensor_node/sensor_node.ino` Wi-Fi + `WS_HOST` constants, flash via Arduino IDE (libraries: WebSockets by Markus Sattler, ArduinoJson). Reed switch on GPIO 4 to GND, PIR OUT on GPIO 5.
