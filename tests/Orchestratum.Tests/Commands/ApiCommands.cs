using Orchestratum.Contract;

namespace Orchestratum.Tests.Commands;

public record ApiData(string Endpoint, bool ShouldFail);

public class CallExternalApiCommand : OrchCommand<ApiData>
{
    public override int RetryCount => 2;
}
