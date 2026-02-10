using Orchestratum.Contract;

namespace Orchestratum.Tests.Commands;

public record VideoData(string VideoId, int ProcessingTimeMs);

public class ProcessVideoCommand : OrchCommand<VideoData> { }
