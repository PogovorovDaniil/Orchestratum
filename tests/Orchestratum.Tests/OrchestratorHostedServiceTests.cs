namespace Orchestratum.Tests;

public class OrchestratorHostedServiceTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldCallSyncCommandsRepeatedly()
    {
        // Arrange
        var orchestratorMock = new Mock<IOrchestrator>();
        var cts = new CancellationTokenSource();
        int syncCallCount = 0;

        orchestratorMock
            .Setup(o => o.SyncCommands(It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                syncCallCount++;
                if (syncCallCount >= 3)
                {
                    cts.Cancel();
                }
            })
            .Returns(Task.CompletedTask);

        orchestratorMock
            .Setup(o => o.RunCommands(It.IsAny<CancellationToken>()));

        orchestratorMock
            .Setup(o => o.WaitPollingInterval(It.IsAny<CancellationToken>()))
            .Returns(Task.Delay(10));

        var hostedService = new Services.OrchestratorHostedService(orchestratorMock.Object);

        // Act
        await hostedService.StartAsync(cts.Token);
        await Task.Delay(200);
        await hostedService.StopAsync(CancellationToken.None);

        // Assert
        orchestratorMock.Verify(o => o.SyncCommands(It.IsAny<CancellationToken>()), Times.AtLeast(3));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCallRunCommandsRepeatedly()
    {
        // Arrange
        var orchestratorMock = new Mock<IOrchestrator>();
        var cts = new CancellationTokenSource();
        int runCallCount = 0;

        orchestratorMock
            .Setup(o => o.SyncCommands(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        orchestratorMock
            .Setup(o => o.RunCommands(It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                runCallCount++;
                if (runCallCount >= 3)
                {
                    cts.Cancel();
                }
            });

        orchestratorMock
            .Setup(o => o.WaitPollingInterval(It.IsAny<CancellationToken>()))
            .Returns(Task.Delay(10));

        var hostedService = new Services.OrchestratorHostedService(orchestratorMock.Object);

        // Act
        await hostedService.StartAsync(cts.Token);
        await Task.Delay(200);
        await hostedService.StopAsync(CancellationToken.None);

        // Assert
        orchestratorMock.Verify(o => o.RunCommands(It.IsAny<CancellationToken>()), Times.AtLeast(3));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCallWaitPollingIntervalRepeatedly()
    {
        // Arrange
        var orchestratorMock = new Mock<IOrchestrator>();
        var cts = new CancellationTokenSource();
        int waitCallCount = 0;

        orchestratorMock
            .Setup(o => o.SyncCommands(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        orchestratorMock
            .Setup(o => o.RunCommands(It.IsAny<CancellationToken>()));

        orchestratorMock
            .Setup(o => o.WaitPollingInterval(It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                waitCallCount++;
                if (waitCallCount >= 3)
                {
                    cts.Cancel();
                }
            })
            .Returns(Task.Delay(10));

        var hostedService = new Services.OrchestratorHostedService(orchestratorMock.Object);

        // Act
        await hostedService.StartAsync(cts.Token);
        await Task.Delay(200);
        await hostedService.StopAsync(CancellationToken.None);

        // Assert
        orchestratorMock.Verify(o => o.WaitPollingInterval(It.IsAny<CancellationToken>()), Times.AtLeast(3));
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_ShouldStop()
    {
        // Arrange
        var orchestratorMock = new Mock<IOrchestrator>();
        var cts = new CancellationTokenSource();

        orchestratorMock
            .Setup(o => o.SyncCommands(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        orchestratorMock
            .Setup(o => o.RunCommands(It.IsAny<CancellationToken>()));

        orchestratorMock
            .Setup(o => o.WaitPollingInterval(It.IsAny<CancellationToken>()))
            .Returns(Task.Delay(10));

        var hostedService = new Services.OrchestratorHostedService(orchestratorMock.Object);

        // Act
        await hostedService.StartAsync(cts.Token);
        await Task.Delay(50);
        cts.Cancel();
        await hostedService.StopAsync(CancellationToken.None);

        // Assert - should complete without hanging
        orchestratorMock.Verify(o => o.SyncCommands(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCallMethodsInCorrectOrder()
    {
        // Arrange
        var orchestratorMock = new Mock<IOrchestrator>();
        var cts = new CancellationTokenSource();
        var callSequence = new List<string>();

        orchestratorMock
            .Setup(o => o.SyncCommands(It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                callSequence.Add("SyncCommands");
                if (callSequence.Count >= 9)
                {
                    cts.Cancel();
                }
            })
            .Returns(Task.CompletedTask);

        orchestratorMock
            .Setup(o => o.RunCommands(It.IsAny<CancellationToken>()))
            .Callback(() => callSequence.Add("RunCommands"));

        orchestratorMock
            .Setup(o => o.WaitPollingInterval(It.IsAny<CancellationToken>()))
            .Callback(() => callSequence.Add("WaitPollingInterval"))
            .Returns(Task.Delay(10));

        var hostedService = new Services.OrchestratorHostedService(orchestratorMock.Object);

        // Act
        await hostedService.StartAsync(cts.Token);
        await Task.Delay(200);
        await hostedService.StopAsync(CancellationToken.None);

        // Assert
        callSequence.Should().ContainInOrder("SyncCommands", "RunCommands", "WaitPollingInterval");
        callSequence.Should().ContainInOrder(
            "SyncCommands", "RunCommands", "WaitPollingInterval",
            "SyncCommands", "RunCommands", "WaitPollingInterval"
        );
    }
}
