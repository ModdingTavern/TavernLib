using System;
using System.IO;
using System.Linq;
using NLog;
using NLog.Targets;

namespace TavernLib.Debugging;

/// <summary>
/// Archives unity-log.csv the same way TavernLauncher does for the client
/// and server it manages -- moved into an Archives/ folder next to itself,
/// sequence-numbered, capped at 20 files.
///
/// TavernLib runs inside the same process as the game, so unlike an
/// external launcher it can't just rename the file out from under
/// whatever's writing to it -- NLog's FileTarget holds it open
/// (KeepFileOpen=true). Instead this briefly clears and restores
/// LogManager.Configuration, which is the documented way to make NLog
/// release its target file handles; NLogCatcher.cs already does the same
/// kind of live reconfiguration elsewhere in this codebase. NLog recreates
/// the file on its own next log line once the path no longer exists.
/// </summary>
public static class LogArchiver
{
    // The literal registered target name is assumed to be "unity-log" to
    // match the file itself -- not confirmed against source, since the
    // actual NLog.config likely lives in StreamingAssets rather than
    // compiled C#. FindUnityLogTarget() falls back to searching by
    // filename if this guess is wrong, so this isn't load-bearing.
    private const string TargetName = "unity-log";
    private const int MaxArchiveFiles = 20;
    private const long SizeThresholdBytes = 10_000_000;

    public static string ArchiveLogNow()
    {
        var target = FindUnityLogTarget();
        if (target == null)
        {
            TavernLogger.Error("LogArchiver: couldn't find the unity-log target.");
            return null;
        }

        var logPath = RenderFilePath(target);
        if (string.IsNullOrEmpty(logPath) || !File.Exists(logPath))
            return null;

        var archivesDir = Path.Combine(Path.GetDirectoryName(logPath) ?? "", "Archives");
        Directory.CreateDirectory(archivesDir);

        var stem = Path.GetFileNameWithoutExtension(logPath);
        var ext = Path.GetExtension(logPath);
        var existing = Directory.GetFiles(archivesDir, $"{stem}.*{ext}")
            .Select(p => (path: p, num: TryParseSequenceNumber(p, stem, ext)))
            .Where(x => x.num.HasValue)
            .OrderBy(x => x.num)
            .ToList();

        while (existing.Count >= MaxArchiveFiles)
        {
            File.Delete(existing[0].path);
            existing.RemoveAt(0);
        }

        var nextNum = existing.Count > 0 ? existing[existing.Count - 1].num.Value + 1 : 0;
        var archivePath = Path.Combine(archivesDir, $"{stem}.{nextNum}{ext}");

        var config = LogManager.Configuration;
        try
        {
            LogManager.Configuration = null; // closes all targets, including the file handle
            File.Move(logPath, archivePath);
        }
        catch (Exception e)
        {
            TavernLogger.Error($"LogArchiver: failed to move log file: {e}");
            LogManager.Configuration = config;
            return null;
        }
        LogManager.Configuration = config;
        LogManager.ReconfigExistingLoggers();

        TavernLogger.Msg($"Archived log to {archivePath}");
        return archivePath;
    }

    public static bool ShouldAutoArchive()
    {
        var target = FindUnityLogTarget();
        var logPath = target != null ? RenderFilePath(target) : null;
        if (string.IsNullOrEmpty(logPath) || !File.Exists(logPath)) return false;
        return new FileInfo(logPath).Length >= SizeThresholdBytes;
    }

    private static FileTarget FindUnityLogTarget()
    {
        var config = LogManager.Configuration;
        if (config == null) return null;

        if (config.FindTargetByName(TargetName) is FileTarget named)
            return named;

        return config.AllTargets.OfType<FileTarget>()
            .FirstOrDefault(t => (RenderFilePath(t) ?? "")
                .EndsWith("unity-log.csv", StringComparison.OrdinalIgnoreCase));
    }

    private static string RenderFilePath(FileTarget target)
    {
        try { return target.FileName?.Render(LogEventInfo.CreateNullEvent()); }
        catch { return null; }
    }

    private static int? TryParseSequenceNumber(string path, string stem, string ext)
    {
        var name = Path.GetFileName(path);
        var prefix = stem + ".";
        if (!name.StartsWith(prefix) || !name.EndsWith(ext)) return null;
        var middle = name.Substring(prefix.Length, name.Length - prefix.Length - ext.Length);
        return int.TryParse(middle, out var n) ? n : null;
    }
}
