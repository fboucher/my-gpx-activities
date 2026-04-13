namespace my_gpx_activities.ApiService.Models;

public record GlobalStatistics(
    int CurrentWeekStreak,
    int LongestWeekStreak,
    IEnumerable<DayActivityCount> ActivityDaysByWeek,
    IEnumerable<MonthActivityCount> ActivityDaysByMonth,
    IEnumerable<YearSummary> YearRecap,
    IEnumerable<SportCount> ActivityCountBySport
);

public record DayActivityCount(
    int WeekNumber,
    int Year,
    int DaysWithActivities
);

public record MonthActivityCount(
    int Month,
    int Year,
    int DaysWithActivities
);

public record YearSummary(
    int Year,
    int TotalActivities,
    double TotalDistanceKm,
    double TotalDurationMinutes
);

public record SportCount(
    string SportType,
    int Count
);
