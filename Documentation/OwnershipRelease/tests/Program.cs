using System.ComponentModel.DataAnnotations;
using AmlScreening.Application.Interface;
using AmlScreening.Application.Security;
using AmlScreening.Infrastructure.Data;
using AmlScreening.Portal.ViewModels;
using Microsoft.EntityFrameworkCore;
using ScreeningApi.Application.Models;
using ScreeningApi.Application.Services;
using System.Reflection;
using System.Security.Claims;
using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Domain.Entities;
using ScreeningApi.Application.Interface.Repositories;
using ScreeningApi.Security;

var count = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name); count++;
}
bool Valid(params string?[] names)
{
    var model = new ScreeningBatchRequest { Names = names.ToList() };
    return Validator.TryValidateObject(model, new ValidationContext(model), new List<ValidationResult>(), true);
}
Check(Valid("b"), "One-character portal lookup is accepted");
Check(Valid("Ram Bahadur", "Bishal Bista", "Ali Maychou"), "Three complete names are accepted");
Check(!Valid("", " ", null), "Blank batch is rejected");
Check(!Valid("a", "b", "c", "d"), "More than three names is rejected");
Check(!Valid(new string('a',201)), "Overlong name is rejected");
var user = new TestUser();
var options = new DbContextOptionsBuilder<AmlScreeningDbContext>()
    .UseSqlServer("Server=localhost;Database=Unused;Integrated Security=true;TrustServerCertificate=true").Options;
using var db = new AmlScreeningDbContext(options,user);
var amlSql = db.AmlRecords.ToQueryString();
Check(amlSql.Contains("fn_VisibleAmlRecordIds"), "AML query translates the shared visibility function");
Check(db.Customers.ToQueryString().Contains("OwnerUserId"), "Customer query includes owner filtering");
Check(db.Transactions.ToQueryString().Contains("OwnerUserId"), "Transaction query includes owner filtering");
Check(db.CustomerKycResults.ToQueryString().Contains("OwnerUserId"), "KYC history query includes owner filtering");
Check(db.ComplianceAlerts.ToQueryString().Contains("OwnerUserId"), "Alert query restricts parent transaction ownership");
Check(db.RiskAssessmentFactors.ToQueryString().Contains("OwnerUserId"), "Assessment factor query restricts parent ownership");
user.UserId = 12;
Check(db.Customers.ToQueryString().Contains("12"), "Query filter uses current owner, not first cached owner");
user.Admin = false; user.ViewAll = true;
Check(!db.CanAccessAllClients, "ViewAll alone does not grant system-admin access");
user.Admin = true;
Check(db.CanAccessAllClients, "Admin plus ViewAll grants system-admin access");
var request = new CustomerKycRequest { ClientReference="REF-1", Customer = new() { FullName="Example Person", Occupation="BUSINESS" } };
request.NormalizeReference();
Check(request.CustomerReference=="REF-1", "ClientReference maps to stored customer reference");
var calculator = new CustomerKycRiskCalculator();
var rules = new[] { new KycRiskRule { FactorId=1,FactorCode="OCCUPATION",WeightPercent=100,RuleId=1,RuleVersion=1,ValueCode="BUSINESS",Score=20 } };
var bands = new[] { new KycRiskBand { Id=1,BandVersion=1,MinimumScore=0,MaximumScore=100,RiskLevel="LOW",Decision="ALLOW" } };
var result = calculator.Calculate(request,new CustomerScreeningDbResult(),rules,bands);
Check(result.RiskScore==20 && result.Decision=="ALLOW", "Approved weighted score and ALLOW band are preserved");
result = calculator.Calculate(request,new CustomerScreeningDbResult { IsPrivateMatch=true,HighestMatchScore=100 },rules,bands);
Check(result.Decision=="REVIEW" && result.AmlResult.Decision=="REVIEW", "Private match cannot produce ALLOW or inconsistent decisions");
bands[0].Decision="BLOCK";
result = calculator.Calculate(request,new CustomerScreeningDbResult(),rules,bands);
Check(result.Decision=="BLOCK" && result.AmlResult.Decision=="BLOCK", "Risk-band BLOCK is preserved with no name match");
Check(result.Factors.Count==1 && result.Factors[0].RuleId==1, "Result retains rule breakdown");
Check(db.Model.FindEntityType(typeof(AmlRecord))!.FindProperty(nameof(AmlRecord.Photo)) != null,
    "Existing photo storage remains mapped");
Check(db.Model.FindEntityType(typeof(AmlRecord))!.FindProperty(nameof(AmlRecord.HasPhoto)) == null,
    "HasPhoto is a display value, not a required database column");

var loginRepository = DispatchProxy.Create<IUserRepository, LoginRepositoryStub>();
var loginStub = (LoginRepositoryStub)(object)loginRepository;
var loginService = new AmlScreening.Application.Services.AuthenticationService(loginRepository, new PasswordStub());
Check(!(await loginService.LoginAsync("owner-a", "correct")).Success, "API-only account cannot sign in to portal");
loginStub.Account.CanAccessPortal = true;
var portalLogin = await loginService.LoginAsync("owner-a", "correct");
Check(portalLogin.Success && portalLogin.UserId == 51 && portalLogin.ClientId == 7,
    "Enabling portal access keeps the same UserId and ClientId");
Check(portalLogin.Role == "APIUser", "Portal access does not require replacing the API role or creating a user");
Check(!(await loginService.LoginAsync("owner-a", "wrong")).Success, "Portal access does not bypass password verification");
loginStub.Account.IsActive = false;
Check(!(await loginService.LoginAsync("owner-a", "correct")).Success, "Inactive account cannot sign in");

var mapping = new ActorRepositoryStub();
var resolver = new ApiActorResolver(mapping);
Check(await resolver.ResolveAsync(new ClaimsPrincipal(), default) is null && mapping.Calls == 0,
    "Anonymous caller is rejected before application lookup");
var applicationPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[] {
    new Claim("iss", "https://issuer.example"), new Claim("sub", "application-a"), new Claim("ClientId", "999") }, "test"));
var resolved = await resolver.ResolveAsync(applicationPrincipal, default);
Check(resolved?.UserId == 51 && resolved.ClientId == 7, "Owner/client come from application mapping, not supplied ClientId claim");
var duplicateSubject = new ClaimsPrincipal(new ClaimsIdentity(new[] {
    new Claim("iss", "https://issuer.example"), new Claim("sub", "a"), new Claim("sub", "b") }, "test"));
var callsBefore = mapping.Calls;
Check(await resolver.ResolveAsync(duplicateSubject, default) is null && mapping.Calls == callsBefore,
    "Ambiguous application subjects are rejected");
mapping.Result = null;
Check(await resolver.ResolveAsync(applicationPrincipal, default) is null, "Unmapped/disabled application is rejected");

var flowRepository = new FlowRepositoryStub();
var flowService = new CustomerKycService(flowRepository, calculator);
var flowActor = new ApiActor { ApiApplicationId=2,UserId=51,ClientId=7,CanManageCustomers=true,CanScreen=true };
await flowService.ProcessAsync(flowActor,request,default);
Check(string.Join(",",flowRepository.Session.Calls)=="customer,screen,setup,result,commit,dispose",
    "Customer, screening, risk, history, commit, dispose execute in order");
flowRepository = new FlowRepositoryStub { Session = new FlowSessionStub { FailResult = true } };
flowService = new CustomerKycService(flowRepository,calculator);
try { await flowService.ProcessAsync(flowActor,request,default); Check(false,"History failure must throw"); }
catch (InvalidOperationException) { }
Check(!flowRepository.Session.Calls.Contains("commit") && flowRepository.Session.Calls.Last()=="dispose",
    "History failure disposes the transaction without committing");
Console.WriteLine($"{count} checks passed. Unit checks/SQL translation only; no database writes or real-token HTTP tests performed.");

sealed class TestUser : ICurrentUserContext
{
 public bool IsAuthenticated => true;
 public bool Admin { get; set; }
 public bool ViewAll { get; set; }
 public bool IsAdministrator => Admin;
 public long UserId { get; set; } = 11;
 public int ClientId => 1;
 public string Username => "test-owner";
 public bool HasPermission(string permission) => permission==AppPermissions.AmlRecordsViewAll && ViewAll;
}

public class LoginRepositoryStub : DispatchProxy
{
    public User Account { get; } = new() { Id=51,ClientId=7,Username="owner-a",Role="APIUser",IsActive=true,CanAccessPortal=false };
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name switch
    {
        "GetUserAsync" => Task.FromResult<User?>(Account),
        "GetPermissionsAsync" => Task.FromResult<IReadOnlyList<string>>(new[] { "Customers.View" }),
        _ => throw new NotSupportedException(method?.Name)
    };
}
sealed class PasswordStub : IPasswordService
{
    public string Hash(string password) => throw new NotSupportedException();
    public bool Verify(string hash,string password) => password=="correct";
}
sealed class ActorRepositoryStub : IApiActorRepository
{
    public int Calls { get; private set; }
    public ApiActor? Result { get; set; } = new() { UserId=51,ClientId=7,ApiApplicationId=2 };
    public Task<ApiActor?> FindAsync(string issuer,string subject,CancellationToken ct) { Calls++; return Task.FromResult(Result); }
}
sealed class FlowRepositoryStub : ICustomerKycRepository
{
    public FlowSessionStub Session { get; set; } = new();
    public Task<ICustomerKycSession> BeginAsync(ApiActor actor,CancellationToken ct) => Task.FromResult<ICustomerKycSession>(Session);
    public Task<CustomerKycResponse?> GetResultAsync(ApiActor actor,long id,CancellationToken ct) => throw new NotSupportedException();
}
sealed class FlowSessionStub : ICustomerKycSession
{
    public List<string> Calls { get; } = [];
    public bool FailResult { get; set; }
    public Task<int> SaveCustomerAsync(CustomerKycRequest request,CancellationToken ct) { Calls.Add("customer");return Task.FromResult(1); }
    public Task<CustomerScreeningDbResult> ScreenAsync(string name,CancellationToken ct) { Calls.Add("screen");return Task.FromResult(new CustomerScreeningDbResult()); }
    public Task<(List<KycRiskRule> Rules,List<KycRiskBand> Bands)> GetApprovedSetupAsync(CancellationToken ct)
    {
        Calls.Add("setup");
        return Task.FromResult((new List<KycRiskRule> { new() { FactorId=1,FactorCode="OCCUPATION",WeightPercent=100,RuleId=1,RuleVersion=1,ValueCode="BUSINESS",Score=20 } },
            new List<KycRiskBand> { new() { Id=1,BandVersion=1,MinimumScore=0,MaximumScore=100,RiskLevel="LOW",Decision="ALLOW" } }));
    }
    public Task SaveResultAsync(CustomerKycResponse result,CancellationToken ct)
    { Calls.Add("result"); if(FailResult) throw new InvalidOperationException("Simulated history failure"); return Task.CompletedTask; }
    public Task CommitAsync(CancellationToken ct) { Calls.Add("commit"); return Task.CompletedTask; }
    public ValueTask DisposeAsync() { Calls.Add("dispose"); return ValueTask.CompletedTask; }
}
