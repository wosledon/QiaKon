using FluentAssertions;
using QiaKon.Connector;
using Xunit;

namespace QiaKon.Connector.Tests;

public class ConnectorTests
{
    [Fact]
    public void ConnectorState_ShouldHaveExpectedValues()
    {
        // Assert
        Enum.GetValues<ConnectorState>().Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public void ConnectorType_ShouldHaveExpectedValues()
    {
        // Assert
        Enum.GetValues<ConnectorType>().Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public void HealthCheckResult_ShouldIndicateHealthyStatus()
    {
        // Arrange
        var result = new HealthCheckResult(true, "OK");

        // Assert
        result.IsHealthy.Should().BeTrue();
    }

    [Fact]
    public void HealthCheckResult_Unhealthy_ShouldContainError()
    {
        // Arrange
        var result = new HealthCheckResult(false, "Test error");

        // Assert
        result.IsHealthy.Should().BeFalse();
        result.Message.Should().Be("Test error");
    }
}
