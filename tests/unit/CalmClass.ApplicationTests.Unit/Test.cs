namespace CalmClass.ApplicationTests.Unit;

using CalmClass.Application;

public class Test
{
    [Test]
    public async Task IsEven_WhenValueIsEven_ReturnsTrue()
    {
        // Arrange
        var sut = new DummyService();

        // Act
        var result = sut.IsEven(2);

        // Assert
        await Assert.That(result).IsTrue();
    }
}
