using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace MyMediaPlayer.Models
{
    public class MediaItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public DateTime DateAdded { get; set; } = DateTime.Now;

        [JsonIgnore]
        public bool IsPlaying { get; set; }

        [JsonIgnore]
        public string DurationText => Duration.TotalHours >= 1 
            ? Duration.ToString(@"hh\:mm\:ss") 
            : Duration.ToString(@"mm\:ss");
    }

    public class Playlist
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "New Playlist";
        public List<MediaItem> Items { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime ModifiedAt { get; set; } = DateTime.Now;
    }

    public class Collection
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "New Collection";
        public Playlist? Playlist { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class PlaybackData
    {
        public Dictionary<string, double> MediaPositions { get; set; } = new();
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }

    public class AppSettings
    {
        public bool IsDarkTheme { get; set; } = false;
        public string? LastCollectionId { get; set; }
        public double Volume { get; set; } = 0.5;
    }

    public enum LogLevel
    {
        Info,
        Warning,
        Error
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public LogLevel Level { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
