using ScreeningApi.Application.Models;
using ScreeningApi.Application.Services;
using ScreeningApi.Application.Interface.Repositories;

var calculator = new CustomerKycRiskCalculator();
var request = new CustomerKycRequest
{
    CustomerReference = "TEST-001",
    Customer = new() { FullName = "Example Customer", Occupation = " business ", SourceOfFunds = "SALARY" }
};
List<KycRiskRule> Rules() => [
    new() { FactorId=1, FactorCode="OCCUPATION", WeightPercent=30, RuleId=1, ValueCode="BUSINESS", Score=20 },
    new() { FactorId=2, FactorCode="CUSTOMER_PEP", WeightPercent=50, RuleId=2, ValueCode="FALSE", Score=0 },
    new() { FactorId=2, FactorCode="CUSTOMER_PEP", WeightPercent=50, RuleId=3, ValueCode="TRUE", Score=100 },
    new() { FactorId=3, FactorCode="SOURCE_OF_FUNDS", WeightPercent=20, RuleId=4, ValueCode="SALARY", Score=20 }
];
List<KycRiskBand> Bands() => [
    new() { Id=1, MinimumScore=0, MaximumScore=29, RiskLevel="LOW", Decision="ALLOW" },
    new() { Id=2, MinimumScore=30, MaximumScore=59, RiskLevel="MEDIUM", Decision="REVIEW" },
    new() { Id=3, MinimumScore=60, MaximumScore=79, RiskLevel="HIGH", Decision="REVIEW" },
    new() { Id=4, MinimumScore=80, MaximumScore=100, RiskLevel="CRITICAL", Decision="BLOCK" }
];
var count = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAILED: " + name);
    count++; Console.WriteLine("PASS: " + name);
}
void Reject(Action action, string name)
{
    try { action(); } catch (KycConfigurationException) { Check(true, name); return; }
    throw new Exception("FAILED: " + name);
}
var clear = calculator.Calculate(request, new(), Rules(), Bands());
Check(clear.RiskScore == 10 && clear.RiskLevel == "LOW" && clear.Decision == "Approve", "Weighted score and low band");
var un = calculator.Calculate(request, new() { IsUnMatch=true, HighestMatchScore=100 }, Rules(), Bands());
Check(un.RiskScore == 10 && un.Decision == "Review", "UN match overrides allow without conflating name score and risk score");
var pep = calculator.Calculate(request, new() { IsPep=true, HighestMatchScore=100 }, Rules(), Bands());
Check(pep.RiskScore == 60 && pep.RiskLevel == "HIGH", "PEP weight and exact band boundary");
Check(calculator.Calculate(request, new() { HighestMatchScore=100 }, Rules(), Bands()).Decision == "Review", "Unknown AML source cannot be cleared");
var missing = Rules(); missing.RemoveAll(r => r.FactorId == 2); missing.Add(new() { FactorId=2, FactorCode="CUSTOMER_PEP", WeightPercent=50 });
Reject(() => calculator.Calculate(request,new(),missing,Bands()), "Missing approved PEP rule rejected");
var duplicate = Rules(); duplicate.Add(duplicate[0]);
Reject(() => calculator.Calculate(request,new(),duplicate,Bands()), "Overlapping effective rules rejected");
var overlap = Bands(); overlap.Add(overlap[0]);
Reject(() => calculator.Calculate(request,new(),Rules(),overlap), "Overlapping bands rejected");
Reject(() => calculator.Calculate(request,new(),Rules(),[]), "Missing approved band rejected");
var fallback = Rules(); fallback[0].ValueCode="DEFAULT";
Check(calculator.Calculate(request,new(),fallback,Bands()).RiskScore==10, "Explicit DEFAULT rule supported");
var invalidWeights = Rules(); invalidWeights[0].WeightPercent=20;
Reject(() => calculator.Calculate(request,new(),invalidWeights,Bands()), "Incomplete weights rejected");
var block = Bands(); block[0].Decision="BLOCK";
Check(calculator.Calculate(request,new() { IsUnMatch=true },Rules(),block).Decision=="Block", "AML review never downgrades block");
foreach (var (score, level) in new[] { (0,"LOW"), (29,"LOW"), (30,"MEDIUM"), (59,"MEDIUM"), (60,"HIGH"), (79,"HIGH"), (80,"CRITICAL"), (100,"CRITICAL") })
{
    var boundaryRules = Rules(); boundaryRules.ForEach(r => r.Score=score);
    var boundary = calculator.Calculate(request,new(),boundaryRules,Bands());
    Check(boundary.RiskScore==score && boundary.RiskLevel==level, $"Band boundary {score}");
}
var exactAndDefault = Rules(); exactAndDefault.Add(new() { FactorId=1, FactorCode="OCCUPATION", WeightPercent=30, RuleId=8, ValueCode="DEFAULT", Score=100 });
Check(calculator.Calculate(request,new(),exactAndDefault,Bands()).RiskScore==10, "Exact rule takes precedence over DEFAULT");
var rounding = Rules(); rounding[0].Score=1; rounding[3].Score=1;
Check(calculator.Calculate(request,new(),rounding,Bands()).RiskScore==1, "Half scores round away from zero");
var session = new FakeSession(Rules(), Bands());
var service = new CustomerKycService(new FakeRepository(session), calculator);
await service.ProcessAsync(7, request, default);
Check(session.Events.SequenceEqual(new[] { "customer", "aml", "setup", "result", "commit", "dispose" }), "Workflow order and commit");
var failed = new FakeSession(missing, Bands());
try { await new CustomerKycService(new FakeRepository(failed),calculator).ProcessAsync(7,request,default); }
catch(KycConfigurationException) { }
Check(!failed.Events.Contains("commit") && !failed.Events.Contains("result") && failed.Events.Last()=="dispose", "Failure disposes transaction without committing");
Console.WriteLine($"{count} checks passed.");

sealed class FakeRepository(FakeSession session) : ICustomerKycRepository
{
    public Task<ICustomerKycSession> BeginAsync(int clientId, CancellationToken ct) => Task.FromResult<ICustomerKycSession>(session);
}
sealed class FakeSession(List<KycRiskRule> rules,List<KycRiskBand> bands) : ICustomerKycSession
{
    public List<string> Events { get; } = [];
    public Task<int> SaveCustomerAsync(CustomerKycRequest r,CancellationToken ct) { Events.Add("customer"); return Task.FromResult(1); }
    public Task<CustomerScreeningDbResult> ScreenAsync(string n,CancellationToken ct) { Events.Add("aml"); return Task.FromResult(new CustomerScreeningDbResult()); }
    public Task<(List<KycRiskRule> Rules,List<KycRiskBand> Bands)> GetApprovedSetupAsync(CancellationToken ct) { Events.Add("setup"); return Task.FromResult((rules,bands)); }
    public Task SaveResultAsync(CustomerKycResponse r,CancellationToken ct) { Events.Add("result"); return Task.CompletedTask; }
    public Task CommitAsync(CancellationToken ct) { Events.Add("commit"); return Task.CompletedTask; }
    public ValueTask DisposeAsync() { Events.Add("dispose"); return ValueTask.CompletedTask; }
}
