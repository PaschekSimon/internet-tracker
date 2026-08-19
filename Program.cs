using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace RouterTaskbarMonitor
{
    // Metric Options Enum
    public enum MetricType
    {
        DownloadSpeed,
        UploadSpeed,
        CombinedSpeed,
        RsrpSignal,
        RsrqSignal,
        CombinedSignal,
        RssiAndBand,
        ConnectedClients,
        RouterUptime,
        WanIpAddress,
        CarrierAggregation
    }

    public class AppConfig
    {
        public string RouterUrl { get; set; }
        public string RouterPassword { get; set; }
        public int RefreshIntervalSeconds { get; set; }
        public MetricType Line1Metric { get; set; }
        public MetricType Line2Metric { get; set; }
        public bool ShowTaskbarBar { get; set; }
        public int WindowX { get; set; }
        public int WindowY { get; set; }
        public bool StartWithWindows { get; set; }

        public AppConfig()
        {
            RouterUrl = "http://192.168.0.1";
            RouterPassword = Environment.GetEnvironmentVariable("ROUTER_PASSWORD") ?? "";
            RefreshIntervalSeconds = 2;
            Line1Metric = MetricType.CombinedSpeed;
            Line2Metric = MetricType.CombinedSignal;
            ShowTaskbarBar = true;
            WindowX = -1; // Default auto-position near taskbar
            WindowY = -1;
            StartWithWindows = false;
        }

        public static string ConfigPath
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RouterTaskbarMonitor");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                return Path.Combine(dir, "config.json");
            }
        }

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    AppConfig cfg = serializer.Deserialize<AppConfig>(json);
                    if (cfg != null) return cfg;
                }
            }
            catch { }
            return new AppConfig();
        }

        public void Save()
        {
            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(this);
                File.WriteAllText(ConfigPath, json);
            }
            catch { }
        }
    }

    public class RouterMetrics
    {
        public bool IsConnected { get; set; }
        public string ErrorMessage { get; set; }

        public double RxBytesPerSec { get; set; }
        public double TxBytesPerSec { get; set; }
        public int Rsrp { get; set; }
        public double Rsrq { get; set; }
        public int Rssi { get; set; }
        public string Band { get; set; }
        public string CaStatus { get; set; }
        public int ConnectedClients { get; set; }
        public int UptimeSeconds { get; set; }
        public string WanIp { get; set; }
        public string NetworkType { get; set; }

        public RouterMetrics()
        {
            IsConnected = false;
            ErrorMessage = "Connecting...";
            Band = "N/A";
            CaStatus = "Off";
            WanIp = "N/A";
            NetworkType = "N/A";
        }

        public string FormatSpeed(double bytesPerSec)
        {
            double bitsPerSec = bytesPerSec * 8;
            if (bitsPerSec >= 10000000) // >= 10 Mbps
                return string.Format("{0:F1} Mb/s", bitsPerSec / 1000000.0);
            if (bitsPerSec >= 1000000) // >= 1 Mbps
                return string.Format("{0:F2} Mb/s", bitsPerSec / 1000000.0);
            if (bytesPerSec >= 1024 * 1024)
                return string.Format("{0:F1} MB/s", bytesPerSec / (1024.0 * 1024.0));
            if (bytesPerSec >= 1024)
                return string.Format("{0:F0} KB/s", bytesPerSec / 1024.0);
            return string.Format("{0:F0} B/s", bytesPerSec);
        }

        public string FormatUptime(int seconds)
        {
            TimeSpan t = TimeSpan.FromSeconds(seconds);
            if (t.TotalHours >= 1)
                return string.Format("{0:D2}:{1:D2}:{2:D2}", (int)t.TotalHours, t.Minutes, t.Seconds);
            return string.Format("{0:D2}:{1:D2}", t.Minutes, t.Seconds);
        }

        public string GetMetricValue(MetricType metric)
        {
            if (!IsConnected) return ErrorMessage;

            switch (metric)
            {
                case MetricType.DownloadSpeed:
                    return string.Format("↓ {0}", FormatSpeed(RxBytesPerSec));
                case MetricType.UploadSpeed:
                    return string.Format("↑ {0}", FormatSpeed(TxBytesPerSec));
                case MetricType.CombinedSpeed:
                    return string.Format("↓ {0}  ↑ {1}", FormatSpeed(RxBytesPerSec), FormatSpeed(TxBytesPerSec));
                case MetricType.RsrpSignal:
                    return string.Format("RSRP: {0} dBm", Rsrp);
                case MetricType.RsrqSignal:
                    return string.Format("RSRQ: {0:F1} dB", Rsrq);
                case MetricType.CombinedSignal:
                    return string.Format("RSRP: {0} | RSRQ: {1:F1}", Rsrp, Rsrq);
                case MetricType.RssiAndBand:
                    return string.Format("{0} ({1} dBm)", Band, Rssi);
                case MetricType.ConnectedClients:
                    return string.Format("Clients: {0}", ConnectedClients);
                case MetricType.RouterUptime:
                    return string.Format("Uptime: {0}", FormatUptime(UptimeSeconds));
                case MetricType.WanIpAddress:
                    return string.Format("WAN: {0}", WanIp);
                case MetricType.CarrierAggregation:
                    return string.Format("CA: {0}", CaStatus);
                default:
                    return "N/A";
            }
        }
    }

    public class RouterApiClient
    {
        private readonly AppConfig _config;
        private bool _isLoggedIn = false;
        private string _cookieHeader = null;

        public RouterApiClient(AppConfig config)
        {
            _config = config;
        }

        public void ResetSession()
        {
            _isLoggedIn = false;
            _cookieHeader = null;
        }

        private static string ComputePasswordHash(string rawPassword)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(rawPassword);
            string base64 = Convert.ToBase64String(bytes);

            using (SHA256 sha = SHA256.Create())
            {
                byte[] hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(base64));
                StringBuilder sb = new StringBuilder();
                foreach (byte b in hashBytes) sb.Append(b.ToString("X2"));
                return sb.ToString();
            }
        }

        public bool Login()
        {
            try
            {
                string hashedPwd = ComputePasswordHash(_config.RouterPassword);
                string postData = string.Format("isTest=false&goformId=LOGIN&password={0}", hashedPwd);
                byte[] data = Encoding.UTF8.GetBytes(postData);

                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(_config.RouterUrl + "/goform/goform_set_cmd_process");
                req.Method = "POST";
                req.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
                req.Accept = "application/json, text/javascript, */*; q=0.01";
                req.Referer = _config.RouterUrl + "/index.html";
                req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36";
                req.Headers.Add("Origin", _config.RouterUrl);
                req.Headers.Add("X-Requested-With", "XMLHttpRequest");
                req.Timeout = 4000;

                using (Stream stream = req.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    string rawSetCookie = resp.Headers["Set-Cookie"];
                    if (!string.IsNullOrEmpty(rawSetCookie))
                    {
                        string[] parts = rawSetCookie.Split(';');
                        if (parts.Length > 0)
                        {
                            _cookieHeader = parts[0].Trim();
                        }
                    }

                    using (StreamReader reader = new StreamReader(resp.GetResponseStream()))
                    {
                        string json = reader.ReadToEnd();
                        JavaScriptSerializer serializer = new JavaScriptSerializer();
                        Dictionary<string, object> dict = serializer.Deserialize<Dictionary<string, object>>(json);
                        if (dict != null && dict.ContainsKey("result") && dict["result"].ToString() == "0")
                        {
                            _isLoggedIn = true;
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _isLoggedIn = false;
            }
            return false;
        }

        public RouterMetrics FetchMetrics()
        {
            RouterMetrics metrics = new RouterMetrics();
            if (!_isLoggedIn || string.IsNullOrEmpty(_cookieHeader))
            {
                if (!Login())
                {
                    metrics.ErrorMessage = "Login Failed";
                    return metrics;
                }
            }

            try
            {
                // Request 1: Signal Info
                string signalUrl = _config.RouterUrl + "/goform/goform_get_cmd_process?isTest=false&cmd=network_type,rssi,rscp,lte_rsrp,Z_rssi,Z_rscp,Z_rsrp,Z_rsrq,wan_lte_ca,lte_ca_pcell_bandwidth,lte_ca_pcell_band,lte_ca_scell_bandwidth,lte_ca_scell_band,ZCELLINFO_band&multi_data=1&_=" + DateTime.Now.Ticks;
                Dictionary<string, object> sigDict = GetJson(signalUrl);

                bool isLoggedOut = sigDict == null ||
                                   (sigDict.ContainsKey("loginfo") && sigDict["loginfo"].ToString() == "logout") ||
                                   (sigDict.ContainsKey("lte_rsrp") && string.IsNullOrEmpty(sigDict["lte_rsrp"].ToString()) &&
                                    sigDict.ContainsKey("Z_rsrp") && string.IsNullOrEmpty(sigDict["Z_rsrp"].ToString()));

                if (isLoggedOut)
                {
                    _isLoggedIn = false;
                    if (Login())
                    {
                        sigDict = GetJson(signalUrl);
                    }
                }

                if (sigDict != null)
                {
                    metrics.IsConnected = true;
                    metrics.NetworkType = sigDict.ContainsKey("network_type") ? sigDict["network_type"].ToString() : "";
                    metrics.Band = sigDict.ContainsKey("ZCELLINFO_band") ? sigDict["ZCELLINFO_band"].ToString() : (sigDict.ContainsKey("lte_ca_pcell_band") ? "B" + sigDict["lte_ca_pcell_band"] : "");
                    metrics.CaStatus = (sigDict.ContainsKey("wan_lte_ca") && sigDict["wan_lte_ca"].ToString() == "ca_activated") ? "Active" : "Off";

                    int rsrp;
                    if (sigDict.ContainsKey("lte_rsrp") && int.TryParse(sigDict["lte_rsrp"].ToString(), out rsrp)) metrics.Rsrp = rsrp;
                    else if (sigDict.ContainsKey("Z_rsrp") && int.TryParse(sigDict["Z_rsrp"].ToString(), out rsrp)) metrics.Rsrp = rsrp;

                    double rsrq;
                    if (sigDict.ContainsKey("Z_rsrq") && double.TryParse(sigDict["Z_rsrq"].ToString().Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out rsrq)) metrics.Rsrq = rsrq;

                    int rssi;
                    if (sigDict.ContainsKey("Z_rssi") && int.TryParse(sigDict["Z_rssi"].ToString(), out rssi)) metrics.Rssi = rssi;
                    else if (sigDict.ContainsKey("rssi") && int.TryParse(sigDict["rssi"].ToString(), out rssi)) metrics.Rssi = rssi;
                }

                // Request 2: Speed & Status Info
                string speedUrl = _config.RouterUrl + "/goform/goform_get_cmd_process?multi_data=1&isTest=false&sms_received_flag_flag=0&sts_received_flag_flag=0&cmd=realtime_tx_bytes,realtime_rx_bytes,realtime_time,realtime_tx_thrpt,realtime_rx_thrpt,wifi_access_sta_num,wifi_mesh_lan_access_num,wan_ipaddr&_=" + DateTime.Now.Ticks;
                Dictionary<string, object> spdDict = GetJson(speedUrl);

                if (spdDict != null)
                {
                    double rxThrpt, txThrpt;
                    if (spdDict.ContainsKey("realtime_rx_thrpt") && double.TryParse(spdDict["realtime_rx_thrpt"].ToString(), out rxThrpt)) metrics.RxBytesPerSec = rxThrpt;
                    if (spdDict.ContainsKey("realtime_tx_thrpt") && double.TryParse(spdDict["realtime_tx_thrpt"].ToString(), out txThrpt)) metrics.TxBytesPerSec = txThrpt;

                    int uptime;
                    if (spdDict.ContainsKey("realtime_time") && int.TryParse(spdDict["realtime_time"].ToString(), out uptime)) metrics.UptimeSeconds = uptime;

                    if (spdDict.ContainsKey("wan_ipaddr")) metrics.WanIp = spdDict["wan_ipaddr"].ToString();

                    int wifiNum = 0, lanNum = 0;
                    if (spdDict.ContainsKey("wifi_access_sta_num")) int.TryParse(spdDict["wifi_access_sta_num"].ToString(), out wifiNum);
                    if (spdDict.ContainsKey("wifi_mesh_lan_access_num")) int.TryParse(spdDict["wifi_mesh_lan_access_num"].ToString(), out lanNum);
                    metrics.ConnectedClients = wifiNum + lanNum;
                }
            }
            catch (Exception ex)
            {
                metrics.IsConnected = false;
                metrics.ErrorMessage = "Error: " + ex.Message;
            }

            return metrics;
        }

        private Dictionary<string, object> GetJson(string url)
        {
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "GET";
                req.Accept = "application/json, text/javascript, */*; q=0.01";
                req.Referer = _config.RouterUrl + "/index.html";
                req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36";
                req.Headers.Add("X-Requested-With", "XMLHttpRequest");
                if (!string.IsNullOrEmpty(_cookieHeader))
                {
                    req.Headers.Add("Cookie", _cookieHeader);
                }
                req.Timeout = 3000;

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader reader = new StreamReader(resp.GetResponseStream()))
                {
                    string json = reader.ReadToEnd();
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    return serializer.Deserialize<Dictionary<string, object>>(json);
                }
            }
            catch
            {
                return null;
            }
        }
    }

    // Taskbar Info Bar Window Component
    public class TaskbarInfoForm : Form
    {
        private readonly AppConfig _config;
        private RouterMetrics _metrics = new RouterMetrics();
        private bool _isDragging = false;
        private Point _dragStartPoint;

        public TaskbarInfoForm(AppConfig config)
        {
            _config = config;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            BackColor = Color.FromArgb(24, 24, 24);
            ForeColor = Color.White;
            Width = 140;
            Height = 40;

            PositionWindow();

            MouseDown += TaskbarInfoForm_MouseDown;
            MouseMove += TaskbarInfoForm_MouseMove;
            MouseUp += TaskbarInfoForm_MouseUp;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                return cp;
            }
        }

        public void UpdateMetrics(RouterMetrics metrics)
        {
            _metrics = metrics;
            if (InvokeRequired)
            {
                BeginInvoke(new Action(Invalidate));
            }
            else
            {
                Invalidate();
            }
        }

        public void PositionWindow()
        {
            if (_config.WindowX >= 0 && _config.WindowY >= 0)
            {
                Location = new Point(_config.WindowX, _config.WindowY);
            }
            else
            {
                // Default position: Taskbar notification area (bottom right)
                Rectangle taskbar = Screen.PrimaryScreen.WorkingArea;
                int screenWidth = Screen.PrimaryScreen.Bounds.Width;
                int screenHeight = Screen.PrimaryScreen.Bounds.Height;

                // Place near clock in taskbar (bottom right)
                int defaultX = screenWidth - Width - 180;
                int defaultY = screenHeight - Height - 4;
                Location = new Point(defaultX, defaultY);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Background card style matching Windows 11/10 Taskbar dark theme
            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(28, 28, 30)))
            using (Pen borderPen = new Pen(Color.FromArgb(50, 255, 255, 255), 1))
            {
                Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
                g.FillRectangle(bgBrush, rect);
                g.DrawRectangle(borderPen, rect);
            }

            string line1Text = _metrics.GetMetricValue(_config.Line1Metric);
            string line2Text = _metrics.GetMetricValue(_config.Line2Metric);

            using (Font font = new Font("Segoe UI", 8.25f, FontStyle.Regular))
            using (Brush textBrush = new SolidBrush(Color.FromArgb(240, 240, 240)))
            {
                SizeF size1 = g.MeasureString(line1Text, font);
                SizeF size2 = g.MeasureString(line2Text, font);

                int requiredWidth = Math.Max(130, (int)Math.Max(size1.Width, size2.Width) + 16);
                if (Width != requiredWidth)
                {
                    Width = requiredWidth;
                    Invalidate();
                    return;
                }

                // Draw Line 1 (Row 1)
                g.DrawString(line1Text, font, textBrush, new PointF(8, 3));

                // Draw Line 2 (Row 2)
                g.DrawString(line2Text, font, textBrush, new PointF(8, 19));
            }
        }

        private void TaskbarInfoForm_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _isDragging = true;
                _dragStartPoint = e.Location;
            }
        }

        private void TaskbarInfoForm_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                Point currentScreenPos = PointToScreen(e.Location);
                Location = new Point(currentScreenPos.X - _dragStartPoint.X, currentScreenPos.Y - _dragStartPoint.Y);
            }
        }

        private void TaskbarInfoForm_MouseUp(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                _config.WindowX = Location.X;
                _config.WindowY = Location.Y;
                _config.Save();
            }
        }
    }

    // Settings Modal Dialog
    public class SettingsForm : Form
    {
        private readonly AppConfig _config;
        private TextBox txtUrl;
        private TextBox txtPassword;
        private ComboBox cbRefresh;
        private CheckBox chkStartWithWindows;

        public SettingsForm(AppConfig config)
        {
            _config = config;
            Text = "Router Monitor Settings";
            Size = new Size(360, 260);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(32, 32, 32);
            ForeColor = Color.White;

            InitializeControls();
        }

        private void InitializeControls()
        {
            Label lblUrl = new Label { Text = "Router Address:", Location = new Point(20, 20), AutoSize = true, ForeColor = Color.LightGray };
            txtUrl = new TextBox { Text = _config.RouterUrl, Location = new Point(140, 18), Width = 180, BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White };

            Label lblPwd = new Label { Text = "Router Password:", Location = new Point(20, 55), AutoSize = true, ForeColor = Color.LightGray };
            txtPassword = new TextBox { Text = _config.RouterPassword, Location = new Point(140, 53), Width = 180, PasswordChar = '•', BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White };

            Label lblRefresh = new Label { Text = "Refresh Interval:", Location = new Point(20, 90), AutoSize = true, ForeColor = Color.LightGray };
            cbRefresh = new ComboBox { Location = new Point(140, 88), Width = 180, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White };
            cbRefresh.Items.AddRange(new object[] { "1 Second", "2 Seconds", "3 Seconds", "5 Seconds" });
            cbRefresh.SelectedIndex = Math.Min(3, Math.Max(0, _config.RefreshIntervalSeconds - 1));

            chkStartWithWindows = new CheckBox { Text = "Start automatically with Windows", Location = new Point(20, 125), AutoSize = true, Checked = _config.StartWithWindows, ForeColor = Color.LightGray };

            Button btnSave = new Button { Text = "Save", Location = new Point(140, 170), Width = 80, Height = 30, DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0, 120, 215), ForeColor = Color.White };
            Button btnCancel = new Button { Text = "Cancel", Location = new Point(240, 170), Width = 80, Height = 30, DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat, ForeColor = Color.White };

            btnSave.Click += (s, e) =>
            {
                _config.RouterUrl = txtUrl.Text.Trim();
                _config.RouterPassword = txtPassword.Text.Trim();
                _config.RefreshIntervalSeconds = cbRefresh.SelectedIndex + 1;
                _config.StartWithWindows = chkStartWithWindows.Checked;
                _config.Save();

                SetAutoStart(_config.StartWithWindows);
            };

            Controls.Add(lblUrl);
            Controls.Add(txtUrl);
            Controls.Add(lblPwd);
            Controls.Add(txtPassword);
            Controls.Add(lblRefresh);
            Controls.Add(cbRefresh);
            Controls.Add(chkStartWithWindows);
            Controls.Add(btnSave);
            Controls.Add(btnCancel);
        }

        private static void SetAutoStart(bool enable)
        {
            try
            {
                RegistryKey rk = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                if (enable)
                {
                    rk.SetValue("RouterTaskbarMonitor", Application.ExecutablePath);
                }
                else
                {
                    rk.DeleteValue("RouterTaskbarMonitor", false);
                }
            }
            catch { }
        }
    }

    // System Tray Controller Context
    public class TrayApplicationContext : ApplicationContext
    {
        private readonly AppConfig _config;
        private readonly NotifyIcon _trayIcon;
        private readonly TaskbarInfoForm _infoForm;
        private readonly RouterApiClient _apiClient;
        private readonly System.Windows.Forms.Timer _pollTimer;

        public TrayApplicationContext()
        {
            _config = AppConfig.Load();
            _apiClient = new RouterApiClient(_config);
            _infoForm = new TaskbarInfoForm(_config);

            _trayIcon = new NotifyIcon
            {
                Icon = CreateTrayIcon(),
                Text = "Router Taskbar Monitor (Left-click to toggle)",
                Visible = true
            };

            // Left click toggles taskbar bar
            _trayIcon.Click += TrayIcon_Click;

            // Build Context Menu for Right-Click
            RebuildContextMenu();

            if (_config.ShowTaskbarBar)
            {
                _infoForm.Show();
                ThreadPool.QueueUserWorkItem(o => DoPoll());
            }
            else
            {
                try
                {
                    _trayIcon.Text = "Router Monitor (Paused - Left-click to show)";
                }
                catch { }
            }

            _pollTimer = new System.Windows.Forms.Timer
            {
                Interval = _config.RefreshIntervalSeconds * 1000
            };
            _pollTimer.Tick += PollTimer_Tick;
            _pollTimer.Start();
        }

        private Icon CreateTrayIcon()
        {
            Bitmap bmp = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                // Draw sleek router icon with signal waves
                using (Brush b = new SolidBrush(Color.FromArgb(0, 180, 255)))
                {
                    g.FillRectangle(b, 2, 10, 12, 4);
                }
                using (Pen p = new Pen(Color.FromArgb(0, 220, 130), 1.5f))
                {
                    g.DrawArc(p, 2, 2, 12, 12, 210, 120);
                    g.DrawArc(p, 4, 5, 8, 8, 210, 120);
                }
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        private void RebuildContextMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.BackColor = Color.FromArgb(32, 32, 32);
            menu.ForeColor = Color.White;

            ToolStripMenuItem itemToggle = new ToolStripMenuItem("Show Taskbar Info Bar", null, (s, e) => ToggleBar())
            {
                Checked = _config.ShowTaskbarBar
            };
            menu.Items.Add(itemToggle);
            menu.Items.Add(new ToolStripSeparator());

            // Submenu: Line 1 Selection
            ToolStripMenuItem line1Menu = new ToolStripMenuItem("Line 1 Display");
            AddMetricOptions(line1Menu, _config.Line1Metric, (selected) =>
            {
                _config.Line1Metric = selected;
                _config.Save();
                RebuildContextMenu();
                _infoForm.Invalidate();
            });
            menu.Items.Add(line1Menu);

            // Submenu: Line 2 Selection
            ToolStripMenuItem line2Menu = new ToolStripMenuItem("Line 2 Display");
            AddMetricOptions(line2Menu, _config.Line2Metric, (selected) =>
            {
                _config.Line2Metric = selected;
                _config.Save();
                RebuildContextMenu();
                _infoForm.Invalidate();
            });
            menu.Items.Add(line2Menu);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem itemResetPos = new ToolStripMenuItem("Reset Position to Taskbar", null, (s, e) =>
            {
                _config.WindowX = -1;
                _config.WindowY = -1;
                _config.Save();
                _infoForm.PositionWindow();
            });
            menu.Items.Add(itemResetPos);

            ToolStripMenuItem itemSettings = new ToolStripMenuItem("Router & App Settings...", null, (s, e) =>
            {
                using (SettingsForm form = new SettingsForm(_config))
                {
                    if (form.ShowDialog() == DialogResult.OK)
                    {
                        _pollTimer.Interval = _config.RefreshIntervalSeconds * 1000;
                        ThreadPool.QueueUserWorkItem(o => DoPoll());
                    }
                }
            });
            menu.Items.Add(itemSettings);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem itemExit = new ToolStripMenuItem("Exit", null, (s, e) => ExitApp());
            menu.Items.Add(itemExit);

            _trayIcon.ContextMenuStrip = menu;
        }

        private static void AddMetricOptions(ToolStripMenuItem parentMenu, MetricType current, Action<MetricType> onSelect)
        {
            Dictionary<MetricType, string> options = new Dictionary<MetricType, string>
            {
                { MetricType.CombinedSpeed, "Download & Upload Speed (↓ / ↑)" },
                { MetricType.DownloadSpeed, "Download Speed Only (↓)" },
                { MetricType.UploadSpeed, "Upload Speed Only (↑)" },
                { MetricType.CombinedSignal, "RSRP & RSRQ Signal (Combined)" },
                { MetricType.RsrpSignal, "RSRP Signal (dBm)" },
                { MetricType.RsrqSignal, "RSRQ Quality (dB)" },
                { MetricType.RssiAndBand, "LTE Band & RSSI" },
                { MetricType.ConnectedClients, "Connected Clients Count" },
                { MetricType.RouterUptime, "Router Uptime" },
                { MetricType.WanIpAddress, "WAN IP Address" },
                { MetricType.CarrierAggregation, "Carrier Aggregation (CA) Status" }
            };

            foreach (KeyValuePair<MetricType, string> kvp in options)
            {
                MetricType type = kvp.Key;
                ToolStripMenuItem item = new ToolStripMenuItem(kvp.Value, null, (s, e) => onSelect(type))
                {
                    Checked = (current == type)
                };
                parentMenu.DropDownItems.Add(item);
            }
        }

        private void TrayIcon_Click(object sender, EventArgs e)
        {
            MouseEventArgs me = e as MouseEventArgs;
            if (me != null && me.Button == MouseButtons.Left)
            {
                ToggleBar();
            }
        }

        private void ToggleBar()
        {
            _config.ShowTaskbarBar = !_config.ShowTaskbarBar;
            _config.Save();

            if (_config.ShowTaskbarBar)
            {
                _infoForm.Show();
                ThreadPool.QueueUserWorkItem(o => DoPoll());
            }
            else
            {
                _infoForm.Hide();
                _apiClient.ResetSession();
                try
                {
                    if (_trayIcon != null)
                    {
                        _trayIcon.Text = "Router Monitor (Paused - Left-click to show)";
                    }
                }
                catch { }
            }
            RebuildContextMenu();
        }

        private void PollTimer_Tick(object sender, EventArgs e)
        {
            if (_config.ShowTaskbarBar)
            {
                ThreadPool.QueueUserWorkItem(o => DoPoll());
            }
        }

        private void DoPoll()
        {
            if (!_config.ShowTaskbarBar) return;

            RouterMetrics metrics = _apiClient.FetchMetrics();

            if (!_config.ShowTaskbarBar) return;

            _infoForm.UpdateMetrics(metrics);

            string trayText = metrics.IsConnected
                ? string.Format("Router: Connected\n{0}\n{1}", metrics.GetMetricValue(MetricType.CombinedSpeed), metrics.GetMetricValue(MetricType.CombinedSignal))
                : string.Format("Router: {0}", metrics.ErrorMessage);

            if (trayText.Length > 63) trayText = trayText.Substring(0, 63);

            if (_trayIcon != null && _config.ShowTaskbarBar)
            {
                try
                {
                    _trayIcon.Text = trayText;
                }
                catch { }
            }
        }

        private void ExitApp()
        {
            _pollTimer.Stop();
            _trayIcon.Visible = false;
            _infoForm.Close();
            Application.Exit();
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayApplicationContext());
        }
    }
}
