using System.ComponentModel.DataAnnotations;

namespace my_gpx_activities.ApiService.Models;

public class ActivityType
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    [StringLength(50)]
    public string? Icon { get; set; }

    [StringLength(7)]
    public string? Color { get; set; }

    public bool IsDefault { get; set; } = false;
}