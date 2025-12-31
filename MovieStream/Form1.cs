using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;
using LibVLCSharp.Shared;
using Microsoft.Web.WebView2.Core;

namespace MovieStream
{
    public sealed partial class Form1 : Form
    {
        // LibVLC
        private LibVLC _libVLC = null!;
        private MediaPlayer _mp = null!;
        private Media? _media;

        private bool _seekingFromUi;
        private string? _currentUrl;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
            int enabled = 1;
            _ = DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref enabled, sizeof(int));
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            int attr,
            ref int attrValue,
            int attrSize);

        public Form1()
        {
            InitializeComponent();

            Load += Form1_Load;
            FormClosing += Form1_FormClosing;

            uiTimer.Tick += (_, __) => PushPlayerStateToUi();
        }

        private async void Form1_Load(object? sender, EventArgs e)
        {
            uiView.BringToFront();

            Core.Initialize();
            _libVLC = new LibVLC();
            _mp = new MediaPlayer(_libVLC);
            videoView.MediaPlayer = _mp;

            // Push state on relevant player events
            _mp.Playing += (_, __) => SafeUiPush();
            _mp.Paused += (_, __) => SafeUiPush();
            _mp.Stopped += (_, __) => SafeUiPush();
            _mp.EndReached += (_, __) => SafeUiPush();
            _mp.TimeChanged += (_, __) => { if (!_seekingFromUi) SafeUiPush(); };

            await uiView.EnsureCoreWebView2Async();
            uiView.DefaultBackgroundColor = Color.Transparent;

            var baseDir = AppContext.BaseDirectory;
            uiView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "app",
                baseDir,
                CoreWebView2HostResourceAccessKind.DenyCors);

            uiView.CoreWebView2.WebMessageReceived += Ui_WebMessageReceived;
            uiView.Source = new Uri("https://app/ui/playerui.html");

            uiTimer.Start();
        }

        private void SafeUiPush()
        {
            try
            {
                if (!IsHandleCreated) return;
                if (InvokeRequired)
                {
                    BeginInvoke((Action)(() => PushPlayerStateToUi()));
                }
                else
                {
                    PushPlayerStateToUi();
                }
            }
            catch { }
        }

        private void Ui_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                using var doc = JsonDocument.Parse(e.WebMessageAsJson);
                var root = doc.RootElement;

                var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
                if (string.IsNullOrWhiteSpace(type)) return;

                switch (type)
                {
                    case "load":
                        {
                            var url = root.TryGetProperty("url", out var u) ? (u.GetString() ?? "").Trim() : "";
                            if (!Uri.TryCreate(url, UriKind.Absolute, out _))
                            {
                                PostToUi(new { type = "error", message = "Invalid URL." });
                                return;
                            }

                            PlayUrl(url);
                            break;
                        }

                    case "remove":
                        StopAndClear();
                        break;

                    case "play":
                        PlayOrResume();
                        break;

                    case "pause":
                        Pause();
                        break;

                    case "seekStart":
                        _seekingFromUi = true;
                        break;

                    case "seekEnd":
                        {
                            _seekingFromUi = false;
                            if (!root.TryGetProperty("pos", out var p)) return;

                            var pos = (float)p.GetDouble();
                            pos = Math.Clamp(pos, 0f, 1f);

                            var len = _mp.Length;
                            if (len > 0) _mp.Time = (long)(pos * len);
                            else _mp.Position = pos;

                            PushPlayerStateToUi();
                            break;
                        }

                    case "requestState":
                        PushPlayerStateToUi();
                        break;
                }
            }
            catch
            {
                PostToUi(new { type = "error", message = "Bad message from UI." });
            }
        }

        private void PlayOrResume()
        {
            if (_media == null)
            {
                PostToUi(new { type = "status", message = "No media loaded." });
                return;
            }

            try
            {
                _mp.SetPause(false);
                _mp.Play();
            }
            catch { }

            PushPlayerStateToUi();
        }

        private void Pause()
        {
            if (_media == null) return;

            try { _mp.SetPause(true); } catch { }
            PushPlayerStateToUi();
        }

        private void PlayUrl(string url)
        {
            _currentUrl = url;

            try { _mp.Stop(); } catch { }

            _media?.Dispose();
            _media = new Media(_libVLC, url, FromType.FromLocation);

            // Let VLC render embedded/external subtitles normally (no WebView subtitle overlay)
            // Do NOT set :no-spu here.

            _mp.Play(_media);

            PostToUi(new { type = "status", message = "Loading..." });
            PushPlayerStateToUi();
        }

        private void StopAndClear()
        {
            try { _mp.Stop(); } catch { }

            _media?.Dispose();
            _media = null;
            _currentUrl = null;

            PostToUi(new { type = "status", message = "" });
            PushPlayerStateToUi(reset: true);
        }

        private void PushPlayerStateToUi(bool reset = false)
        {
            if (uiView.CoreWebView2 == null) return;

            if (reset)
            {
                PostToUi(new
                {
                    type = "state",
                    hasMedia = false,
                    isPlaying = false,
                    timeMs = 0L,
                    lengthMs = 0L,
                    pos = 0.0
                });
                return;
            }

            var time = Math.Max(0, _mp.Time);
            var len = Math.Max(0, _mp.Length);
            var pos = (len > 0) ? (double)time / len : Math.Clamp(_mp.Position, 0f, 1f);

            if (_seekingFromUi) pos = -1;

            var state = _mp.State;
            var isPlaying = state == VLCState.Playing || state == VLCState.Buffering || state == VLCState.Opening;

            PostToUi(new
            {
                type = "state",
                hasMedia = _media != null,
                isPlaying,
                timeMs = time,
                lengthMs = len,
                pos
            });
        }

        private void PostToUi(object obj)
        {
            try
            {
                if (uiView.CoreWebView2 == null) return;

                var json = JsonSerializer.Serialize(obj);

                if (InvokeRequired)
                {
                    BeginInvoke((Action)(() =>
                    {
                        try { uiView.CoreWebView2?.PostWebMessageAsJson(json); } catch { }
                    }));
                }
                else
                {
                    uiView.CoreWebView2.PostWebMessageAsJson(json);
                }
            }
            catch { }
        }

        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            uiTimer.Stop();

            try { _mp?.Stop(); } catch { }
            try { _media?.Dispose(); } catch { }
            try { _mp?.Dispose(); } catch { }
            try { _libVLC?.Dispose(); } catch { }
        }
    }
}
