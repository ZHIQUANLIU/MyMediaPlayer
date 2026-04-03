# MyMediaPlayer

A Windows desktop media player built with WPF and .NET 8.

## Features

- **Media Playback**: Supports MP3, MP4, AVI, WMV, WAV, M4A formats
- **Playlist Management**: Add, remove, clear, reorder (drag & drop), sort by title/duration/date
- **Collections**: Create, rename, delete collections to organize playlists
- **Position Persistence**: Saves playback position every 60 seconds and on pause/close
- **Logging**: Built-in logging system with UI viewer
- **Theme Support**: Light and Dark theme toggle

## Requirements

- Windows 10/11
- .NET 8.0 Runtime

## Installation

1. Download the latest release from `publish/` folder
2. Run `MyMediaPlayer.exe`

## Data Location

All data is stored in the application directory:
- `data/collections.json` - Saved playlists and collections
- `data/playback_data.json` - Last playback positions
- `data/settings.json` - User preferences
- `logs/app.log` - Application logs

## Usage

1. **Add Media**: Click "Add Files" to add media files to the playlist
2. **Create Collection**: Click "Add" in the Collections panel to create a new collection
3. **Play**: Double-click a media item or click the Play button
4. **Controls**: Use Play/Pause, Stop, Previous, Next buttons and volume slider
5. **Seek**: Use the progress slider to seek to a position
6. **View Logs**: Click the log icon in the title bar to toggle the log panel
7. **Change Theme**: Click the theme icon in the title bar

## Build from Source

```bash
cd MyMediaPlayer
dotnet build
dotnet publish -c Release -o ../publish
```

## License

MIT
