import http from 'http';
import { WebSocketServer } from 'ws';

const devices = [
  { id: 'esp32-01', name: 'Boiler Room',  type: 'temperature', location: 'Basement B1',  lastSeen: new Date().toISOString() },
  { id: 'esp32-02', name: 'Server Rack',  type: 'temperature', location: 'Data Center',  lastSeen: new Date().toISOString() },
  { id: 'esp32-03', name: 'Front Door',   type: 'motion',      location: 'Entrance',     lastSeen: new Date().toISOString() },
  { id: 'esp32-04', name: 'Flood Sensor', type: 'water',       location: 'Utility Room', lastSeen: new Date().toISOString() },
];

let alerts = [
  { id: 1, deviceId: 'esp32-01', message: 'Temperature exceeded 80°C threshold', severity: 'critical', timestamp: new Date(Date.now() - 120000).toISOString(), acknowledged: false },
  { id: 2, deviceId: 'esp32-04', message: 'Water detected on floor',              severity: 'warning',  timestamp: new Date(Date.now() - 300000).toISOString(), acknowledged: false },
  { id: 3, deviceId: 'esp32-02', message: 'High CPU temp: 72°C',                  severity: 'info',     timestamp: new Date(Date.now() - 600000).toISOString(), acknowledged: true  },
];

const server = http.createServer((req, res) => {
  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Methods', 'GET,POST,DELETE');
  res.setHeader('Content-Type', 'application/json');

  if (req.method === 'GET' && req.url === '/api/devices') {
    res.end(JSON.stringify(devices));
  } else if (req.method === 'GET' && req.url === '/api/alerts') {
    res.end(JSON.stringify(alerts));
  } else if (req.method === 'POST' && req.url?.match(/^\/api\/alerts\/\d+\/ack$/)) {
    const id = parseInt(req.url.split('/')[3]);
    alerts = alerts.map(a => a.id === id ? { ...a, acknowledged: true } : a);
    res.end(JSON.stringify({ ok: true }));
  } else {
    res.writeHead(404);
    res.end('{}');
  }
});

const wss = new WebSocketServer({ server, path: '/live' });
const clients = new Set();

wss.on('connection', ws => {
  clients.add(ws);
  ws.on('close', () => clients.delete(ws));
});

const readings = [
  { deviceId: 'esp32-01', type: 'temperature', state: 'critical', value: 83.2 },
  { deviceId: 'esp32-02', type: 'temperature', state: 'warn',     value: 72.1 },
  { deviceId: 'esp32-03', type: 'motion',       state: 'motion',  value: null  },
  { deviceId: 'esp32-04', type: 'water',         state: 'wet',    value: null  },
];
let i = 0;

setInterval(() => {
  const base = readings[i++ % readings.length];
  const reading = {
    id: Date.now(),
    deviceId: base.deviceId,
    type: base.type,
    state: base.state,
    value: base.value != null ? parseFloat((base.value + (Math.random() - 0.5) * 2).toFixed(1)) : null,
    timestamp: new Date().toISOString(),
  };
  const msg = JSON.stringify({ kind: 'reading', payload: reading });
  for (const ws of clients) ws.readyState === 1 && ws.send(msg);
}, 900);

server.listen(5080, () => console.log('Mock server running on http://localhost:5080'));
