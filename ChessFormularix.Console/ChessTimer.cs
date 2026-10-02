using Timer = System.Timers.Timer;

namespace ChessFormularix.Console;

public class ChessTimer
{
    readonly Timer _timer;
    public TimeSpan RemainingTime { get; private set; }
    public event Action? OnTimeOut;
    public event Action? OnTimerTick;

    public ChessTimer(TimeSpan initialTime)
    {
        RemainingTime = initialTime;
        _timer = new Timer(1000);
        _timer.AutoReset = true;
        _timer.Elapsed += (_, _) => OnTick();
    }



    public void StopTimer()
    {
        _timer.Stop();
    }

    private void OnTick()
    {
        RemainingTime = RemainingTime > TimeSpan.FromSeconds(1)
            ? RemainingTime - TimeSpan.FromSeconds(1)
            : TimeSpan.Zero;

        OnTimerTick?.Invoke();

        if (RemainingTime <= TimeSpan.Zero)
        {
            _timer.Stop();
            OnTimeOut?.Invoke();
        }
    }
}
