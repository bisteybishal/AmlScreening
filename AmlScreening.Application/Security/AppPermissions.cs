using AmlScreening.Domain.Entities;

public static class AppPermissions
{
    public const string ClaimType = "Permission";

    public static class Portal
    {
        public const string Access = "Portal.Access";
    }
    public static class Partners
    {
        public const string Manage = "Partners.Manage";
    }

    public static class Users
    {
        public const string Manage = "Users.Manage";
    }

    public static class Aml
    {
        public const string View = "AML.View";
        public const string Manage = "AML.Manage";
    }
    public static class AmlSource
    {
        public const string View = "AmlSource.View";

        public const string Sync = "AmlSource.Sync";
    }

    public static class Subscription
    {
        public const string View = "Subscription.View";
        public const string Manage = "Subscription.Manage";
    }
    public static class Compliance
    {
        public const string View = "Compliance.View";
        public const string Manage = "Compliance.Manage";
        public const string Approve = "Compliance.Approve";
    }
    public static class Customers
    {
        public const string View =
            "Customers.View";
    }

    public static class Transactions
    {
        public const string View =
            "Transactions.View";
    }
    public static class Screening
    {
        public const string View =
            "Screening.View";
    }

    public static class Alerts
    {
        public const string View =
            "Alerts.View";

        public const string Manage =
            "Alerts.Manage";
    }

    public static class Cases
    {
        public const string View =
            "Cases.View";

        public const string Manage =
            "Cases.Manage";
    }

    public static class Audit
    {
        public const string View =
            "Audit.View";
    }
    public static class RiskScore
    {
        public const string View =
            "RiskScore.View";

        public const string Manage =
            "RiskScore.Manage";

        public const string Approve =
            "RiskScore.Approve";

        public const string Calculate =
            "RiskScore.Calculate";
    }

    public static class RiskScoreSetup
    {
        public const string View =
            "RiskScoreSetup.View";

        public const string Manage =
            "RiskScoreSetup.Manage";

        public const string Approve =
            "RiskScoreSetup.Approve";
    }


    public static class Roles
    {
        public const string View =
            "Roles.View";
    }

    public static class Api
    {
        public const string Access = "API.Access";
    }
    public static readonly string[] All =
     [
         Portal.Access,

        Partners.Manage,

        Users.Manage,

        Aml.View,
        Aml.Manage,

        AmlSource.View,
        AmlSource.Sync,

        Subscription.View,
        Subscription.Manage,

        Compliance.View,
        Compliance.Manage,
        Compliance.Approve,

        RiskScore.View,
        RiskScore.Manage,
        RiskScore.Approve,
        RiskScore.Calculate,

        RiskScoreSetup.View,
        RiskScoreSetup.Manage,
        RiskScoreSetup.Approve,

        Customers.View,

        Transactions.View,

        Alerts.View,
        Alerts.Manage,

        Cases.View,
        Cases.Manage,

        Audit.View,
        Screening.View,
        Roles.View,
        Api.Access
     ];

    public static readonly string[] AdminDefaults =
[
    Portal.Access,

    Partners.Manage,
    Users.Manage,

    Aml.View,
    Aml.Manage,

    AmlSource.View,
    AmlSource.Sync,

    Subscription.View,
    Subscription.Manage,

    Compliance.View,
    Compliance.Manage,
    Compliance.Approve,

        RiskScore.View,
        RiskScore.Manage,
        RiskScore.Approve,
        RiskScore.Calculate,

        RiskScoreSetup.View,
        RiskScoreSetup.Manage,
        RiskScoreSetup.Approve,

    Customers.View,
    Transactions.View,

    Alerts.View,
    Alerts.Manage,

    Cases.View,
    Cases.Manage,
    Screening.View,
    Roles.View,

    Audit.View
];

    public static readonly string[] ApiUserDefaults =
[
    Api.Access
];

}