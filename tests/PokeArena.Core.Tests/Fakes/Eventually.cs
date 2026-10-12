namespace PokeArena.Core.Tests.Fakes;

/// <summary>Waits (in real time) for background loops to reach a state; fails after a few seconds.</summary>
public static class Eventually
{
    public static async Task TrueAsync(Func<bool> condition, string? because = null)
    {
        var deadline = Environment.TickCount64 + 5000;
        while (!condition())
        {
            if (Environment.TickCount64 > deadline)
            {
                Assert.Fail($"Condition not reached in time{(because is null ? "" : ": " + because)}");
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }
}
