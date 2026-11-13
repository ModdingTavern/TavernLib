using System;
using System.IO;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace TavernLib.Backend.Server.Configs;

public abstract class ServerConfigFile<T>(string filePath) where T : class, new()
{
    private readonly object _sync = new();
    private string FilePath { get; set; } = filePath;
    private string FileName => Path.GetFileName(FilePath);

    public T LastRead { get; private set; } = new();

    // False until the file has been read once, and again whenever a read fails.
    public bool Readable { get; private set; }


    public virtual void ReadFromFile() => Read();

    // Reads the file into a new object and makes it LastRead. A missing file is created with the
    // defaults. An empty or unreadable file throws and LastRead stays as it was: it is never
    // taken for an empty config.
    public T Read()
    {
        lock (_sync)
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    var defaults = new T();
                    Write(defaults);
                    return Accept(defaults);
                }

                var text = File.ReadAllText(FilePath);
                if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException($"{FileName} is empty");

                return Accept(JsonConvert.DeserializeObject<T>(text) ?? throw new InvalidDataException($"{FileName} is empty"));
            }
            catch (Exception e)
            {
                Readable = false;
                TavernLogger.Error($"Could not read {FileName}: {e.Message}");
                throw;
            }
        }
    }

    public virtual void WriteToFile()
    {
        lock (_sync)
        {
            Write(LastRead ??= new T());
        }
    }

    // Reads the file, hands it to change and writes it back if change returns true, all under one
    // lock, so two changes can never interleave or drop each other.
    public void Update(Func<T, bool> change)
    {
        lock (_sync)
        {
            var config = Read();
            if (change(config)) Write(config);
        }
    }

    private T Accept(T config)
    {
        LastRead = config;
        Readable = true;
        return config;
    }

    // Writes a temp file, then swaps it in and keeps the previous file as .bak. A reader never sees
    // a half-written file.
    private void Write(T config)
    {
        try
        {
            var json = JsonConvert.SerializeObject(config, Formatting.Indented);
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));

            var temp = FilePath + ".tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var bytes = new UTF8Encoding(false).GetBytes(json);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }

            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak");
                    else File.Move(temp, FilePath);
                    return;
                }
                catch (IOException) when (attempt < 10)
                {
                    Thread.Sleep(50);
                }
            }
        }
        catch (Exception e)
        {
            TavernLogger.Error($"Could not write {FileName}: {e.Message}");
            throw;
        }
    }
}