using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MyMediaPlayer.Models;
using MyMediaPlayer.Services;
using MyMediaPlayer.ViewModels;

namespace MyMediaPlayer;

public partial class MainWindow : Window
{
    private MainViewModel _viewModel = null!;
    private readonly DispatcherTimer _positionTimer;
    private bool _isDraggingSlider = false;
    private bool _isInitialized = false;

    public MainWindow()
    {
        InitializeComponent();
        _positionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _positionTimer.Tick += PositionTimer_Tick;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _viewModel = new MainViewModel
        {
            MediaElement = MediaPlayer
        };
        DataContext = _viewModel;
        
        VolumeSlider.Value = _viewModel.Volume;
        MediaPlayer.Volume = _viewModel.Volume;
        
        ApplyTheme(_viewModel.IsDarkTheme);
        _isInitialized = true;
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _viewModel?.OnClosing();
    }

    private void PositionTimer_Tick(object? sender, EventArgs e)
    {
        if (_viewModel != null && MediaPlayer.NaturalDuration.HasTimeSpan && !_isDraggingSlider)
        {
            var total = MediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
            if (total > 0)
            {
                ProgressSlider.Value = MediaPlayer.Position.TotalSeconds / total;
                _viewModel.CurrentPosition = MediaPlayer.Position;
                _viewModel.TotalDuration = MediaPlayer.NaturalDuration.TimeSpan;
                CurrentTimeText.Text = _viewModel.CurrentPositionText;
                TotalTimeText.Text = _viewModel.TotalDurationText;
            }
        }
    }

    private void MediaPlayer_MediaOpened(object sender, RoutedEventArgs e)
    {
        if (MediaPlayer.NaturalDuration.HasTimeSpan)
        {
            _viewModel.TotalDuration = MediaPlayer.NaturalDuration.TimeSpan;
            TotalTimeText.Text = _viewModel.TotalDurationText;
            ProgressSlider.Value = 0;
            _positionTimer.Start();
        }
    }

    private void MediaPlayer_MediaEnded(object sender, RoutedEventArgs e)
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
        if (_isDraggingSlider && MediaPlayer.NaturalDuration.HasTimeSpan)
        {
            var total = MediaPlayer.NaturalDuration.TimeSpan;
            var position = TimeSpan.FromSeconds(e.NewValue * total.TotalSeconds);
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
            MediaPlayer.Volume = e.NewValue;
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
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void PlaylistDataGrid_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(MediaItem)))
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
