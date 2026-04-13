using System.ComponentModel.DataAnnotations;

namespace my_gpx_activities.ApiService.Models;

public class Activity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public DateTime StartDateTime { get; set; }

    [Required]
    public DateTime EndDateTime { get; set; }

    [Required]
    [StringLength(50)]
    public string ActivityType { get; set; } = string.Empty;

    public double DistanceMeters { get; set; }

    public double ElevationGainMeters { get; set; }

    public double ElevationLossMeters { get; set; }

    public double AverageSpeedMs { get; set; }

    public double MaxSpeedMs { get; set; }

    public int TrackPointCount { get; set; }

    // Stored as JSON array of [lat, lon] pairs
    public string? TrackCoordinatesJson { get; set; }

    // Stored as JSON array of [lat, lon, elevation_or_null, hr_or_null, unix_ms_or_null, cadence_or_null] per trackpoint
    // Note: older stored activities may have 5-element arrays (no cadence slot); consumers must handle both lengths.
    public string? TrackDataJson { get; set; }

    public double? AverageHeartRate { get; set; }
    public double? MaxHeartRate { get; set; }
    public double? Calories { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
