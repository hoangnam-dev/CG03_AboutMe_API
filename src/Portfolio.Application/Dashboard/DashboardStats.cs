namespace Portfolio.Application.Dashboard;

public sealed record DashboardStats(
    int Projects,
    int Certificates,
    int Resumes,
    int UnreadContacts);
