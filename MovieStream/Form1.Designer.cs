namespace MovieStream
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            uiTimer = new System.Windows.Forms.Timer(components);
            videoView = new LibVLCSharp.WinForms.VideoView();
            uiView = new Microsoft.Web.WebView2.WinForms.WebView2();
            ((System.ComponentModel.ISupportInitialize)videoView).BeginInit();
            ((System.ComponentModel.ISupportInitialize)uiView).BeginInit();
            SuspendLayout();
            // 
            // uiTimer
            // 
            uiTimer.Enabled = true;
            uiTimer.Interval = 200;
            // 
            // videoView
            // 
            videoView.BackColor = Color.Black;
            videoView.Dock = DockStyle.Fill;
            videoView.Location = new Point(0, 0);
            videoView.MediaPlayer = null;
            videoView.Name = "videoView";
            videoView.Size = new Size(800, 450);
            videoView.TabIndex = 0;
            videoView.Text = "videoView1";
            // 
            // uiView
            // 
            uiView.AllowExternalDrop = true;
            uiView.CreationProperties = null;
            uiView.DefaultBackgroundColor = Color.White;
            uiView.Dock = DockStyle.Fill;
            uiView.Location = new Point(0, 0);
            uiView.Margin = new Padding(0);
            uiView.Name = "uiView";
            uiView.Size = new Size(800, 450);
            uiView.TabIndex = 1;
            uiView.ZoomFactor = 1D;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(15, 15, 15);
            ClientSize = new Size(800, 450);
            Controls.Add(uiView);
            Controls.Add(videoView);
            Name = "Form1";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "MovieStream";
            Load += Form1_Load;
            ((System.ComponentModel.ISupportInitialize)videoView).EndInit();
            ((System.ComponentModel.ISupportInitialize)uiView).EndInit();
            ResumeLayout(false);
        }

        #endregion
        private System.Windows.Forms.Timer uiTimer;
        private LibVLCSharp.WinForms.VideoView videoView;
        private Microsoft.Web.WebView2.WinForms.WebView2 uiView;
    }
}
