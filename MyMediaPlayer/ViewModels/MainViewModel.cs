using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using LibVLCSharp.Shared;
using MyMediaPlayer.Models;
using MyMediaPlayer.Services;

namespace MyMediaPlayer.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly DataService _dataService;
        private readonly DispatcherTimer _positionSyncTimer;
        private readonly DispatcherTimer _uiUpdateTimer;

        public event PropertyChangedEventHandler? PropertyChanged;

        private Collection? _selectedCollection;
        public Collection? SelectedCollection
        {
            get => _selectedCollection;
            set
            {
                _selectedCollection = value;
                OnPropertyChanged();
                LoadPlaylist();
            }
        }

        private MediaItem? _selectedMediaItem;
        public MediaItem? SelectedMediaItem
        {
            get => _selectedMediaItem;
            set
            {
                _selectedMediaItem = value;
                OnPropertyChanged();
            }
        }

        private MediaItem? _currentMedia;
        public MediaItem? CurrentMedia
        {
            get => _currentMedia;
            set
            {
                _currentMedia = value;
                OnPropertyChanged();
            }
        }

        public LibVLC? LibVLC { get; set; }
        public MediaPlayer? MediaPlayer { get; set; }

        private bool _isPlaying;
        public bool IsPlaying
        {
            get => _isPlaying;
            set
            {
                _isPlaying = value;
                OnPropertyChanged();
            }
        }

        private TimeSpan _currentPosition;
        public TimeSpan CurrentPosition
        {
            get => _currentPosition;
            set
            {
                _currentPosition = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentPositionText));
            }
        }

        private TimeSpan _totalDuration;
        public TimeSpan TotalDuration
        {
            get => _totalDuration;
            set
            {
                _totalDuration = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalDurationText));
            }
        }

        private double _volume = 0.5;
        public double Volume
        {
            get => _volume;
            set
            {
                _volume = value;
                OnPropertyChanged();
                if (MediaPlayer != null)
                {
                    MediaPlayer.Volume = (int)(value * 100);
                }
            }
        }

        public string CurrentPositionText => CurrentPosition.TotalHours >= 1
            ? CurrentPosition.ToString(@"hh\:mm\:ss")
            : CurrentPosition.ToString(@"mm\:ss");

        public string TotalDurationText => TotalDuration.TotalHours >= 1
            ? TotalDuration.ToString(@"hh\:mm\:ss")
            : TotalDuration.ToString(@"mm\:ss");

        public ObservableCollection<Collection> Collections { get; } = new();
        public ObservableCollection<MediaItem> Playlist { get; } = new();
        public ObservableCollection<LogEntry> LogEntries { get; } = new();

        private bool _isDarkTheme;
        public bool IsDarkTheme
        {
            get => _isDarkTheme;
            set
            {
                _isDarkTheme = value;
                OnPropertyChanged();
                SaveSettings();
            }
        }

        private bool _isLogPanelVisible;
        public bool IsLogPanelVisible
        {
            get => _isLogPanelVisible;
            set
            {
                _isLogPanelVisible = value;
                OnPropertyChanged();
            }
        }

        private PlaybackData _playbackData;
        private AppSettings _settings;

        public ICommand PlayCommand { get; }
        public ICommand PauseCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand PreviousCommand { get; }
        public ICommand NextCommand { get; }
        public ICommand AddFilesCommand { get; }
        public ICommand RemoveItemCommand { get; }
        public ICommand ClearPlaylistCommand { get; }
        public ICommand AddCollectionCommand { get; }
        public ICommand DeleteCollectionCommand { get; }
        public ICommand RenameCollectionCommand { get; }
        public ICommand SortByTitleCommand { get; }
        public ICommand SortByDurationCommand { get; }
        public ICommand SortByDateCommand { get; }
        public ICommand ToggleThemeCommand { get; }
        public ICommand ToggleLogPanelCommand { get; }
        public ICommand ClearLogsCommand { get; }

        public MainViewModel()
        {
            _dataService = new DataService();
            
            PlayCommand = new RelayCommand(_ => Play(), _ => CanPlay());
            PauseCommand = new RelayCommand(_ => Pause(), _ => CanPause());
            StopCommand = new RelayCommand(_ => Stop(), _ => CanStop());
            PreviousCommand = new RelayCommand(_ => PlayPrevious(), _ => CanPlayPrevious());
            NextCommand = new RelayCommand(_ => PlayNext(), _ => CanPlayNext());
            AddFilesCommand = new RelayCommand(_ => AddFiles());
            RemoveItemCommand = new RelayCommand(_ => RemoveItem(), _ => SelectedMediaItem != null);
            ClearPlaylistCommand = new RelayCommand(_ => ClearPlaylist(), _ => Playlist.Any());
            AddCollectionCommand = new RelayCommand(_ => AddCollection());
            DeleteCollectionCommand = new RelayCommand(_ => DeleteCollection(), _ => SelectedCollection != null);
            RenameCollectionCommand = new RelayCommand(_ => RenameCollection(), _ => SelectedCollection != null);
            SortByTitleCommand = new RelayCommand(_ => SortByTitle());
            SortByDurationCommand = new RelayCommand(_ => SortByDuration());
            SortByDateCommand = new RelayCommand(_ => SortByDate());
            ToggleThemeCommand = new RelayCommand(_ => ToggleTheme());
            ToggleLogPanelCommand = new RelayCommand(_ => ToggleLogPanel());
            ClearLogsCommand = new RelayCommand(_ => ClearLogs());
            
            _positionSyncTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(60)
            };
            _positionSyncTimer.Tick += (s, e) => SyncPosition();

            _uiUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _uiUpdateTimer.Tick += (s, e) => UpdatePositionFromMedia();

            LoggingService.Instance.OnLogAdded += entry =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    LogEntries.Insert(0, entry);
                    if (LogEntries.Count > 500)
                        LogEntries.RemoveAt(LogEntries.Count - 1);
                });
            };

            LoadData();
            LoggingService.Instance.LogInfo("Application started");
        }

        private void LoadData()
        {
            var collections = _dataService.LoadCollections();
            foreach (var c in collections)
                Collections.Add(c);

            _settings = _dataService.LoadSettings();
            IsDarkTheme = _settings.IsDarkTheme;
            Volume = _settings.Volume;

            _playbackData = _dataService.LoadPlaybackData();

            if (Collections.Any())
            {
                var lastCollection = Collections.FirstOrDefault(c => c.Id == _settings.LastCollectionId);
                SelectedCollection = lastCollection ?? Collections.First();
            }

            foreach (var entry in LoggingService.Instance.LogEntries.Take(100))
            {
                LogEntries.Add(entry);
            }
        }

        private void SaveSettings()
        {
            _settings.IsDarkTheme = IsDarkTheme;
            _settings.Volume = Volume;
            if (SelectedCollection != null)
                _settings.LastCollectionId = SelectedCollection.Id;
            _dataService.SaveSettings(_settings);
        }

        private void SaveCollections()
        {
            _dataService.SaveCollections(Collections.ToList());
        }

        private void LoadPlaylist()
        {
            Playlist.Clear();
            SelectedMediaItem = null;
            CurrentMedia = null;
            if (SelectedCollection?.Playlist != null)
            {
                foreach (var item in SelectedCollection.Playlist.Items)
                    Playlist.Add(item);
            }
            LoggingService.Instance.LogInfo($"Loaded playlist: {SelectedCollection?.Name}");
        }

        private void SavePlaylist()
        {
            if (SelectedCollection != null)
            {
                SelectedCollection.Playlist = new Playlist
                {
                    Items = Playlist.ToList()
                };
                SelectedCollection.Playlist.ModifiedAt = DateTime.Now;
                SaveCollections();
            }
        }

        private void SyncPosition()
        {
            if (CurrentMedia != null && MediaPlayer != null && MediaPlayer.Length > 0)
            {
                _playbackData.MediaPositions[CurrentMedia.FilePath] = MediaPlayer.Time;
                _dataService.SavePlaybackData(_playbackData);
                LoggingService.Instance.LogInfo($"Position synced: {CurrentMedia.Title}");
            }
        }

        private void UpdatePositionFromMedia()
        {
            if (MediaPlayer != null && MediaPlayer.Length > 0 && IsPlaying)
            {
                CurrentPosition = TimeSpan.FromMilliseconds(MediaPlayer.Time);
                TotalDuration = TimeSpan.FromMilliseconds(MediaPlayer.Length);
            }
        }

        private bool CanPlay() => SelectedMediaItem != null || CurrentMedia != null;
        private bool CanPause() => IsPlaying;
        private bool CanStop() => MediaPlayer != null && (IsPlaying || CurrentPosition > TimeSpan.Zero);
        private bool CanPlayPrevious() => Playlist.Count > 0;
        private bool CanPlayNext() => Playlist.Count > 0;

        public void Play()
        {
            if (SelectedMediaItem != null)
            {
                LoadMedia(SelectedMediaItem);
            }
            else if (CurrentMedia != null && !IsPlaying)
            {
                MediaPlayer?.Play();
                IsPlaying = true;
                _positionSyncTimer.Start();
                _uiUpdateTimer.Start();
                LoggingService.Instance.LogInfo($"Playing: {CurrentMedia.Title}");
            }
        }

        private void LoadMedia(MediaItem item)
        {
            if (MediaPlayer == null || LibVLC == null) return;

            try
            {
                foreach (var m in Playlist)
                    m.IsPlaying = false;

                CurrentMedia = item;
                item.IsPlaying = true;
                SelectedMediaItem = item;
                
                MediaPlayer.Stop();
                
                using var media = new Media(LibVLC, item.FilePath, FromType.FromPath);
                if (!MediaPlayer.Play(media))
                {
                    LoggingService.Instance.LogError($"Failed to play: {item.Title}");
                    return;
                }
                MediaPlayer.Volume = (int)(Volume * 100);
                IsPlaying = true;

                _positionSyncTimer.Start();
                _uiUpdateTimer.Start();
                LoggingService.Instance.LogInfo($"Loaded media: {item.Title}");
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError($"Error loading media: {ex.Message}", ex);
            }
        }

        public void Pause()
        {
            if (MediaPlayer != null && IsPlaying)
            {
                MediaPlayer.Pause();
                IsPlaying = false;
                _positionSyncTimer.Stop();
                _uiUpdateTimer.Stop();
                SaveCurrentPosition();
                LoggingService.Instance.LogInfo($"Paused: {CurrentMedia?.Title}");
            }
        }

        public void Stop()
        {
            if (MediaPlayer != null)
            {
                MediaPlayer.Stop();
                IsPlaying = false;
                if (CurrentMedia != null)
                    CurrentMedia.IsPlaying = false;
                CurrentPosition = TimeSpan.Zero;
                _positionSyncTimer.Stop();
                _uiUpdateTimer.Stop();
                SaveCurrentPosition();
                LoggingService.Instance.LogInfo($"Stopped: {CurrentMedia?.Title}");
            }
        }

        private void SaveCurrentPosition()
        {
            if (CurrentMedia != null && MediaPlayer != null && MediaPlayer.Length > 0)
            {
                _playbackData.MediaPositions[CurrentMedia.FilePath] = MediaPlayer.Time;
                _dataService.SavePlaybackData(_playbackData);
            }
        }

        public void PlayPrevious()
        {
            if (Playlist.Count == 0) return;
            
            int targetIndex;
            if (CurrentMedia != null)
            {
                var index = Playlist.IndexOf(CurrentMedia);
                targetIndex = index > 0 ? index - 1 : Playlist.Count - 1;
            }
            else if (SelectedMediaItem != null)
            {
                var index = Playlist.IndexOf(SelectedMediaItem);
                targetIndex = index > 0 ? index - 1 : Playlist.Count - 1;
            }
            else
            {
                targetIndex = 0;
            }
            LoadMedia(Playlist[targetIndex]);
        }

        public void PlayNext()
        {
            if (Playlist.Count == 0) return;
            
            int targetIndex;
            if (CurrentMedia != null)
            {
                var index = Playlist.IndexOf(CurrentMedia);
                targetIndex = index < Playlist.Count - 1 ? index + 1 : 0;
            }
            else if (SelectedMediaItem != null)
            {
                var index = Playlist.IndexOf(SelectedMediaItem);
                targetIndex = index < Playlist.Count - 1 ? index + 1 : 0;
            }
            else
            {
                targetIndex = 0;
            }
            LoadMedia(Playlist[targetIndex]);
        }

        public void OnMediaEnded()
        {
            SaveCurrentPosition();
            PlayNext();
        }

        public void Seek(double position)
        {
            if (MediaPlayer != null && MediaPlayer.Length > 0)
            {
                var newTime = (long)(position * MediaPlayer.Length);
                MediaPlayer.Time = newTime;
                CurrentPosition = TimeSpan.FromMilliseconds(newTime);
            }
        }

        private void AddFiles()
        {
            var dialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Media Files|*.mp3;*.mp4;*.avi;*.wmv;*.wav;*.m4a;*.mkv|All Files|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                AddFilesToPlaylist(dialog.FileNames);
            }
        }

        public void AddFilesToPlaylist(string[] files)
        {
            var validExtensions = new[] { ".mp3", ".mp4", ".avi", ".wmv", ".wav", ".m4a", ".mkv" };
            var addedCount = 0;
            foreach (var file in files)
            {
                var ext = System.IO.Path.GetExtension(file).ToLower();
                if (validExtensions.Contains(ext))
                {
                    var item = new MediaItem
                    {
                        Title = System.IO.Path.GetFileNameWithoutExtension(file),
                        FilePath = file
                    };
                    Playlist.Add(item);
                    addedCount++;
                }
            }
            if (addedCount > 0)
            {
                SavePlaylist();
                LoggingService.Instance.LogInfo($"Added {addedCount} files to playlist");
            }
        }

        private void RemoveItem()
        {
            if (SelectedMediaItem != null)
            {
                var index = Playlist.IndexOf(SelectedMediaItem);
                Playlist.Remove(SelectedMediaItem);
                SavePlaylist();
                LoggingService.Instance.LogInfo($"Removed item from playlist");

                if (CurrentMedia == SelectedMediaItem)
                {
                    Stop();
                    CurrentMedia = null;
                }

                if (Playlist.Count > 0)
                {
                    SelectedMediaItem = Playlist[Math.Min(index, Playlist.Count - 1)];
                }
            }
        }

        private void ClearPlaylist()
        {
            Stop();
            Playlist.Clear();
            SavePlaylist();
            LoggingService.Instance.LogInfo("Cleared playlist");
        }

        private void AddCollection()
        {
            var collection = new Collection
            {
                Name = $"Collection {Collections.Count + 1}"
            };
            Collections.Add(collection);
            SelectedCollection = collection;
            SaveCollections();
            LoggingService.Instance.LogInfo($"Created collection: {collection.Name}");
        }

        private void DeleteCollection()
        {
            if (SelectedCollection != null)
            {
                var name = SelectedCollection.Name;
                Collections.Remove(SelectedCollection);
                SelectedCollection = Collections.FirstOrDefault();
                SaveCollections();
                LoggingService.Instance.LogInfo($"Deleted collection: {name}");
            }
        }

        private void RenameCollection()
        {
            if (SelectedCollection != null)
            {
                LoggingService.Instance.LogInfo($"Renamed collection: {SelectedCollection.Name}");
            }
        }

        public void RenameCollectionTo(string newName)
        {
            if (SelectedCollection != null && !string.IsNullOrWhiteSpace(newName))
            {
                SelectedCollection.Name = newName;
                SaveCollections();
                OnPropertyChanged(nameof(SelectedCollection));
                LoggingService.Instance.LogInfo($"Collection renamed to: {newName}");
            }
        }

        private void SortByTitle()
        {
            var sorted = Playlist.OrderBy(x => x.Title).ToList();
            var current = CurrentMedia;
            var selected = SelectedMediaItem;
            Playlist.Clear();
            foreach (var item in sorted)
                Playlist.Add(item);
            
            if (current != null)
                CurrentMedia = Playlist.FirstOrDefault(x => x.Id == current.Id);
            if (selected != null)
                SelectedMediaItem = Playlist.FirstOrDefault(x => x.Id == selected.Id);
            
            SavePlaylist();
            LoggingService.Instance.LogInfo("Sorted playlist by title");
        }

        private void SortByDuration()
        {
            var sorted = Playlist.OrderBy(x => x.Duration).ToList();
            var current = CurrentMedia;
            var selected = SelectedMediaItem;
            Playlist.Clear();
            foreach (var item in sorted)
                Playlist.Add(item);
            
            if (current != null)
                CurrentMedia = Playlist.FirstOrDefault(x => x.Id == current.Id);
            if (selected != null)
                SelectedMediaItem = Playlist.FirstOrDefault(x => x.Id == selected.Id);
            
            SavePlaylist();
            LoggingService.Instance.LogInfo("Sorted playlist by duration");
        }

        private void SortByDate()
        {
            var sorted = Playlist.OrderBy(x => x.DateAdded).ToList();
            var current = CurrentMedia;
            var selected = SelectedMediaItem;
            Playlist.Clear();
            foreach (var item in sorted)
                Playlist.Add(item);
            
            if (current != null)
                CurrentMedia = Playlist.FirstOrDefault(x => x.Id == current.Id);
            if (selected != null)
                SelectedMediaItem = Playlist.FirstOrDefault(x => x.Id == selected.Id);
            
            SavePlaylist();
            LoggingService.Instance.LogInfo("Sorted playlist by date");
        }

        private void ToggleTheme()
        {
            IsDarkTheme = !IsDarkTheme;
            LoggingService.Instance.LogInfo($"Theme changed to: {(IsDarkTheme ? "Dark" : "Light")}");
        }

        private void ToggleLogPanel()
        {
            IsLogPanelVisible = !IsLogPanelVisible;
        }

        private void ClearLogs()
        {
            LoggingService.Instance.ClearLogs();
            LogEntries.Clear();
            LoggingService.Instance.LogInfo("Logs cleared");
        }

        public void MoveItem(int oldIndex, int newIndex)
        {
            if (oldIndex < 0 || oldIndex >= Playlist.Count || newIndex < 0 || newIndex >= Playlist.Count)
                return;

            var item = Playlist[oldIndex];
            Playlist.RemoveAt(oldIndex);
            Playlist.Insert(newIndex, item);
            SavePlaylist();
        }

        public void OnClosing()
        {
            SaveCurrentPosition();
            SaveSettings();
            _positionSyncTimer.Stop();
            _uiUpdateTimer.Stop();
            LoggingService.Instance.LogInfo("Application closed");
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => _execute(parameter);
    }
}
