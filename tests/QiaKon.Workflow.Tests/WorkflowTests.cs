using FluentAssertions;
using Moq;
using QiaKon.Workflow;
using QiaKon.Workflow.Abstractions;
using QiaKon.Workflow.Core;
using Xunit;

namespace QiaKon.Workflow.Tests;

public class WorkflowTests
{
    [Fact]
    public async Task Pipeline_ExecuteAsync_WithNoStages_ShouldReturnEmptyResult()
    {
        // Arrange
        var pipeline = new Pipeline("TestPipeline");

        // Act
        var result = await pipeline.ExecuteAsync(new WorkflowContext());

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Pipeline_AddStage_ShouldContainStage()
    {
        // Arrange
        var pipeline = new Pipeline("TestPipeline");
        var stage = new Stage("TestStage");

        // Act
        pipeline.AddStage(stage);

        // Assert
        pipeline.Stages.Should().HaveCount(1);
        pipeline.Stages[0].Name.Should().Be("TestStage");
    }

    [Fact]
    public void StepResult_Succeeded_ShouldIndicateSuccessfulExecution()
    {
        // Arrange & Act
        var result = StepResult.Succeeded();

        // Assert
        result.Status.Should().Be(StepResultStatus.Succeeded);
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void StepResult_Failed_ShouldContainErrorMessage()
    {
        // Arrange & Act
        var result = StepResult.Failed("Test error");

        // Assert
        result.Status.Should().Be(StepResultStatus.Failed);
        result.ErrorMessage.Should().Be("Test error");
    }

    [Fact]
    public void Stage_DefaultMode_ShouldBeSequential()
    {
        // Arrange & Act
        var stage = new Stage("TestStage");

        // Assert
        stage.Mode.Should().Be(StepMode.Sequential);
    }
}
