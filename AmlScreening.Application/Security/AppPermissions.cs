namespace AmlScreening.Application.Security;

public static class AppPermissions
{
    public const string ClaimType = "Permission";

    public const string AmlRecordsView = "AMLRecords.View";
    public const string AmlRecordsCreate = "AMLRecords.Create";
    public const string AmlRecordsEdit = "AMLRecords.Edit";
    public const string AmlRecordsDelete = "AMLRecords.Delete";
    public const string AmlRecordsViewClient = "AMLRecords.ViewClient";
    public const string AmlRecordsViewAll = "AMLRecords.ViewAll";
    public const string AuditLogsView = "AuditLogs.View";
    public const string UsersView = "Users.View";
    public const string UsersCreate = "Users.Create";
    public const string RolesManage = "Roles.Manage";
    public const string SubscriptionView = "Subscription.View";
    public const string SubscriptionCreate = "Subscription.Create";
    public const string SubscriptionEdit = "Subscription.Edit";
    public const string SubscriptionDelete = "Subscription.Delete";
    public const string ComplianceRulesView = "ComplianceRules.View";

    public const string ComplianceRulesCreate = "ComplianceRules.Create";

    public const string ComplianceRulesEdit = "ComplianceRules.Edit";

    public const string ComplianceRulesDelete = "ComplianceRules.Delete";

    public const string CustomersView = "Customers.View";

    public const string TransactionsView = "Transactions.View";

    public const string AlertsView = "Alerts.View";

    public const string AlertsManage = "Alerts.Manage";

    public const string CasesView = "Cases.View";

    public const string CasesManage = "Cases.Manage";
    public const string AmlSourceView = "AmlSource.View";
    public const string AmlSourceSync = "AmlSource.Sync";

    public static readonly string[] All =
    {
        AmlRecordsView,
        AmlRecordsCreate,
        AmlRecordsEdit,
        AmlRecordsDelete,
        AmlRecordsViewClient,
        AmlRecordsViewAll,
        AuditLogsView,
        UsersView,
        UsersCreate,
        RolesManage,
        SubscriptionView,
        SubscriptionCreate,
        SubscriptionEdit,
        SubscriptionDelete,
        ComplianceRulesView,
        ComplianceRulesCreate,
        ComplianceRulesEdit,
        ComplianceRulesDelete,
        CustomersView,
        TransactionsView,
        AlertsView,
        AlertsManage,
        CasesView,
        CasesManage,
        AmlSourceView,
        AmlSourceSync
    };
}
