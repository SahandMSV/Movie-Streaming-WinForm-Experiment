using System;
using System.Drawing;
using System.Text.Json;
using System.Windows.Forms;
using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Runtime.InteropServices;

namespace MovieStream
{
    public sealed partial class Form1 : Form
    {
        // LibVLC
        private LibVLC _libVLC = null!;
        private MediaPlayer _mp = null!;
        private Media? _media;

        // Layout
        private readonly TableLayoutPanel _root = new();
        private readonly VideoView _videoView = new();

        // HTML bars
        private readonly WebView2 _topBar = new();
        private readonly WebView2 _bottomBar = new();

        // Fix Timer ambiguity by fully qualifying
        private readonly System.Windows.Forms.Timer _uiTimer = new() { Interval = 200 };
        private bool _seekingFromUi;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e); // required when overriding [web:347]

            const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
            int enabled = 1;
            DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref enabled, sizeof(int));
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            int attr,
            ref int attrValue,
            int attrSize);

        public Form1()
        {
            Text = "MovieStream";
            Width = 1100;
            Height = 700;
            StartPosition = FormStartPosition.CenterScreen;

            BuildLayout();

            Load += Form1_Load;
            FormClosing += Form1_FormClosing;

            _uiTimer.Tick += (_, __) => PushPlayerStateToBottomBar();
        }

        private void BuildLayout()
        {
            BackColor = Color.FromArgb(17, 17, 17);
            _root.BackColor = BackColor;
            _root.Padding = Padding.Empty;
            _root.Margin = Padding.Empty;
            _root.Dock = DockStyle.Fill;
            _root.ColumnCount = 1;
            _root.RowCount = 3;
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));  // top bar
            _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // video
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));  // bottom bar
            Controls.Add(_root);

            _topBar.Dock = DockStyle.Fill;
            _topBar.Margin = Padding.Empty;
            _root.Controls.Add(_topBar, 0, 0);

            _videoView.Dock = DockStyle.Fill;
            _videoView.Margin = Padding.Empty;
            _videoView.BackColor = Color.Black;
            _root.Controls.Add(_videoView, 0, 1);

            _bottomBar.Dock = DockStyle.Fill;
            _bottomBar.Margin = Padding.Empty;
            _root.Controls.Add(_bottomBar, 0, 2);
        }

        private async void Form1_Load(object? sender, EventArgs e)
        {
            // LibVLC init
            Core.Initialize();
            _libVLC = new LibVLC();
            _mp = new MediaPlayer(_libVLC);
            _videoView.MediaPlayer = _mp;

            // WebView2 init (async) [web:299]
            await _topBar.EnsureCoreWebView2Async();
            await _bottomBar.EnsureCoreWebView2Async();

            // One origin for local files (recommended for local content) [web:86]
            var baseDir = AppContext.BaseDirectory;
            _topBar.CoreWebView2.SetVirtualHostNameToFolderMapping("app", baseDir, CoreWebView2HostResourceAccessKind.DenyCors);
            _bottomBar.CoreWebView2.SetVirtualHostNameToFolderMapping("app", baseDir, CoreWebView2HostResourceAccessKind.DenyCors);

            // Load UI
            _topBar.Source = new Uri("https://app/ui/top.html");
            _bottomBar.Source = new Uri("https://app/ui/bottom.html");

            // Hook messages
            _topBar.CoreWebView2.WebMessageReceived += TopBar_WebMessageReceived;
            _bottomBar.CoreWebView2.WebMessageReceived += BottomBar_WebMessageReceived;

            _uiTimer.Start();
        }

        private void TopBar_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
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
                                PostToTop(new { type = "error", message = "Invalid URL." });
                                return;
                            }
                            PlayUrl(url);
                            break;
                        }

                    case "remove":
                        StopAndClear();
                        break;
                }
            }
            catch
            {
                PostToTop(new { type = "error", message = "Bad message from UI." });
            }
        }

        private void BottomBar_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                using var doc = JsonDocument.Parse(e.WebMessageAsJson);
                var root = doc.RootElement;
                var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
                if (string.IsNullOrWhiteSpace(type)) return;

                switch (type)
                {
                    case "togglePlayPause":
                        if (_mp.IsPlaying) _mp.Pause();
                        else _mp.Play();
                        PushPlayerStateToBottomBar();
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

                            PushPlayerStateToBottomBar();
                            break;
                        }

                    case "requestState":
                        PushPlayerStateToBottomBar();
                        break;
                }
            }
            catch { }
        }

        private void PlayUrl(string url)
        {
            try { _mp.Stop(); } catch { }

            _media?.Dispose();
            _media = new Media(_libVLC, url, FromType.FromLocation);
            _mp.Play(_media);

            PostToTop(new { type = "status", message = "Loading..." });
            PushPlayerStateToBottomBar();
        }

        private void StopAndClear()
        {
            try { _mp.Stop(); } catch { }

            _media?.Dispose();
            _media = null;

            PostToTop(new { type = "status", message = "" });
            PushPlayerStateToBottomBar(reset: true);
        }

        private void PushPlayerStateToBottomBar(bool reset = false)
        {
            if (_bottomBar.CoreWebView2 == null) return;

            if (reset)
            {
                PostToBottom(new { type = "state", hasMedia = false, isPlaying = false, timeMs = 0L, lengthMs = 0L, pos = 0.0 });
                return;
            }

            var time = Math.Max(0, _mp.Time);
            var len = Math.Max(0, _mp.Length);
            var pos = (len > 0) ? (double)time / len : Math.Clamp(_mp.Position, 0f, 1f);

            if (_seekingFromUi) pos = -1; // don’t overwrite slider while dragging

            PostToBottom(new { type = "state", hasMedia = _media != null, isPlaying = _mp.IsPlaying, timeMs = time, lengthMs = len, pos });
        }

        private void PostToTop(object obj)
        {
            try { _topBar.CoreWebView2?.PostWebMessageAsJson(JsonSerializer.Serialize(obj)); } catch { }
        }

        private void PostToBottom(object obj)
        {
            try { _bottomBar.CoreWebView2?.PostWebMessageAsJson(JsonSerializer.Serialize(obj)); } catch { }
        }

        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            _uiTimer.Stop();
            try { _mp?.Stop(); } catch { }
            try { _media?.Dispose(); } catch { }
            try { _mp?.Dispose(); } catch { }
            try { _libVLC?.Dispose(); } catch { }
        }
    }
}
