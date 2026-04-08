using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LibVLCSharp.WPF;
using LibVLCSharp.Shared;
using MyMediaPlayer.Models;
using MyMediaPlayer.Services;
using MyMediaPlayer.ViewModels;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace MyMediaPlayer;

public partial class MainWindow : Window
{
    private MainViewModel _viewModel = null!;
    private readonly DispatcherTimer _positionTimer;
    private bool _isDraggingSlider = false;
    private bool _isInitialized = false;
    private LibVLC? _libVLC;
    private MediaPlayer? _mediaPlayer;

    private WindowState _previousWindowState = WindowState.Normal;
    private WindowStyle _previousWindowStyle;
    private ResizeMode _previousResizeMode;
    private bool _isFullscreen = false;

    public MainWindow()
    {
        InitializeComponent();
        
        try
        {
            LoggingService.Instance.LogInfo("Initializing LibVLC...");
            Core.Initialize();
            LoggingService.Instance.LogInfo("Core initialized");
            
            _libVLC = new LibVLC();
            LoggingService.Instance.LogInfo($"LibVLC version: {_libVLC.Version}");
            
            _mediaPlayer = new MediaPlayer(_libVLC);
            LoggingService.Instance.LogInfo("MediaPlayer created");
            
            InnerVideoView.MediaPlayer = _mediaPlayer;
            LoggingService.Instance.LogInfo("VideoView connected to MediaPlayer");

            _mediaPlayer.EndReached += MediaPlayer_EndReached;
            _mediaPlayer.Playing += MediaPlayer_Playing;
            LoggingService.Instance.LogInfo("MediaPlayer event handlers attached");
            
            // Handle the Stopped event to know when it's safe to dispose
            _mediaPlayer.Stopped += MediaPlayer_Stopped;
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Failed to initialize media components", ex);
        }

        _positionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _positionTimer.Tick += PositionTimer_Tick;
    }

    private bool _isClosing = false; // Guard flag to prevent events during shutdown

    private void MediaPlayer_Playing(object? sender, EventArgs e)
    {
        if (_isClosing) return;
        Dispatcher.BeginInvoke(() =>
        {
            if (_isClosing || _mediaPlayer == null) return;
            if (_mediaPlayer.Length > 0)
            {
                _viewModel.TotalDuration = TimeSpan.FromMilliseconds(_mediaPlayer.Length);
                TotalTimeText.Text = _viewModel.TotalDurationText;
                ProgressSlider.Value = 0;
                _positionTimer.Start();
            }
        });
    }

    private void MediaPlayer_EndReached(object? sender, EventArgs e)
    {
        if (_isClosing) return;
        Dispatcher.BeginInvoke(() =>
        {
            if (_isClosing) return;
            _positionTimer.Stop();
            _viewModel.OnMediaEnded();
        });
    }

    private void MediaPlayer_Stopped(object? sender, EventArgs e)
    {
        // No-op during normal operation; used to signal shutdown is safe
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _viewModel = new MainViewModel
        {
            MediaPlayer = _mediaPlayer,
            LibVLC = _libVLC
        };
        DataContext = _viewModel;
        
        // Attach MediaPlayer to the embedded VideoView.
        // This tells VLC to render into our WPF control instead of
        // creating a separate floating Direct3D window.
        if (_mediaPlayer != null)
            InnerVideoView.MediaPlayer = _mediaPlayer;
        
        VolumeSlider.Value = _viewModel.Volume;
        if (_mediaPlayer != null)
            _mediaPlayer.Volume = (int)(_viewModel.Volume * 100);
        
        ApplyTheme(_viewModel.IsDarkTheme);
        _isInitialized = true;
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        LoggingService.Instance.LogInfo("Window closing starting...");
        
        // Set flag FIRST so all VLC event callbacks become no-ops immediately.
        // This breaks the potential deadlock: VLC Stop() fires events on VLC threads,
        // those events used Dispatcher.Invoke() which would wait for the UI thread,
        // but the UI thread is blocked here waiting for Stop() → deadlock.
        _isClosing = true;
        
        // Stop the position timer immediately
        try { _positionTimer.Stop(); } catch { }
        
        // Save state while still on UI thread
        try { _viewModel?.OnClosing(); } catch { }
        
        // CRITICAL: Detach MediaPlayer from VideoView FIRST.
        // This releases VLC's Direct3D surface and Win32 HWND handle,
        // which is what was preventing the window from closing.
            try { InnerVideoView.MediaPlayer = null; } catch { }
        
        // Detach VLC event handlers BEFORE calling Stop().
        // This ensures no VLC thread can try to Dispatcher.Invoke back onto the UI thread.
        var playerToDispose = _mediaPlayer;
        var libVlcToDispose = _libVLC;
        _mediaPlayer = null;
        _libVLC = null;
        
        if (playerToDispose != null)
        {
            try
            {
                playerToDispose.EndReached -= MediaPlayer_EndReached;
                playerToDispose.Playing -= MediaPlayer_Playing;
                playerToDispose.Stopped -= MediaPlayer_Stopped;
            }
            catch { }
        }
        
        // Shut down the WPF application (closes the window, ends the message loop)
        // Then do the heavy-weight VLC cleanup on a background thread so we don't freeze
        Application.Current.Shutdown();
        
        System.Threading.ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                playerToDispose?.Stop();
                playerToDispose?.Dispose();
            }
            catch { }
            
            try
            {
                libVlcToDispose?.Dispose();
            }
            catch { }
            
            // Force-exit to ensure no background VLC threads keep the process alive
            System.Threading.Thread.Sleep(200);
            Environment.Exit(0);
        });
    }

    private void PositionTimer_Tick(object? sender, EventArgs e)
    {
        if (_viewModel != null && _mediaPlayer != null && _mediaPlayer.Length > 0 && !_isDraggingSlider)
        {
            var total = _mediaPlayer.Length;
            if (total > 0)
            {
                ProgressSlider.Value = (double)_mediaPlayer.Time / total;
                _viewModel.CurrentPosition = TimeSpan.FromMilliseconds(_mediaPlayer.Time);
                _viewModel.TotalDuration = TimeSpan.FromMilliseconds(total);
                CurrentTimeText.Text = _viewModel.CurrentPositionText;
                TotalTimeText.Text = _viewModel.TotalDurationText;
            }

            if (_viewModel.CurrentMedia != null)
            {
                PlaylistDataGrid.SelectedItem = _viewModel.CurrentMedia;
                PlaylistDataGrid.CurrentItem = _viewModel.CurrentMedia;
                PlaylistDataGrid.ScrollIntoView(_viewModel.CurrentMedia);
                
                var row = PlaylistDataGrid.ItemContainerGenerator.ContainerFromItem(_viewModel.CurrentMedia) as DataGridRow;
                if (row != null)
                {
                    row.IsSelected = true;
                    row.Focus();
                }
            }
        }
    }

    private void VideoPlayer_MediaStarted(object? sender, EventArgs e)
    {
        if (_mediaPlayer != null && _mediaPlayer.Length > 0)
        {
            _viewModel.TotalDuration = TimeSpan.FromMilliseconds(_mediaPlayer.Length);
            TotalTimeText.Text = _viewModel.TotalDurationText;
            ProgressSlider.Value = 0;
            _positionTimer.Start();
        }
    }

    private void VideoPlayer_MediaEnded(object? sender, EventArgs e)
    {
        _positionTimer.Stop();
        _viewModel.OnMediaEnded();
    }

    private void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsPlaying)
        {
            _viewModel.Pause();
            PlayPauseIcon.Text = "\uE768";
        }
        else
        {
            _viewModel.Play();
            if (_viewModel.IsPlaying)
            {
                PlayPauseIcon.Text = "\uE769";
            }
        }
    }

    private void ProgressSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingSlider = true;
    }

    private void ProgressSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingSlider = false;
        _viewModel.Seek(ProgressSlider.Value);
    }

    private void ProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isDraggingSlider && _mediaPlayer != null && _mediaPlayer.Length > 0)
        {
            var total = _mediaPlayer.Length;
            var position = TimeSpan.FromMilliseconds(e.NewValue * total);
            CurrentTimeText.Text = position.TotalHours >= 1 
                ? position.ToString(@"hh\:mm\:ss") 
                : position.ToString(@"mm\:ss");
        }
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_viewModel != null)
        {
            _viewModel.Volume = e.NewValue;
            if (_mediaPlayer != null)
                _mediaPlayer.Volume = (int)(e.NewValue * 100);
        }
    }

    private void ToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.IsDarkTheme = !_viewModel.IsDarkTheme;
        ApplyTheme(_viewModel.IsDarkTheme);
    }

    private void ApplyTheme(bool isDark)
    {
        var resources = Application.Current.Resources;
        
        if (isDark)
        {
            resources["BackgroundBrush"] = new SolidColorBrush(Color.FromRgb(26, 26, 26));
            resources["SecondaryBrush"] = new SolidColorBrush(Color.FromRgb(45, 45, 45));
            resources["PrimaryBrush"] = new SolidColorBrush(Color.FromRgb(96, 205, 255));
            resources["TextBrush"] = new SolidColorBrush(Colors.White);
            resources["BorderBrush"] = new SolidColorBrush(Color.FromRgb(60, 60, 60));
            resources["HoverBrush"] = new SolidColorBrush(Color.FromRgb(60, 60, 60));
        }
        else
        {
            resources["BackgroundBrush"] = new SolidColorBrush(Colors.White);
            resources["SecondaryBrush"] = new SolidColorBrush(Color.FromRgb(243, 243, 243));
            resources["PrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0, 120, 212));
            resources["TextBrush"] = new SolidColorBrush(Color.FromRgb(26, 26, 26));
            resources["BorderBrush"] = new SolidColorBrush(Color.FromRgb(200, 200, 200));
            resources["HoverBrush"] = new SolidColorBrush(Color.FromRgb(220, 220, 220));
        }
    }

    private void ToggleLogPanel_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.IsLogPanelVisible = !_viewModel.IsLogPanelVisible;
    }

    private void CollectionsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CollectionsListBox.SelectedItem is Collection collection)
        {
            _viewModel.SelectedCollection = collection;
        }
    }

    private void RenameCollection_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedCollection == null) return;

        var dialog = new InputDialog("Rename Collection", "Enter new name:", _viewModel.SelectedCollection.Name);
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
        {
            _viewModel.RenameCollectionTo(dialog.InputText);
        }
    }

    private void PlaylistDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.SelectedMediaItem != null)
        {
            _viewModel.Play();
            PlayPauseIcon.Text = "\uE769";
        }
    }

    private void PlaylistDataGrid_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else if (e.Data.GetDataPresent(typeof(MediaItem)))
        {
            e.Effects = DragDropEffects.Move;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void PlaylistDataGrid_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null)
            {
                _viewModel.AddFilesToPlaylist(files);
            }
        }
        else if (e.Data.GetDataPresent(typeof(MediaItem)))
        {
            var item = e.Data.GetData(typeof(MediaItem)) as MediaItem;
            if (item != null)
            {
                var targetIndex = PlaylistDataGrid.SelectedIndex;
                var oldIndex = _viewModel.Playlist.IndexOf(item);
                if (oldIndex >= 0 && targetIndex >= 0 && oldIndex != targetIndex)
                {
                    _viewModel.MoveItem(oldIndex, targetIndex);
                }
            }
        }
    }

    private void VideoView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
	
		LoggingService.Instance.LogInfo($"VideoView_MouseDoubleClick click: ClickCount={e.ClickCount}");
		 
        if (e.ClickCount == 2 && _mediaPlayer != null && _viewModel?.CurrentMedia != null)
        {
            ToggleFullscreen();
        }
    }

    private void VideoView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        LoggingService.Instance.LogInfo($"VideoView click: ClickCount={e.ClickCount}");
    }

    private void VideoView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        LoggingService.Instance.LogInfo($"VideoView PreviewMouseLeftButtonDown: ClickCount={e.ClickCount}");
        if (e.ClickCount == 2 && _mediaPlayer != null && _viewModel?.CurrentMedia != null)
        {
            LoggingService.Instance.LogInfo("Entering fullscreen mode...");
            ToggleFullscreen();
        }
    }

    private void VideoOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        LoggingService.Instance.LogInfo($"VideoOverlay click: ClickCount={e.ClickCount}");
        if (e.ClickCount == 2)
        {
            LoggingService.Instance.LogInfo("Double-click detected, checking conditions...");
            if (_mediaPlayer != null)
            {
                LoggingService.Instance.LogInfo("_mediaPlayer is not null");
            }
            else
            {
                LoggingService.Instance.LogInfo("_mediaPlayer is null");
            }
            if (_viewModel?.CurrentMedia != null)
            {
                LoggingService.Instance.LogInfo($"_viewModel.CurrentMedia: {_viewModel.CurrentMedia.Title}");
            }
            else
            {
                LoggingService.Instance.LogInfo("_viewModel.CurrentMedia is null");
            }
            
            if (_mediaPlayer != null && _viewModel?.CurrentMedia != null)
            {
                LoggingService.Instance.LogInfo("Entering fullscreen mode...");
                ToggleFullscreen();
            }
        }
    }

    private void VideoBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        LoggingService.Instance.LogInfo($"VideoView double-click detected: ClickCount={e.ClickCount}, Position=({e.GetPosition(sender as IInputElement)})");
        if (e.ClickCount == 2 && _mediaPlayer != null && _viewModel?.CurrentMedia != null)
        {
            LoggingService.Instance.LogInfo("Entering fullscreen mode...");
            ToggleFullscreen();
        }
    }

    private void VideoPlayer_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        LoggingService.Instance.LogInfo("VideoPlayer_MouseDoubleClick : Double-click detected, checking conditions...");
		if (_mediaPlayer == null || _viewModel?.CurrentMedia == null) return;
        ToggleFullscreen();
    }

    private void ToggleFullscreen_Click(object sender, RoutedEventArgs e)
    {
        LoggingService.Instance.LogInfo("ToggleFullscreen_Click called");
        if (_mediaPlayer == null || _viewModel?.CurrentMedia == null) return;
        ToggleFullscreen();
    }

    private void ToggleFullscreen()
    {
        if (_isFullscreen)
        {
            ExitFullscreenMode();
        }
        else
        {
            EnterFullscreenMode();
        }
    }

    private void EnterFullscreenMode()
    {
        LoggingService.Instance.LogInfo("EnterFullscreenMode called");
        if (_mediaPlayer == null || _viewModel?.CurrentMedia == null) 
        {
            LoggingService.Instance.LogInfo("EnterFullscreenMode: conditions not met, returning");
            return;
        }
        
        _isFullscreen = true;
        _previousWindowState = WindowState;
        _previousWindowStyle = WindowStyle;
        _previousResizeMode = ResizeMode;
        
        WindowStyle = WindowStyle.None;
        WindowState = WindowState.Maximized;
        Background = Brushes.Black;
        
        HeaderBorder.Visibility = Visibility.Collapsed;
        FooterBorder.Visibility = Visibility.Collapsed;
        SidebarBorder.Visibility = Visibility.Collapsed;
        LogPanel.Visibility = Visibility.Collapsed;
        ContentGrid.Visibility = Visibility.Collapsed;
        
        VideoView.MediaPlayer = _mediaPlayer;
        VideoView.Visibility = Visibility.Visible;
        
        Activate();
        Focus();
        
        LoggingService.Instance.LogInfo("EnterFullscreenMode: simple fullscreen activated");
    }
    
    private Window? _fullscreenWindow;

    private void FullscreenOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ExitFullscreenMode();
        }
    }

    private void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        if (_isFullscreen && e.Key == Key.Escape)
        {
            ExitFullscreenMode();
        }
    }

    private void ExitFullscreenMode()
    {
        LoggingService.Instance.LogInfo("ExitFullscreenMode called");
        _isFullscreen = false;
        
        WindowStyle = _previousWindowStyle;
        WindowState = _previousWindowState;
        ResizeMode = _previousResizeMode;
        
        Background = null;
        
        HeaderBorder.Visibility = Visibility.Visible;
        FooterBorder.Visibility = Visibility.Visible;
        SidebarBorder.Visibility = Visibility.Visible;
        LogPanel.Visibility = Visibility.Collapsed;
        ContentGrid.Visibility = Visibility.Visible;
        
        VideoView.Visibility = Visibility.Collapsed;
        VideoView.MediaPlayer = null;
        InnerVideoView.MediaPlayer = _mediaPlayer;
        
        LoggingService.Instance.LogInfo("ExitFullscreenMode completed");
    }
}

public class InputDialog : Window
{
    private TextBox _textBox = null!;
    public string InputText => _textBox.Text;

    public InputDialog(string title, string prompt, string defaultValue = "")
    {
        Title = title;
        Width = 350;
        Height = 150;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var label = new Label { Content = prompt, Margin = new Thickness(10) };
        Grid.SetRow(label, 0);
        grid.Children.Add(label);

        _textBox = new TextBox { Text = defaultValue, Margin = new Thickness(10, 0, 10, 10) };
        Grid.SetRow(_textBox, 1);
        grid.Children.Add(_textBox);

        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var okButton = new Button { Content = "OK", Width = 80, Margin = new Thickness(0, 0, 10, 10) };
        okButton.Click += (s, e) => { DialogResult = true; Close(); };
        var cancelButton = new Button { Content = "Cancel", Width = 80, Margin = new Thickness(0, 0, 10, 10) };
        cancelButton.Click += (s, e) => { DialogResult = false; Close(); };
        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);
        Grid.SetRow(buttonPanel, 2);
        grid.Children.Add(buttonPanel);

        Content = grid;
    }
}
