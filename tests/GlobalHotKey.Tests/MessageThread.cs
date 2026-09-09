using System.Diagnostics;
using System.Runtime.ExceptionServices;

// Hotkey registrations and the registry are process-wide state; running these in parallel only buys flakiness.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace GlobalHotKey.Tests;

/// <summary>
/// RegisterHotKey wants the window, the registration and the message pump on one STA thread.
/// xUnit runs tests on MTA pool threads with no pump, so hotkey tests borrow a thread from here.
/// </summary>
static class MessageThread
{
    public static void Run(Action body)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try { body(); }
            catch (Exception ex) { failure = ExceptionDispatchInfo.Capture(ex); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        failure?.Throw();
    }

    public static bool PumpUntil(Func<bool> condition, TimeSpan timeout)
    {
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < timeout)
        {
            Application.DoEvents();
            if (condition()) return true;
            Thread.Sleep(5);
        }
        return condition();
    }
}
