using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Wave;

namespace TTS3.Services
{
    /// <summary>
    /// Manages audio playback functionality
    /// </summary>
    public class AudioPlaybackService : IDisposable
    {
        private WaveOutEvent _waveOut;
        private AudioFileReader _currentAudioFile;
        private List<string> _playlist = new List<string>();
        private int _currentIndex = -1;

        public event EventHandler<PlaybackStateChangedEventArgs> PlaybackStateChanged;
        public event EventHandler PlaybackCompleted;

        public bool IsPlaying => _waveOut?.PlaybackState == PlaybackState.Playing;
        public bool IsPaused => _waveOut?.PlaybackState == PlaybackState.Paused;
        public int CurrentIndex => _currentIndex;
        public string CurrentFile => _currentIndex >= 0 && _currentIndex < _playlist.Count 
            ? _playlist[_currentIndex] : null;

        public TimeSpan CurrentPosition => _currentAudioFile?.CurrentTime ?? TimeSpan.Zero;
        public TimeSpan TotalDuration => _currentAudioFile?.TotalTime ?? TimeSpan.Zero;

        /// <summary>
        /// Load a playlist of audio files
        /// </summary>
        public void LoadPlaylist(List<string> files)
        {
            Stop();
            _playlist = new List<string>(files);
            _currentIndex = -1;
        }

        /// <summary>
        /// Play audio file at specified index
        /// </summary>
        public void Play(int index)
        {
            if (index < 0 || index >= _playlist.Count)
                return;

            try
            {
                Stop();

                string filePath = _playlist[index];
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"Audio file not found: {filePath}");
                }

                _currentIndex = index;
                _currentAudioFile = new AudioFileReader(filePath);
                _waveOut = new WaveOutEvent();
                _waveOut.Init(_currentAudioFile);
                _waveOut.PlaybackStopped += WaveOut_PlaybackStopped;
                _waveOut.Play();

                PlaybackStateChanged?.Invoke(this, new PlaybackStateChangedEventArgs(true, false));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Playback error: {ex.Message}");
                Stop();
            }
        }

        /// <summary>
        /// Resume playback if paused
        /// </summary>
        public void Resume()
        {
            if (_waveOut != null && _waveOut.PlaybackState == PlaybackState.Paused)
            {
                _waveOut.Play();
                PlaybackStateChanged?.Invoke(this, new PlaybackStateChangedEventArgs(true, false));
            }
        }

        /// <summary>
        /// Pause playback
        /// </summary>
        public void Pause()
        {
            if (_waveOut != null && _waveOut.PlaybackState == PlaybackState.Playing)
            {
                _waveOut.Pause();
                PlaybackStateChanged?.Invoke(this, new PlaybackStateChangedEventArgs(false, true));
            }
        }

        /// <summary>
        /// Stop playback
        /// </summary>
        public void Stop()
        {
            if (_waveOut != null)
            {
                _waveOut.Stop();
                _waveOut.Dispose();
                _waveOut = null;
            }

            if (_currentAudioFile != null)
            {
                _currentAudioFile.Dispose();
                _currentAudioFile = null;
            }

            PlaybackStateChanged?.Invoke(this, new PlaybackStateChangedEventArgs(false, false));
        }

        /// <summary>
        /// Play next file in playlist
        /// </summary>
        public void PlayNext()
        {
            if (_currentIndex < _playlist.Count - 1)
            {
                Play(_currentIndex + 1);
            }
        }

        /// <summary>
        /// Play previous file in playlist
        /// </summary>
        public void PlayPrevious()
        {
            if (_currentIndex > 0)
            {
                Play(_currentIndex - 1);
            }
        }

        /// <summary>
        /// Set playback volume (0.0 to 1.0)
        /// </summary>
        public void SetVolume(float volume)
        {
            if (_waveOut != null)
            {
                _waveOut.Volume = Math.Clamp(volume, 0f, 1f);
            }
        }

        /// <summary>
        /// Seek to position in current track
        /// </summary>
        public void Seek(TimeSpan position)
        {
            if (_currentAudioFile != null)
            {
                _currentAudioFile.CurrentTime = position;
            }
        }

        private void WaveOut_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            PlaybackCompleted?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            Stop();
        }
    }

    public class PlaybackStateChangedEventArgs : EventArgs
    {
        public bool IsPlaying { get; }
        public bool IsPaused { get; }

        public PlaybackStateChangedEventArgs(bool isPlaying, bool isPaused)
        {
            IsPlaying = isPlaying;
            IsPaused = isPaused;
        }
    }
}