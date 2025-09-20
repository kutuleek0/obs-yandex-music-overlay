using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace SmtcHelper
{
    internal sealed class NowPlayingState
    {
        public string type { get; set; } = "now_playing";
        public string title { get; set; } = string.Empty;
        public string artist { get; set; } = string.Empty;
        public string album { get; set; } = string.Empty;
        public string? albumArtDataUrl { get; set; }
        public string state { get; set; } = "Unknown"; // Playing, Paused, Stopped
        public double positionSeconds { get; set; }
        public double durationSeconds { get; set; }
        public string? appId { get; set; }
    }

    class Program
    {
        private static GlobalSystemMediaTransportControlsSessionManager? _manager;
        private static GlobalSystemMediaTransportControlsSession? _currentSession;
        private static string[] _allowedPatterns = BuildAllowedPatterns();

        static async Task<int> Main(string[] args)
        {
            try
            {
                _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                _manager.CurrentSessionChanged += Manager_CurrentSessionChanged;
                _manager.SessionsChanged += Manager_SessionsChanged;
                _currentSession = _manager.GetCurrentSession();
                await PickMatchingSession();
                await AttachToSession(_currentSession);
                await EmitState("init");

                // Простой keep-alive, чтобы процесс не завершался
                await Task.Delay(-1);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return 1;
            }
        }

        private static async void Manager_CurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
        {
            _currentSession = sender.GetCurrentSession();
            await PickMatchingSession();
            await AttachToSession(_currentSession);
            await EmitState("session_changed");
        }

        private static async void Manager_SessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
        {
            await PickMatchingSession();
            await EmitState("sessions_changed");
        }

        private static async Task AttachToSession(GlobalSystemMediaTransportControlsSession? session)
        {
            if (session == null) return;
            session.MediaPropertiesChanged += async (s, e) => await EmitState("media_changed");
            session.TimelinePropertiesChanged += async (s, e) => await EmitState("timeline_changed");
            session.PlaybackInfoChanged += async (s, e) => await EmitState("playback_changed");
        }

        private static async Task EmitState(string reason)
        {
            var state = await ReadState();
            var json = JsonSerializer.Serialize(state, SmtcJsonContext.Default.NowPlayingState);
            Console.WriteLine(json);
            Console.Out.Flush();
        }

        private static async Task<NowPlayingState> ReadState()
        {
            var result = new NowPlayingState();
            var session = _currentSession;
            if (session == null)
            {
                result.state = "NoSession";
                return result;
            }

            try
            {
                var mediaProps = await session.TryGetMediaPropertiesAsync();
                var timeline = session.GetTimelineProperties();
                var playback = session.GetPlaybackInfo();

                result.title = mediaProps?.Title ?? string.Empty;
                result.artist = mediaProps?.Artist ?? string.Empty;
                result.album = mediaProps?.AlbumTitle ?? string.Empty;
                result.appId = session.SourceAppUserModelId;
                if (!IsAllowedAppId(result.appId))
                {
                    result.state = "NotYandex";
                    result.title = string.Empty;
                    result.artist = string.Empty;
                    result.albumArtDataUrl = null;
                    return result;
                }
                result.state = playback?.PlaybackStatus.ToString() ?? "Unknown";
                result.positionSeconds = timeline.Position.TotalSeconds;
                result.durationSeconds = (timeline.EndTime - timeline.StartTime).TotalSeconds;

                if (mediaProps?.Thumbnail != null)
                {
                    result.albumArtDataUrl = await ReadThumbnailDataUrl(mediaProps.Thumbnail);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
            }

            return result;
        }

        private static async Task PickMatchingSession()
        {
            if (_manager == null) return;
            var sessions = _manager.GetSessions();
            GlobalSystemMediaTransportControlsSession? preferred = null;
            foreach (var s in sessions)
            {
                var appId = s.SourceAppUserModelId;
                if (IsAllowedAppId(appId))
                {
                    preferred = s;
                    break;
                }
            }
            _currentSession = preferred ?? _manager.GetCurrentSession();
            await Task.CompletedTask;
        }

        private static string[] BuildAllowedPatterns()
        {
            var env = Environment.GetEnvironmentVariable("YM_ALLOW");
            if (!string.IsNullOrWhiteSpace(env))
            {
                var items = env.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < items.Length; i++) items[i] = items[i].Trim().ToLowerInvariant();
                return items.Length > 0 ? items : new[] { "yandex", "music" };
            }
            return new[] { "yandex", "music" };
        }

        private static bool IsAllowedAppId(string? appId)
        {
            if (string.IsNullOrEmpty(appId)) return false;
            var lowered = appId.ToLowerInvariant();
            // Все паттерны должны встречаться (логика AND)
            foreach (var p in _allowedPatterns)
            {
                if (!lowered.Contains(p)) return false;
            }
            return true;
        }

        private static async Task<string?> ReadThumbnailDataUrl(IRandomAccessStreamReference thumbRef)
        {
            try
            {
                using var stream = await thumbRef.OpenReadAsync();
                var size = (uint)stream.Size;
                var reader = new DataReader(stream);
                await reader.LoadAsync(size);
                var bytes = new byte[size];
                reader.ReadBytes(bytes);

                var mime = DetectMime(bytes);
                var base64 = Convert.ToBase64String(bytes);
                return $"data:{mime};base64,{base64}";
            }
            catch
            {
                return null;
            }
        }

        private static string DetectMime(byte[] bytes)
        {
            if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
                return "image/png";
            if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
                return "image/jpeg";
            if (bytes.Length >= 6 && bytes[0] == (byte)'G' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F')
                return "image/gif";
            return "application/octet-stream";
        }
    }
}



