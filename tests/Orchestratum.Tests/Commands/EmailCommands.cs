using Orchestratum.Contract;

namespace Orchestratum.Tests.Commands;

public record EmailData(string To, string Subject, string Body);

public class SendEmailCommand : OrchCommand<EmailData>
{
    public override TimeSpan Timeout => TimeSpan.FromMinutes(1);
}
