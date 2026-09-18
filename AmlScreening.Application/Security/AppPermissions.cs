public static class AppPermissions
{
    public const string ClaimType = "Permission";

    public static class Portal
    {
        public const string Access = "Portal.Access";
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

    public static class Compliance
    {
        public const string View = "Compliance.View";
        public const string Manage = "Compliance.Manage";
        public const string Approve = "Compliance.Approve";
    }

    public static class Risk
    {
        public const string View =
            "Risk.View";

        public const string Manage =
            "Risk.Manage";

        public const string Approve =
            "Risk.Approve";
    }


    public static class Api
    {
        public const string Access = "API.Access";
    }

    public static readonly string[] All =
    [
        Portal.Access,
        Users.Manage,
        Aml.View,
        Aml.Manage,
        Compliance.View,
        Compliance.Manage,
        Compliance.Approve,
        Risk.View,
        Risk.Manage,
        Risk.Approve,
        Api.Access
    ];

    public static readonly string[] AdminDefaults =
   [
       Portal.Access,

        Users.Manage,

        Aml.View,
        Aml.Manage,

        Compliance.View,
        Compliance.Manage,
        Compliance.Approve,

        Risk.View,
        Risk.Manage,
        Risk.Approve
   ];


    public static readonly string[] UserDefaults =
    [
        Portal.Access,

        Aml.View,
        Aml.Manage,

        Compliance.View,
        Compliance.Manage,

        Risk.View
    ];


    public static readonly string[] ApiUserDefaults =
    [
        Api.Access
    ];

}