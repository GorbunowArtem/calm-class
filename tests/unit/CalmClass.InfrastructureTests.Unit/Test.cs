namespace CalmClass.InfrastructureTests.Unit;

using CalmClass.Infrastructure;

public class Test
{
    [Test]
    public async Task IsEven_WhenValueIsEven_ReturnsTrue()
    {
        // Arrange
        var sut = new Dummy2Service();

        // Act
        var result = sut.IsEven(2);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsEven_WhenValueIsOdd_ReturnsFalse()
    {
        // Arrange
        var sut = new Dummy2Service();

        // Act
        var result = sut.IsEven(3);

        // Assert
        await Assert.That(result).IsFalse();
    }
}
