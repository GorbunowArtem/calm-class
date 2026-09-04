namespace CalmClass.Application;

public interface IDummyService
{
    bool IsEven(int value);
}

public sealed class DummyService : IDummyService
{
    public bool IsEven(int value) => value % 2 == 0;
}
