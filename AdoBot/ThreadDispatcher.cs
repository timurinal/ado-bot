namespace AdoBot;

public static class ThreadDispatcher
{
    public static Thread NetworkedRssThread { get; private set; }

    public static void Start()
    {
        RssWatcher.ThreadAlive = true;
        NetworkedRssThread = new Thread(() => RssWatcher.StartThread());
        NetworkedRssThread.Start();
    }

    public static void Exit()
    {
        RssWatcher.ThreadAlive = false;
    }
}