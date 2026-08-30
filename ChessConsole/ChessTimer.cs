using Timer = System.Timers.Timer;

namespace ChessConsole;

public class ChessTimer
{
    Timer _timer;
    public TimeSpan RemainingTime { get; private set; }
    public event Action? OnTimeOut;
    public event Action? OnTimerTick;

    public ChessTimer(TimeSpan initialTime)
    {
        RemainingTime = initialTime;
        // Satu tick harus sama dengan satu detik permainan.
        _timer = new Timer(1000);
        _timer.AutoReset = true;
        _timer.Elapsed += (s, e) => OnTick();
    }

    public void ResetTimer(TimeSpan time)
    {
        RemainingTime = time;
    }

    public void StartTimer()
    {
        _timer.Start();
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
