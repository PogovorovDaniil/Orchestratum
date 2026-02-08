namespace Orchestratum.Tests;

public class OrchestratumExceptionTests
{
    [Fact]
    public void Constructor_WithMessage_ShouldSetMessage()
    {
        // Arrange
        var message = "Test error message";

        // Act
        var exception = new OrchestratumException(message);

        // Assert
        exception.Message.Should().Be(message);
    }

    [Fact]
    public void Constructor_WithMessageAndInnerException_ShouldSetBoth()
    {
        // Arrange
        var message = "Test error message";
        var innerException = new InvalidOperationException("Inner exception");

        // Act
        var exception = new OrchestratumException(message, innerException);

        // Assert
        exception.Message.Should().Be(message);
        exception.InnerException.Should().BeSameAs(innerException);
    }

    [Fact]
    public void Exception_ShouldBeThrowable()
    {
        // Arrange
        var message = "Test error";

        // Act & Assert
        Action act = () => throw new OrchestratumException(message);
        act.Should().Throw<OrchestratumException>()
            .WithMessage(message);
    }

    [Fact]
    public void Exception_ShouldBeCatchableAsException()
    {
        // Arrange
        var message = "Test error";
        Exception? caughtException = null;

        // Act
        try
        {
            throw new OrchestratumException(message);
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        // Assert
        caughtException.Should().NotBeNull();
        caughtException.Should().BeOfType<OrchestratumException>();
    }
}
