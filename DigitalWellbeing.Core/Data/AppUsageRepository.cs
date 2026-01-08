using DigitalWellbeing.Core.Interfaces;
using DigitalWellbeing.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DigitalWellbeing.Core.Data;

public class AppUsageRepository : IAppUsageRepository
{
    private readonly string _logsFolderPath;

    public AppUsageRepository(string logsFolderPath)
    {
        _logsFolderPath = logsFolderPath;
    }

    private string GetFilePath(DateTime date) => Path.Combine(_logsFolderPath, $"{date:MM-dd-yyyy}.log");

    // Use FileShare.ReadWrite to allow both Service (Write) and App (Read) to access file simultaneously
    public async Task<List<AppUsage>> GetUsageForDateAsync(DateTime date)
    {
        string filePath = GetFilePath(date);
        List<AppUsage> usageList = new List<AppUsage>();

        if (!File.Exists(filePath)) return usageList;

        try
        {
            await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs, System.Text.Encoding.UTF8);
            
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                string[] parts = line.Split('\t');
                if (parts.Length >= 2)
                {
                    string processName = parts[0];
                    if (int.TryParse(parts[1], out int seconds))
                    {
                        string programName = parts.Length > 2 ? parts[2] : "";
                        usageList.Add(new AppUsage(processName, programName, TimeSpan.FromSeconds(seconds)));
                    }
                }
            }
        }
        catch (IOException ex)
        {
            Console.WriteLine($"Repository Read Error: {ex.Message}");
        }

        return usageList;
    }

    public async Task UpdateUsageAsync(DateTime date, List<AppUsage> entries)
    {
        string filePath = GetFilePath(date);
        
        Directory.CreateDirectory(_logsFolderPath);

        // Re-construct lines
        List<string> lines = new List<string>();
        foreach(var entry in entries)
        {
            int seconds = (int)entry.Duration.TotalSeconds;
            lines.Add($"{entry.ProcessName}\t{seconds}\t{entry.ProgramName}");
        }

        // Write with FileShare.Read to allow "Apps" to read while we write
        try 
        {
            await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(fs, System.Text.Encoding.UTF8);
            
            foreach (var line in lines)
            {
                await writer.WriteLineAsync(line);
            }
        }
        catch (IOException ex)
        {
            Console.WriteLine($"Repository Write Error: {ex.Message}");
            throw; 
        }
    }
}
