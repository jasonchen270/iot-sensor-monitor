using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SensorMonitor;

var builder = WebApplication.CreateBuilder(args);
var conn = builder.Configuration.GetConnectionString("Db")
    ?? "Host=localhost;Port=5432;Database=sensors;Username=jasonchen";
builder.Services.AddDbContext<AppDb>(o => o.UseNpgsql(conn));
builder.Services.AddSingleton<Hub>();
builder.Services.AddScoped<AlertEngine>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();
app.UseCors();
app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) });

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    db.Database.EnsureCreated();
}

var jsonOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };

app.MapGet("/api/devices", async (AppDb db) => await db.Devices.Include(d => d.Thresholds).ToListAsync());
app.MapPost("/api/devices", async (AppDb db, Device d) => { db.Devices.Add(d); await db.SaveChangesAsync(); return Results.Ok(d); });
app.MapGet("/api/devices/{id}/readings", async (AppDb db, string id, int? limit) =>
    await db.Readings.Where(r => r.DeviceId == id).OrderByDescending(r => r.Timestamp).Take(limit ?? 100).ToListAsync());
app.MapGet("/api/alerts", async (AppDb db) => await db.Alerts.OrderByDescending(a => a.Timestamp).Take(200).ToListAsync());
app.MapPost("/api/alerts/{id}/ack", async (AppDb db, long id) =>
{
    var a = await db.Alerts.FindAsync(id);
    if (a is null) return Results.NotFound();
    a.Acknowledged = true; await db.SaveChangesAsync(); return Results.Ok(a);
});
app.MapPost("/api/thresholds", async (AppDb db, Threshold t) => { db.Thresholds.Add(t); await db.SaveChangesAsync(); return Results.Ok(t); });
app.MapDelete("/api/thresholds/{id}", async (AppDb db, int id) =>
{
    var t = await db.Thresholds.FindAsync(id);
    if (t is null) return Results.NotFound();
    db.Thresholds.Remove(t); await db.SaveChangesAsync(); return Results.Ok();
});

app.Map("/ingest", async (HttpContext ctx, AppDb db, Hub hub, AlertEngine engine) =>
{
    if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }
    using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
    var buf = new byte[4096];
    while (ws.State == WebSocketState.Open)
    {
        var r = await ws.ReceiveAsync(buf, CancellationToken.None);
        if (r.MessageType == WebSocketMessageType.Close) break;
        var text = Encoding.UTF8.GetString(buf, 0, r.Count);
        IngestFrame? frame;
        try { frame = JsonSerializer.Deserialize<IngestFrame>(text, jsonOpts); }
        catch { continue; }
        if (frame is null) continue;

        var dev = await db.Devices.FindAsync(frame.DeviceId);
        if (dev is null)
        {
            dev = new Device { Id = frame.DeviceId, Name = frame.DeviceId, Type = frame.Type, Location = "unknown", LastSeen = DateTime.UtcNow };
            db.Devices.Add(dev);
        }
        else dev.LastSeen = DateTime.UtcNow;

        var reading = new Reading
        {
            DeviceId = frame.DeviceId,
            Type = frame.Type,
            State = frame.State,
            Value = frame.Value,
            Timestamp = frame.Ts.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(frame.Ts.Value).UtcDateTime : DateTime.UtcNow
        };
        db.Readings.Add(reading);
        await db.SaveChangesAsync();
        await hub.Broadcast(new LiveEvent("reading", reading));
        await engine.Evaluate(reading);
    }
});

app.Map("/live", async (HttpContext ctx, Hub hub) =>
{
    if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }
    using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
    await hub.Attach(ws, ctx.RequestAborted);
});

app.Run();
