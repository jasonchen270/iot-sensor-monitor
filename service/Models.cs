using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace SensorMonitor;

public class Device
{
    [Key] public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Location { get; set; } = "";
    public DateTime LastSeen { get; set; }
    public List<Threshold> Thresholds { get; set; } = new();
}

public class Reading
{
    public long Id { get; set; }
    public string DeviceId { get; set; } = "";
    public string Type { get; set; } = "";
    public string State { get; set; } = "";
    public double? Value { get; set; }
    public DateTime Timestamp { get; set; }
}

public class Threshold
{
    public int Id { get; set; }
    public string DeviceId { get; set; } = "";
    public string Metric { get; set; } = "";
    public string Op { get; set; } = "";
    public double Value { get; set; }
    public string Severity { get; set; } = "warn";
}

public class Alert
{
    public long Id { get; set; }
    public string DeviceId { get; set; } = "";
    public string Message { get; set; } = "";
    public string Severity { get; set; } = "warn";
    public DateTime Timestamp { get; set; }
    public bool Acknowledged { get; set; }
}

public class AppDb : DbContext
{
    public AppDb(DbContextOptions<AppDb> opts) : base(opts) { }
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Reading> Readings => Set<Reading>();
    public DbSet<Threshold> Thresholds => Set<Threshold>();
    public DbSet<Alert> Alerts => Set<Alert>();
}

public record IngestFrame(string DeviceId, string Type, string State, double? Value, long? Ts);
public record LiveEvent(string Kind, object Payload);
