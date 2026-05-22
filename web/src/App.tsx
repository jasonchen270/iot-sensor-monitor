import { useEffect, useMemo, useRef, useState } from 'react'
import './App.css'

const API = (import.meta.env.VITE_API as string) || 'http://localhost:5080'
const WS  = (import.meta.env.VITE_WS  as string) || 'ws://localhost:5080/live'

type Device = { id: string; name: string; type: string; location: string; lastSeen: string }
type Reading = { id: number; deviceId: string; type: string; state: string; value: number | null; timestamp: string }
type Alert = { id: number; deviceId: string; message: string; severity: string; timestamp: string; acknowledged: boolean }

export default function App() {
  const [devices, setDevices] = useState<Device[]>([])
  const [latest, setLatest] = useState<Record<string, Reading>>({})
  const [alerts, setAlerts] = useState<Alert[]>([])
  const [connected, setConnected] = useState(false)
  const wsRef = useRef<WebSocket | null>(null)

  useEffect(() => {
    fetch(`${API}/api/devices`).then(r => r.json()).then(setDevices)
    fetch(`${API}/api/alerts`).then(r => r.json()).then(setAlerts)
  }, [])

  useEffect(() => {
    const connect = () => {
      const ws = new WebSocket(WS)
      wsRef.current = ws
      ws.onopen = () => setConnected(true)
      ws.onclose = () => { setConnected(false); setTimeout(connect, 2000) }
      ws.onmessage = (e) => {
        const ev = JSON.parse(e.data)
        if (ev.kind === 'reading') {
          const r: Reading = ev.payload
          setLatest(prev => ({ ...prev, [r.deviceId]: r }))
        } else if (ev.kind === 'alert') {
          setAlerts(prev => [ev.payload, ...prev].slice(0, 200))
        }
      }
    }
    connect()
    return () => wsRef.current?.close()
  }, [])

  const ack = async (id: number) => {
    await fetch(`${API}/api/alerts/${id}/ack`, { method: 'POST' })
    setAlerts(a => a.map(x => x.id === id ? { ...x, acknowledged: true } : x))
  }

  const unacked = useMemo(() => alerts.filter(a => !a.acknowledged), [alerts])

  return (
    <div className="app">
      <header>
        <h1>IoT Sensor Monitor</h1>
        <span className={`badge ${connected ? 'ok' : 'bad'}`}>{connected ? 'live' : 'offline'}</span>
      </header>

      {unacked.length > 0 && (
        <div className="alert-banner">
          {unacked.length} active alert{unacked.length > 1 ? 's' : ''}
        </div>
      )}

      <section>
        <h2>Devices</h2>
        <div className="grid">
          {devices.map(d => {
            const r = latest[d.id]
            return (
              <div key={d.id} className="tile">
                <div className="tile-head">
                  <strong>{d.name}</strong>
                  <small>{d.type}</small>
                </div>
                <div className="tile-body">
                  <div className="state">{r?.state ?? 'N/A'}</div>
                  {r?.value != null && <div className="value">{r.value}</div>}
                  <small>{r ? new Date(r.timestamp).toLocaleTimeString() : 'no data'}</small>
                </div>
                <small className="loc">{d.location}</small>
              </div>
            )
          })}
          {devices.length === 0 && <p>No devices yet. Connect an ESP32 to <code>/ingest</code>.</p>}
        </div>
      </section>

      <section>
        <h2>Alerts</h2>
        <ul className="alerts">
          {alerts.map(a => (
            <li key={a.id} className={`sev-${a.severity} ${a.acknowledged ? 'ackd' : ''}`}>
              <span className="ts">{new Date(a.timestamp).toLocaleString()}</span>
              <span className="dev">{a.deviceId}</span>
              <span className="msg">{a.message}</span>
              {!a.acknowledged && <button onClick={() => ack(a.id)}>ack</button>}
            </li>
          ))}
          {alerts.length === 0 && <p>No alerts.</p>}
        </ul>
      </section>
    </div>
  )
}
