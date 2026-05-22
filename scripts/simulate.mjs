// Stand-in for an ESP32 that sends fake door + motion frames to /ingest.
// Usage: node scripts/simulate.mjs [ws://localhost:5080/ingest]
import WebSocket from 'ws'

const url = process.argv[2] || 'ws://localhost:5080/ingest'
const ws = new WebSocket(url)
const devices = [
  { deviceId: 'sim-front-door', type: 'door', states: ['open', 'closed'] },
  { deviceId: 'sim-hall-pir',   type: 'motion', states: ['detected', 'clear'] },
]

ws.on('open', () => {
  console.log('connected', url)
  setInterval(() => {
    const d = devices[Math.floor(Math.random() * devices.length)]
    const state = d.states[Math.floor(Math.random() * d.states.length)]
    const frame = { deviceId: d.deviceId, type: d.type, state, ts: Date.now() }
    ws.send(JSON.stringify(frame))
    console.log('->', frame)
  }, 1500)
})
ws.on('error', (e) => console.error(e.message))
ws.on('close', () => process.exit(0))
