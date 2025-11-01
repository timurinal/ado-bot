namespace AdoBot;

public static class Config
{
    public const string Version = "1.1.0";
    public const string Id = "UCln9P4Qm3-EAY4aiEPmRwEA";
    
    public static string Token { get; private set; }
    
    public static ulong LogChannelId { get; private set; }
    public static ulong UpdateMessageChannelId { get; private set; }
    
    public static ulong Guild { get; private set;  }
    public static ulong RadioChannel { get; private set;  }
    
    public static string DefaultRadioStream { get; private set; }

    public static void Load()
    {
        if (File.Exists("config.conf") == false)
        {
            File.Create("config.conf").Close();
            throw new FileNotFoundException("Config file was not found. Assuming this is running for the first time, a blank config file will be created.");
        }
        
        var file = File.ReadAllText("config.conf");

        string[] lines = file.Split('\n');

        foreach (var line in lines)
        {
            // Not a fan of this, but it works ig.
            if (line.StartsWith("token=")) Token = line.Substring(6);
            
            if (line.StartsWith("logChannelId=")) LogChannelId = ulong.Parse(line.Substring(13));
            if (line.StartsWith("updateMessageChannelId=")) UpdateMessageChannelId = ulong.Parse(line.Substring(23));
            
            if (line.StartsWith("guild=")) Guild = ulong.Parse(line.Substring(6));
            if (line.StartsWith("radioChannel=")) RadioChannel = ulong.Parse(line.Substring(13));
            
            if (line.StartsWith("defaultRadioStream=")) DefaultRadioStream = line.Substring(19);
        }
    }
}