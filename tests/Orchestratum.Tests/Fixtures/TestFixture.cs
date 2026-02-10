using Microsoft.Extensions.DependencyInjection;
using Orchestratum.Tests.Misc;

namespace Orchestratum.Tests.Fixtures;

public class TestFixture : TestApplication
{
    public override void ConfigureOrchestratum(OrchServiceConfiguration configuration)
    {
        configuration.RegisterCommands(typeof(TestFixture).Assembly);
        configuration.RegisterHandlers(typeof(TestFixture).Assembly);
    }
}
