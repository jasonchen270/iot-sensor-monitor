// ESP32 sensor node - reed switch (door) on GPIO 4, PIR (motion) on GPIO 5.
// Sends JSON frames over WebSocket on state change + 30s heartbeat.
// Libraries: WebSockets by Markus Sattler, ArduinoJson by Benoit Blanchon.

#include <WiFi.h>
#include <WebSocketsClient.h>
#include <ArduinoJson.h>

const char* WIFI_SSID = "YOUR_SSID";
const char* WIFI_PASS = "YOUR_PASS";
const char* WS_HOST   = "your.server.example";   // EC2 public DNS or LAN IP
const uint16_t WS_PORT = 80;                      // 80 behind nginx, 5080 direct
const char* WS_PATH   = "/ingest";
const char* DEVICE_ID = "esp32-front-door";

const int PIN_DOOR   = 4;   // reed switch -> GND, INPUT_PULLUP. LOW = closed
const int PIN_MOTION = 5;   // PIR OUT pin. HIGH = motion

WebSocketsClient ws;
int lastDoor = -1, lastMotion = -1;
unsigned long lastBeat = 0;

void send(const char* type, const char* state, float value = NAN) {
  StaticJsonDocument<192> doc;
  doc["deviceId"] = DEVICE_ID;
  doc["type"] = type;
  doc["state"] = state;
  if (!isnan(value)) doc["value"] = value;
  doc["ts"] = (uint64_t) time(nullptr) * 1000ULL;
  char buf[192];
  size_t n = serializeJson(doc, buf);
  ws.sendTXT(buf, n);
}

void onWs(WStype_t type, uint8_t* payload, size_t len) {
  if (type == WStype_CONNECTED) Serial.println("[ws] connected");
  if (type == WStype_DISCONNECTED) Serial.println("[ws] disconnected");
}

void setup() {
  Serial.begin(115200);
  pinMode(PIN_DOOR, INPUT_PULLUP);
  pinMode(PIN_MOTION, INPUT);
  WiFi.begin(WIFI_SSID, WIFI_PASS);
  while (WiFi.status() != WL_CONNECTED) { delay(300); Serial.print("."); }
  Serial.println("\n[wifi] " + WiFi.localIP().toString());
  configTime(0, 0, "pool.ntp.org");
  ws.begin(WS_HOST, WS_PORT, WS_PATH);
  ws.onEvent(onWs);
  ws.setReconnectInterval(3000);
}

void loop() {
  ws.loop();
  int d = digitalRead(PIN_DOOR);
  int m = digitalRead(PIN_MOTION);
  if (d != lastDoor) {
    lastDoor = d;
    send("door", d == LOW ? "closed" : "open");
  }
  if (m != lastMotion) {
    lastMotion = m;
    send("motion", m == HIGH ? "detected" : "clear");
  }
  if (millis() - lastBeat > 30000) {
    lastBeat = millis();
    send("heartbeat", "ok");
  }
  delay(20);
}
