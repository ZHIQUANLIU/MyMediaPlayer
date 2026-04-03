# MyMediaPlayer Specification

## Project Overview
- **Project Name**: MyMediaPlayer
- **Type**: Windows Desktop Application (WPF/.NET 8)
- **Core Functionality**: A media player supporting MP3, MP4, AVI formats with playlist management, collections, position persistence, logging, and theming

## UI/UX Specification

### Layout Structure
- **Main Window**: Single window with multiple panels
  - Title Bar: Custom with app icon, title, theme toggle, minimize/maximize/close
  - Left Panel: Collections list (250px width)
  - Center Panel: Playlist of current collection
  - Bottom Panel: Media player controls
  - Right Panel (toggle): Log viewer

### Visual Design
- **Color Palette**:
  - Light Theme:
    - Primary: #0078D4 (Windows Blue)
    - Secondary: #F3F3F3
    - Accent: #106EBE
    - Text: #1A1A1A
    - Background: #FFFFFF
  - Dark Theme:
    - Primary: #60CDFF
    - Secondary: #2D2D2D
    - Accent: #0078D4
    - Text: #FFFFFF
    - Background: #1A1A1A
- **Typography**: Segoe UI, 12px body, 14px headers
- **Spacing**: 8px base unit
- **Visual Effects**: Subtle shadows on panels, smooth transitions

### Components
1. **Collection Panel**: TreeView-style list with add/delete buttons
2. **Playlist Panel**: DataGrid with columns (Title, Duration, Path), drag-drop reorder
3. **Player Controls**: Play/Pause, Stop, Previous, Next, Volume slider, Progress slider, Time display
4. **Log Panel**: ScrollViewer with timestamp + message entries

## Functional Specification

### Core Features

1. **Media Playback**
   - Support formats: MP3, MP4, AVI, WMV, WAV, M4A
   - Play, Pause, Stop, Seek
   - Volume control (0-100%)
   - Display current time / total duration

2. **Playlist Management**
   - Add files (multi-select dialog)
   - Remove selected items
   - Clear playlist
   - Sort by: Title, Duration, Date Added
   - Drag-drop to reorder
   - Save playlist to collection
   - Auto-play next in list

3. **Collection Management**
   - Create new collection
   - Rename collection
   - Delete collection
   - Each collection contains one playlist

4. **Position Persistence**
   - Save current position on:
     - Media end
     - Media pause
     - Application close
     - Every 60 seconds during playback
   - Load last position on media load
   - Store in JSON file: `playback_data.json`

5. **Logging**
   - Log levels: INFO, WARNING, ERROR
   - Log events: Playback start/stop, Playlist changes, Collection changes, Errors
   - Store in: `logs/app.log` (rotating, max 5MB)
   - Log viewer UI to display and filter logs

6. **Theme Settings**
   - Toggle between Light/Dark theme
   - Persist theme preference
   - Apply to all UI elements

### Data Storage
- Collections stored in: `data/collections.json`
- Playback positions in: `data/playback_data.json`
- Settings in: `data/settings.json`
- Logs in: `logs/app.log`

## Acceptance Criteria
1. App launches without errors
2. Can add MP3/MP4/AVI files to playlist
3. Can play media with controls working
4. Playlist items can be sorted and reordered
5. Collections can be created/renamed/deleted
6. Playback position saves every 60 seconds
7. Position restores when reopening same media
8. Logs display in Log Viewer panel
9. Theme toggle switches between light/dark
10. All data persists across app restarts
