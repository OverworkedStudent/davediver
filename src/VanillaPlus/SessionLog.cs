using System;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Logging;

namespace VanillaPlus;

// BepInEx overwrites LogOutput.log on every launch. This keeps a copy of this plugin's own lines per
// session in BepInEx/VanillaPlus-logs so a play session can still be read after the game is restarted.
internal static class SessionLog
{
    private const int SessionsToKeep = 10;

    private static StreamWriter writer;

    public static void Start(ManualLogSource source)
    {
        try
        {
            string dir = Path.Combine(Paths.BepInExRootPath, "VanillaPlus-logs");
            Directory.CreateDirectory(dir);

            foreach (var old in new DirectoryInfo(dir).GetFiles("session-*.log")
                         .OrderByDescending(f => f.Name).Skip(SessionsToKeep - 1))
            {
                // A file that cannot be removed must not cost this session its log.
                try { old.Delete(); }
                catch (Exception) { }
            }

            writer = new StreamWriter(Path.Combine(dir, $"session-{DateTime.Now:yyyyMMdd-HHmmss}.log")) { AutoFlush = true };
            source.LogEvent += OnLog;
        }
        catch (Exception e)
        {
            source.LogWarning($"Session log unavailable: {e.Message}");
        }
    }

    private static void OnLog(object sender, LogEventArgs e)
    {
        // A full disk or a lost file must never reach the feature that was only trying to log a line.
        try
        {
            writer?.WriteLine($"{DateTime.Now:HH:mm:ss} [{e.Level}] {e.Data}");
        }
        catch (Exception)
        {
            writer = null;
        }
    }
}
