namespace CRMS.Web.Intranet.Models;

public class DashboardSummary
{
    public int TotalApplications { get; set; }
    public int PendingApplications { get; set; }
    public int ApprovedThisMonth { get; set; }
    public int RejectedThisMonth { get; set; }
    public decimal TotalDisbursedAmount { get; set; }
    public decimal AverageProcessingDays { get; set; }
    public int MyPendingTasks { get; set; }
    public int OverdueApplications { get; set; }
    public List<ApplicationByStatus> ApplicationsByStatus { get; set; } = [];
    public List<RecentActivity> RecentActivities { get; set; } = [];
}

public class ApplicationByStatus
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

public class RecentActivity
{
    public Guid ApplicationId { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class PendingTask
{
    public Guid ApplicationId { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public string RequiredAction { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public bool IsOverdue => DueDate < DateTime.UtcNow;
    public decimal Amount { get; set; }
    public string ProductName { get; set; } = string.Empty;
}
