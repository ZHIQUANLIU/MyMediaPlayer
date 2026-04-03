using System;
using System.Collections.Generic;
using System.IO;
using MyMediaPlayer.Models;
using Newtonsoft.Json;

namespace MyMediaPlayer.Services
{
    public class DataService
    {
        private readonly string _dataFolder;
        private readonly string _collectionsFile;
        private readonly string _playbackFile;
        private readonly string _settingsFile;

        public DataService()
        {
            _dataFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
            Directory.CreateDirectory(_dataFolder);
            _collectionsFile = Path.Combine(_dataFolder, "collections.json");
            _playbackFile = Path.Combine(_dataFolder, "playback_data.json");
            _settingsFile = Path.Combine(_dataFolder, "settings.json");
        }

        public List<Collection> LoadCollections()
        {
            try
            {
                if (File.Exists(_collectionsFile))
                {
                    var json = File.ReadAllText(_collectionsFile);
                    return JsonConvert.DeserializeObject<List<Collection>>(json) ?? new List<Collection>();
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("Failed to load collections", ex);
            }
            return new List<Collection>();
        }

        public void SaveCollections(List<Collection> collections)
        {
            try
            {
                var json = JsonConvert.SerializeObject(collections, Formatting.Indented);
                File.WriteAllText(_collectionsFile, json);
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("Failed to save collections", ex);
            }
        }

        public PlaybackData LoadPlaybackData()
        {
            try
            {
                if (File.Exists(_playbackFile))
                {
                    var json = File.ReadAllText(_playbackFile);
                    return JsonConvert.DeserializeObject<PlaybackData>(json) ?? new PlaybackData();
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("Failed to load playback data", ex);
            }
            return new PlaybackData();
        }

        public void SavePlaybackData(PlaybackData data)
        {
            try
            {
                data.LastUpdated = DateTime.Now;
                var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(_playbackFile, json);
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("Failed to save playback data", ex);
            }
        }

        public AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFile))
                {
                    var json = File.ReadAllText(_settingsFile);
                    return JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("Failed to load settings", ex);
            }
            return new AppSettings();
        }

        public void SaveSettings(AppSettings settings)
        {
            try
            {
                var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(_settingsFile, json);
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("Failed to save settings", ex);
            }
        }
    }
}
