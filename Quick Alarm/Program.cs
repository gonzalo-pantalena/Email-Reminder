using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Media;
using System.Windows.Forms;

namespace Quick_Alarm
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new AlarmAppContext());
        }

        // ---------------------------------------------------------------
        // Data model
        // ---------------------------------------------------------------
        public class Alarm
        {
            public string   Name        { get; set; }
            public DateTime TriggerTime { get; set; }
            public bool     Fired       { get; set; }
        }

        // ---------------------------------------------------------------
        // Application context — only systray, no main window
        // ---------------------------------------------------------------
        public class AlarmAppContext : ApplicationContext
        {
            private readonly NotifyIcon  _trayIcon;
            private readonly List<Alarm> _alarms = new List<Alarm>();
            private readonly Timer       _checkTimer;
            private readonly SoundPlayer _sound = new SoundPlayer("R2D2.wav");
            private readonly Icon        _icon  = new Icon("ringbell.ico");

            // Context menu items that are rebuilt on Opening
            private readonly ToolStripMenuItem _pendingHeader = new ToolStripMenuItem { Enabled = false };
            private readonly ToolStripSeparator _pendingSep   = new ToolStripSeparator();

            public AlarmAppContext()
            {
                var newAlarmItem = new ToolStripMenuItem("Nueva Alarma...");
                newAlarmItem.Font = new Font(newAlarmItem.Font, FontStyle.Bold);
                newAlarmItem.Click += (s, e) => ShowNewAlarmDialog();

                var exitItem = new ToolStripMenuItem("Salir");
                exitItem.Click += (s, e) => { _trayIcon.Visible = false; Application.Exit(); };

                var menu = new ContextMenuStrip();
                menu.Items.Add(newAlarmItem);
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(_pendingHeader);
                menu.Items.Add(_pendingSep);
                menu.Items.Add(exitItem);

                // Rebuild pending list every time the menu opens
                menu.Opening += (s, e) => RefreshPendingItems(menu, exitItem);

                _trayIcon = new NotifyIcon
                {
                    Icon             = _icon,
                    Text             = "Quick Alarm",
                    ContextMenuStrip = menu,
                    Visible          = true
                };
                _trayIcon.DoubleClick += (s, e) => ShowNewAlarmDialog();

                _checkTimer = new Timer { Interval = 10000 };
                _checkTimer.Tick += CheckAlarms;
                _checkTimer.Start();
            }

            private void RefreshPendingItems(ContextMenuStrip menu, ToolStripMenuItem exitItem)
            {
                // Remove any previously injected alarm items (between _pendingHeader and exitItem)
                int headerIdx = menu.Items.IndexOf(_pendingHeader);
                int exitIdx   = menu.Items.IndexOf(exitItem);
                while (exitIdx - headerIdx > 2) // leave _pendingHeader + _pendingSep
                {
                    menu.Items.RemoveAt(headerIdx + 2);
                    exitIdx = menu.Items.IndexOf(exitItem);
                }

                var pending = _alarms.FindAll(a => !a.Fired);
                if (pending.Count == 0)
                {
                    _pendingHeader.Text    = "Sin alarmas pendientes";
                    _pendingSep.Visible    = false;
                }
                else
                {
                    _pendingHeader.Text = $"Pendientes ({pending.Count}):";
                    _pendingSep.Visible = true;
                    int insertAt = menu.Items.IndexOf(_pendingSep) + 1;
                    foreach (var alarm in pending)
                    {
                        var item = new ToolStripMenuItem($"  {alarm.TriggerTime:dd/MM HH:mm}  —  {alarm.Name}")
                        {
                            Enabled = false,
                            Font    = new Font("Segoe UI", 9F)
                        };
                        menu.Items.Insert(insertAt++, item);
                    }
                }
            }

            private void ShowNewAlarmDialog()
            {
                using (var dlg = new NewAlarmForm())
                {
                    if (dlg.ShowDialog() == DialogResult.OK)
                    {
                        _alarms.Add(new Alarm { Name = dlg.AlarmName, TriggerTime = dlg.AlarmTime });
                        _trayIcon.BalloonTipTitle = "Alarma creada";
                        _trayIcon.BalloonTipText  = $"{dlg.AlarmName}\n{dlg.AlarmTime:dd/MM/yyyy HH:mm}";
                        _trayIcon.ShowBalloonTip(3000);
                    }
                }
            }

            private bool _checkingAlarms = false;

            private void CheckAlarms(object sender, EventArgs e)
            {
                // ShowDialog() pumps messages, which can re-trigger this tick.
                // Guard prevents re-entrant calls from corrupting the enumerator.
                if (_checkingAlarms) return;
                _checkingAlarms = true;
                try
                {
                    var now    = DateTime.Now;
                    // Snapshot which alarms are due — do this before any ShowDialog call.
                    var toFire = _alarms.FindAll(a => !a.Fired && now >= a.TriggerTime);
                    foreach (var alarm in toFire)
                        alarm.Fired = true;
                    _alarms.RemoveAll(a => a.Fired);
                    // Show popups after the list is already clean.
                    foreach (var alarm in toFire)
                        ShowAlarmPopup(alarm.Name);
                }
                finally
                {
                    _checkingAlarms = false;
                }
            }

            private void ShowAlarmPopup(string name)
            {
                // Popup is wider when background image is present so R2D2 is visible
                const string BgFile = "r2d2bg.png";
                bool hasBg = File.Exists(BgFile);
                int popupW = hasBg ? 560 : 420;
                int popupH = hasBg ? 230 : 215;

                var popup = new Form
                {
                    FormBorderStyle = FormBorderStyle.None,
                    StartPosition   = FormStartPosition.CenterScreen,
                    BackColor       = Color.FromArgb(5, 15, 30),   // matches image dark BG
                    Size            = new Size(popupW, popupH),
                    TopMost         = true
                };

                if (hasBg)
                {
                    popup.BackgroundImage       = Image.FromFile(BgFile);
                    popup.BackgroundImageLayout = ImageLayout.Stretch;
                }

                // Drag support using Cursor.Position (works regardless of which child fires it)
                Point dragOffset = Point.Empty;
                bool  dragging   = false;

                void WireDrag(Control c)
                {
                    c.MouseDown += (s, e) =>
                    {
                        if (e.Button != MouseButtons.Left) return;
                        dragging   = true;
                        dragOffset = new Point(Cursor.Position.X - popup.Left,
                                               Cursor.Position.Y - popup.Top);
                    };
                    c.MouseMove += (s, e) =>
                    {
                        if (!dragging) return;
                        popup.Location = new Point(Cursor.Position.X - dragOffset.X,
                                                   Cursor.Position.Y - dragOffset.Y);
                    };
                    c.MouseUp += (s, e) => dragging = false;
                }

                // Blue accent bar at top
                var accent = new Panel
                {
                    Dock      = DockStyle.Top,
                    Height    = 4,
                    BackColor = Color.FromArgb(0, 150, 255)
                };

                // Text sits on the dark left half; keep labels transparent so BG shows through
                var iconLbl = new Label
                {
                    Text      = "🔔",
                    Font      = new Font("Segoe UI Emoji", 26F),
                    ForeColor = Color.FromArgb(0, 170, 255),
                    BackColor = Color.Transparent,
                    Location  = new Point(18, 22),
                    Size      = new Size(48, 48),
                    TextAlign = ContentAlignment.MiddleCenter
                };

                var titleLbl = new Label
                {
                    Text      = "ALARMA",
                    Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(0, 170, 255),
                    BackColor = Color.Transparent,
                    Location  = new Point(74, 22),
                    Size      = new Size(220, 22),
                    TextAlign = ContentAlignment.MiddleLeft
                };

                var nameLbl = new Label
                {
                    Text      = name,
                    Font      = new Font("Segoe UI", 13F, FontStyle.Bold),
                    ForeColor = Color.White,
                    BackColor = Color.Transparent,
                    Location  = new Point(74, 48),
                    Size      = new Size(220, 56),
                    TextAlign = ContentAlignment.MiddleLeft
                };

                var closeBtn = new Button
                {
                    Text      = "Cerrar",
                    Location  = new Point(74, popupH - 58),
                    Size      = new Size(140, 36),
                    BackColor = Color.FromArgb(0, 110, 190),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                    Cursor    = Cursors.Hand
                };
                closeBtn.FlatAppearance.BorderSize = 0;
                closeBtn.Click += (s, e) => { popup.Close(); _sound.Stop(); };

                WireDrag(popup);
                WireDrag(iconLbl);
                WireDrag(titleLbl);
                WireDrag(nameLbl);

                popup.Controls.Add(closeBtn);
                popup.Controls.Add(nameLbl);
                popup.Controls.Add(titleLbl);
                popup.Controls.Add(iconLbl);
                popup.Controls.Add(accent);

                _sound.Play();
                popup.ShowDialog();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _trayIcon?.Dispose();
                    _checkTimer?.Dispose();
                    _sound?.Dispose();
                    _icon?.Dispose();
                }
                base.Dispose(disposing);
            }
        }

        // ---------------------------------------------------------------
        // New-alarm dialog
        // ---------------------------------------------------------------
        public class NewAlarmForm : Form
        {
            public string   AlarmName { get; private set; }
            public DateTime AlarmTime { get; private set; }

            private readonly TextBox         _nameBox;
            private readonly DateTimePicker  _datePicker;
            private readonly DateTimePicker  _timePicker;

            public NewAlarmForm()
            {
                FormBorderStyle = FormBorderStyle.None;
                BackColor       = Color.FromArgb(45, 45, 48);
                Size            = new Size(400, 290);
                StartPosition   = FormStartPosition.CenterScreen;
                TopMost         = true;

                Point dragOffset = Point.Empty;
                bool  dragging   = false;

                void WireDrag(Control c)
                {
                    c.MouseDown += (s, e) =>
                    {
                        if (e.Button != MouseButtons.Left) return;
                        dragging   = true;
                        dragOffset = new Point(Cursor.Position.X - Left, Cursor.Position.Y - Top);
                    };
                    c.MouseMove += (s, e) =>
                    {
                        if (!dragging) return;
                        Location = new Point(Cursor.Position.X - dragOffset.X,
                                             Cursor.Position.Y - dragOffset.Y);
                    };
                    c.MouseUp += (s, e) => dragging = false;
                }

                // ---- Accent bar ----
                var accent = new Panel
                {
                    Dock = DockStyle.Top, Height = 4,
                    BackColor = Color.FromArgb(0, 150, 255)
                };

                // ---- Header ----
                var header = new Panel
                {
                    Location  = new Point(0, 4),
                    Size      = new Size(400, 44),
                    BackColor = Color.FromArgb(37, 37, 38)
                };

                var headerTitle = new Label
                {
                    Text      = "Nueva Alarma",
                    Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
                    ForeColor = Color.White,
                    Location  = new Point(14, 0),
                    Size      = new Size(310, 44),
                    TextAlign = ContentAlignment.MiddleLeft
                };

                var closeX = new Button
                {
                    Text      = "✕",
                    Location  = new Point(356, 0),
                    Size      = new Size(44, 44),
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = Color.Gray,
                    Font      = new Font("Segoe UI", 11F),
                    BackColor = Color.Transparent,
                    Cursor    = Cursors.Hand
                };
                closeX.FlatAppearance.BorderSize            = 0;
                closeX.FlatAppearance.MouseOverBackColor    = Color.FromArgb(196, 43, 28);
                closeX.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

                header.Controls.Add(headerTitle);
                header.Controls.Add(closeX);
                WireDrag(header);
                WireDrag(headerTitle);

                // ---- Form fields ----
                const int LabelX = 24, InputX = 130, InputW = 244;
                int cy = 68;

                var lblName = MakeLabel("Nombre:", LabelX, cy + 4);
                _nameBox = new TextBox
                {
                    Location    = new Point(InputX, cy),
                    Size        = new Size(InputW, 26),
                    BackColor   = Color.FromArgb(62, 62, 66),
                    ForeColor   = Color.White,
                    BorderStyle = BorderStyle.FixedSingle,
                    Font        = new Font("Segoe UI", 10F)
                };

                cy += 44;
                var lblDate = MakeLabel("Fecha:", LabelX, cy + 4);
                _datePicker = new DateTimePicker
                {
                    Location = new Point(InputX, cy),
                    Size     = new Size(InputW, 26),
                    Format   = DateTimePickerFormat.Short,
                    Value    = DateTime.Now,
                    Font     = new Font("Segoe UI", 10F)
                };

                cy += 44;
                var lblTime = MakeLabel("Hora:", LabelX, cy + 4);
                _timePicker = new DateTimePicker
                {
                    Location   = new Point(InputX, cy),
                    Size       = new Size(InputW, 26),
                    Format     = DateTimePickerFormat.Time,
                    ShowUpDown = true,
                    Value      = RoundUpToFiveMinutes(DateTime.Now.AddMinutes(5)),
                    Font       = new Font("Segoe UI", 10F)
                };

                cy += 52;
                var btnOk = new Button
                {
                    Text      = "Crear Alarma",
                    Location  = new Point(InputX, cy),
                    Size      = new Size(InputW, 38),
                    BackColor = Color.FromArgb(0, 122, 204),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
                    Cursor    = Cursors.Hand
                };
                btnOk.FlatAppearance.BorderSize = 0;
                btnOk.Click += BtnOk_Click;

                WireDrag(this);

                AcceptButton = btnOk;

                Controls.Add(btnOk);
                Controls.Add(lblTime);
                Controls.Add(_timePicker);
                Controls.Add(lblDate);
                Controls.Add(_datePicker);
                Controls.Add(lblName);
                Controls.Add(_nameBox);
                Controls.Add(header);
                Controls.Add(accent);
            }

            private static Label MakeLabel(string text, int x, int y) =>
                new Label
                {
                    Text      = text,
                    ForeColor = Color.FromArgb(180, 180, 180),
                    Font      = new Font("Segoe UI", 10F),
                    Location  = new Point(x, y),
                    AutoSize  = true
                };

            private static DateTime RoundUpToFiveMinutes(DateTime dt)
            {
                int mins = (int)(Math.Ceiling(dt.Minute / 5.0) * 5);
                return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0)
                       .AddMinutes(mins);
            }

            private void BtnOk_Click(object sender, EventArgs e)
            {
                var triggerTime = _datePicker.Value.Date + _timePicker.Value.TimeOfDay;
                if (triggerTime <= DateTime.Now)
                {
                    MessageBox.Show("La hora de la alarma debe ser en el futuro.",
                        "Hora inválida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                var name = _nameBox.Text.Trim();
                if (string.IsNullOrEmpty(name))
                    name = $"Alarma {triggerTime:dd/MM/yyyy HH:mm}";
                AlarmName  = name;
                AlarmTime  = triggerTime;
                DialogResult = DialogResult.OK;
                Close();
            }
        }
    }
}
