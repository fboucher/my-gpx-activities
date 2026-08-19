namespace my_gpx_activities.ApiService.Services;

public class ActivityMetricsResult
{
    public double DistanceMeters { get; set; }
    public double ElevationGainMeters { get; set; }
    public double ElevationLossMeters { get; set; }
    public double AverageSpeedMs { get; set; }
    public double MaxSpeedMs { get; set; }
}

public interface IActivityMetricsService
{
    ActivityMetricsResult CalculateMetrics(
        List<double> latitudes,
        List<double> longitudes,
        List<double?> elevations,
        List<DateTime> timestamps);
}

public class ActivityMetricsService : IActivityMetricsService
{
    public ActivityMetricsResult CalculateMetrics(
        List<double> latitudes,
        List<double> longitudes,
        List<double?> elevations,
        List<DateTime> timestamps)
    {
        var count = latitudes.Count;
        if (count < 2)
            return new ActivityMetricsResult();

        double totalDistance = 0;
        for (int i = 1; i < count; i++)
        {
            totalDistance += CalculateDistance(
                latitudes[i - 1], longitudes[i - 1],
                latitudes[i], longitudes[i]);
        }

        double elevationGain = 0;
        double elevationLoss = 0;
        for (int i = 1; i < count; i++)
        {
            if (elevations[i - 1].HasValue && elevations[i].HasValue)
            {
                var diff = elevations[i]!.Value - elevations[i - 1]!.Value;
                if (diff > 0)
                    elevationGain += diff;
                else
                    elevationLoss += Math.Abs(diff);
            }
        }

        var duration = timestamps.Last() - timestamps.First();
        var averageSpeed = duration.TotalSeconds > 0
            ? totalDistance / duration.TotalSeconds
            : 0.0;

        double maxSpeed = 0;
        for (int i = 1; i < count; i++)
        {
            var distance = CalculateDistance(
                latitudes[i - 1], longitudes[i - 1],
                latitudes[i], longitudes[i]);
            var timeDiff = (timestamps[i] - timestamps[i - 1]).TotalSeconds;
            if (timeDiff > 0)
            {
                var speed = distance / timeDiff;
                if (speed > maxSpeed)
                    maxSpeed = speed;
            }
        }

        return new ActivityMetricsResult
        {
            DistanceMeters = totalDistance,
            ElevationGainMeters = elevationGain,
            ElevationLossMeters = elevationLoss,
            AverageSpeedMs = averageSpeed,
            MaxSpeedMs = maxSpeed
        };
    }

    private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000;
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
