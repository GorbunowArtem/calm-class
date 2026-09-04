namespace CalmClass.Infrastructure;

public interface IDummy2Service
{
    bool IsEven(int value);
}

public sealed class Dummy2Service : IDummy2Service
{
    public bool IsEven(int value) => value % 2 == 0;
}
