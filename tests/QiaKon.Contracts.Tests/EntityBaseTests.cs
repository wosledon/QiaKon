using FluentAssertions;
using QiaKon.Contracts;
using Xunit;

namespace QiaKon.Contracts.Tests;

public class EntityBaseTests
{
    [Fact]
    public void EntityBase_Id_ShouldBeInitializedAsEmpty()
    {
        // Arrange & Act
        var entity = new TestEntity();

        // Assert
        entity.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void EntityBase_Id_ShouldBeUnique()
    {
        // Arrange & Act
        var entity1 = new TestEntity();
        var entity2 = new TestEntity();

        // Assert
        entity1.Id.Should().NotBe(entity2.Id);
    }

    [Fact]
    public void EntityBase_Id_ShouldBeNonEmptyGuid()
    {
        // Arrange & Act
        var entity = new TestEntity();

        // Assert
        entity.Id.Should().NotBe(Guid.Empty);
    }

    private class TestEntity : EntityBase
    {
    }
}
