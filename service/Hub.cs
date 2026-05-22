using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace SensorMonitor;

public class Hub
{
    private readonly ConcurrentDictionary<Guid, WebSocket> _clients = new();
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task Attach(WebSocket ws, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        _clients[id] = ws;
        try
        {
            var buf = new byte[1024];
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var r = await ws.ReceiveAsync(buf, ct);
                if (r.MessageType == WebSocketMessageType.Close) break;
            }
        }
        catch { }
        finally { _clients.TryRemove(id, out _); }
    }

    public async Task Broadcast(LiveEvent ev)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(ev, Json));
        foreach (var (id, ws) in _clients)
        {
            if (ws.State != WebSocketState.Open) { _clients.TryRemove(id, out _); continue; }
            try { await ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None); }
            catch { _clients.TryRemove(id, out _); }
        }
    }
}

public class AlertEngine
{
    private readonly IServiceScopeFactory _scopes;
    private readonly Hub _hub;
    public AlertEngine(IServiceScopeFactory scopes, Hub hub) { _scopes = scopes; _hub = hub; }

    public async Task Evaluate(Reading r)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var thresholds = db.Thresholds.Where(t => t.DeviceId == r.DeviceId).ToList();
        foreach (var t in thresholds)
        {
            bool fire = false;
            if (t.Metric == "state" && r.State == t.Op) fire = true;
            else if (t.Metric == "value" && r.Value is double v)
                fire = t.Op switch
                {
                    ">" => v > t.Value,
                    "<" => v < t.Value,
                    ">=" => v >= t.Value,
                    "<=" => v <= t.Value,
                    "==" => Math.Abs(v - t.Value) < 1e-9,
                    _ => false
                };
            if (fire)
            {
                var alert = new Alert
                {
                    DeviceId = r.DeviceId,
                    Message = $"{t.Metric} {t.Op} {t.Value} ({r.State}{(r.Value is null ? "" : $"={r.Value}")})",
                    Severity = t.Severity,
                    Timestamp = DateTime.UtcNow,
                };
                db.Alerts.Add(alert);
                await db.SaveChangesAsync();
                await _hub.Broadcast(new LiveEvent("alert", alert));
            }
        }
    }
}
