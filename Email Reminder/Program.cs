using System;
using System.Collections.Generic;
using System.Drawing;
using System.Media;
using System.Windows.Forms;

namespace Email_Reminder
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new EmailReminderForm());
        }

        public partial class EmailReminderForm : Form
        {
            private List<DateTime> checkTimes;
            private Timer timer1;
            private Label labelNextCheck;
            private NotifyIcon trayIcon;
            private TextBox[] checkTimeInputs;
            private Label[] checkTimeLabels;
            private DateTime checkNextTime;
            private Panel modalPanel;
            private Panel headerPanel;
            private Button closeButton;
            private Button minimizeButton;
            private Panel contentPanel;
            private Icon icon = new Icon("ringbell.ico");
            private bool _isMouseDown;
            private Point _mouseOffset; 
            private SoundPlayer _simpleSound = new SoundPlayer("R2D2.wav");

            public object Process { get; private set; }

            public EmailReminderForm()
            {
                InitializeComponent();
                SetDefaultCheckTimes();
                UpdateNextSchedule();
                timer1.Start();
                MinimizeButton_Click(null, null);
            }

            private void InitializeComponent()
            {
                // Initialize timer
                timer1 = new Timer();
                timer1.Interval = 5000; // Check every 5 seconds
                timer1.Tick += timer1_Tick;

                // Initialize label
                labelNextCheck = new Label();
                labelNextCheck.Location = new Point(12, 20);
                labelNextCheck.Size = new Size(400, 23);
                labelNextCheck.Text = "Next check time: ";
                labelNextCheck.ForeColor = Color.White;
                labelNextCheck.Font = new Font("Segoe UI", 14F, FontStyle.Bold);

                // Initialize check time inputs and labels
                checkTimeInputs = new TextBox[8];
                checkTimeLabels = new Label[8];
                int inputY = 100;
                for (int i = 0; i < 8; i++)
                {
                    checkTimeLabels[i] = new Label();
                    checkTimeLabels[i].Location = new Point(12, inputY);
                    checkTimeLabels[i].Size = new Size(80, 23);
                    checkTimeLabels[i].Text = $"Reminder {i + 1}";
                    checkTimeLabels[i].ForeColor = Color.White;
                    Controls.Add(checkTimeLabels[i]);

                    checkTimeInputs[i] = new TextBox();
                    checkTimeInputs[i].Location = new Point(100, inputY);
                    checkTimeInputs[i].Size = new Size(80, 23);
                    checkTimeInputs[i].TextChanged += textBox_TextChanged;
                    Controls.Add(checkTimeInputs[i]);

                    inputY += 30;
                }

                // System tray icon
                trayIcon = new NotifyIcon();
                trayIcon.Icon = icon;
                trayIcon.Visible = true;
                trayIcon.DoubleClick += trayIcon_DoubleClick;

                // Form settings
                AutoScaleDimensions = new SizeF(7F, 15F);
                AutoScaleMode = AutoScaleMode.Font;
                ClientSize = new Size(284, 370);
                Controls.Add(labelNextCheck);
                Name = "EmailReminderForm";
                Text = "Email Reminder";
                StartPosition = FormStartPosition.CenterScreen;
                FormBorderStyle = FormBorderStyle.FixedSingle;
                MaximizeBox = false;
                MinimizeBox = true;
                FormBorderStyle = FormBorderStyle.None;
                WindowState = FormWindowState.Normal;
                StartPosition = FormStartPosition.CenterParent;
                Icon = icon;
                Resize += Form1_Resize;
                MouseDown += Form_MouseDown;
                MouseMove += Form_MouseMove;
                MouseUp += Form_MouseUp;

                // Set form properties
                FormBorderStyle = FormBorderStyle.None;
                BackColor = Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(51)))), ((int)(((byte)(51)))));
                StartPosition = FormStartPosition.CenterScreen;
                Size = new Size(600, 310);

                // Create modal panel
                modalPanel = new Panel();
                modalPanel.Dock = DockStyle.Fill;
                modalPanel.Padding = new Padding(15);
                Controls.Add(modalPanel);

                // Create header panel
                headerPanel = new Panel();
                headerPanel.Dock = DockStyle.Top;
                headerPanel.Height = 50;
                headerPanel.BackColor = Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
                modalPanel.Controls.Add(headerPanel);
                headerPanel.Controls.Add(labelNextCheck);
                headerPanel.MouseDown += Form_MouseDown;
                headerPanel.MouseMove += Form_MouseMove;
                headerPanel.MouseUp += Form_MouseUp;

                // Add minimize button
                minimizeButton = new Button();
                minimizeButton.Dock = DockStyle.Right;
                minimizeButton.FlatStyle = FlatStyle.Flat;
                minimizeButton.FlatAppearance.BorderSize = 0;
                minimizeButton.ForeColor = Color.White;
                minimizeButton.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
                minimizeButton.Text = "-";
                minimizeButton.Click += MinimizeButton_Click;
                headerPanel.Controls.Add(minimizeButton);

                // Add close button
                closeButton = new Button();
                closeButton.Dock = DockStyle.Right;
                closeButton.FlatStyle = FlatStyle.Flat;
                closeButton.FlatAppearance.BorderSize = 0;
                closeButton.ForeColor = Color.White;
                closeButton.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
                closeButton.Text = "✕";
                closeButton.Click += CloseButton_Click;
                headerPanel.Controls.Add(closeButton);

                // Create content panel
                contentPanel = new Panel();
                contentPanel.Dock = DockStyle.Fill;
                contentPanel.Padding = new Padding(15);
                modalPanel.Controls.Add(contentPanel);
            }

            private void SetDefaultCheckTimes()
            {
                checkTimes = new List<DateTime>
                {
                    new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 9, 30, 0),
                    new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 10, 10, 0),
                    new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 11, 0, 0),
                    new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 11, 50, 0),
                    new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 14, 20, 0),
                    new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 15, 0, 0),
                    new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 16, 0, 0),
                    new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 16, 50, 0)
                };

                for (int i = 0; i < 8; i++)
                {
                    checkTimeInputs[i].Text = checkTimes[i].ToString("HH:mm");
                }
            }

            private void UpdateNextSchedule()
            {
                DateTime nextCheck = GetNextCheckTime();
                checkNextTime = nextCheck;
                labelNextCheck.Text = $"Next email check at: {nextCheck:dddd H:mm}";
            }

            private DateTime GetNextCheckTime()
            {
                DateTime now = DateTime.Now;
                for (int i = 0; i < checkTimeInputs.Length; i++)
                {
                    DateTime checkTime = ParseDateTime(now, checkTimeInputs[i].Text);
                    if (checkTime > now && checkTime.DayOfWeek != DayOfWeek.Saturday && checkTime.DayOfWeek != DayOfWeek.Sunday)
                    {
                        return checkTime;
                    }
                }

                int daysToAdd = 1;

                switch (now.DayOfWeek)
                {
                    case DayOfWeek.Friday:
                        daysToAdd = 3;
                        break;
                    case DayOfWeek.Saturday:
                        daysToAdd = 2;
                        break;
                    default:
                        break;
                }

                return ParseDateTime(now.AddDays(daysToAdd), checkTimeInputs[0].Text);
            }

            private DateTime ParseDateTime(DateTime today, string timeString)
            {
                if (DateTime.TryParseExact(timeString, "HH:mm", null, System.Globalization.DateTimeStyles.None, out DateTime result))
                {
                    return new DateTime(today.Year, today.Month, today.Day, result.Hour, result.Minute, 0);
                }
                return today;
            }

            private void textBox_TextChanged(object sender, EventArgs e)
            {
                UpdateNextSchedule();
            }

            private void timer1_Tick(object sender, EventArgs e)
            {
                if (DateTime.Now >= checkNextTime)
                {
                    UpdateNextSchedule();
                    ShowBootstrapModal("Atention", "Check whatsapp and email accounts!");
                }
            }

            private void ShowBootstrapModal(string title, string message)
            {
                Form modalForm = new Form();
                modalForm.FormBorderStyle = FormBorderStyle.None;
                modalForm.WindowState = FormWindowState.Normal;
                modalForm.StartPosition = FormStartPosition.CenterParent;
                modalForm.BackColor = Color.FromArgb(((int)(((byte)(50)))), ((int)(((byte)(50)))), ((int)(((byte)(50)))));
                modalForm.Size = new Size(400, 200);
                modalForm.TopMost = true;

                TableLayoutPanel tableLayout = new TableLayoutPanel();
                tableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
                tableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
                tableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
                tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                tableLayout.Dock = DockStyle.Fill;
                tableLayout.Padding = new Padding(20);

                Label titleLabel = new Label();
                titleLabel.Text = title;
                titleLabel.ForeColor = Color.White;
                titleLabel.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
                titleLabel.Dock = DockStyle.Fill;
                titleLabel.TextAlign = ContentAlignment.MiddleCenter;

                Label messageLabel = new Label();
                messageLabel.Text = message;
                messageLabel.ForeColor = Color.White;
                messageLabel.Font = new Font("Segoe UI", 12F);
                messageLabel.Dock = DockStyle.Fill;
                messageLabel.TextAlign = ContentAlignment.MiddleCenter;

                Button closeButton = new Button();
                closeButton.Text = "Close";
                closeButton.Font = new Font("Segoe UI", 12F);
                closeButton.ForeColor = Color.White;
                closeButton.BackColor = Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(122)))), ((int)(((byte)(204)))));
                closeButton.FlatStyle = FlatStyle.Flat;
                closeButton.FlatAppearance.BorderSize = 0;
                closeButton.Dock = DockStyle.Fill;
                closeButton.Click += (sender, args) => { 
                    modalForm.Close();
                    _simpleSound.Stop();
                };
                
                _simpleSound.Play();

                tableLayout.Controls.Add(titleLabel, 0, 0);
                tableLayout.Controls.Add(messageLabel, 0, 1);
                tableLayout.Controls.Add(closeButton, 0, 2);
                modalForm.Controls.Add(tableLayout);
                modalForm.ShowDialog();
            }

            private void CloseButton_Click(object sender, EventArgs e)
            {
                Close();
            }

            private void MinimizeButton_Click(object sender, EventArgs e)
            {
                WindowState = FormWindowState.Minimized;
                Form1_Resize(sender, e);
            }

            private void Form1_Resize(object sender, EventArgs e)
            {
                if (WindowState == FormWindowState.Minimized)
                {
                    Hide();
                    trayIcon.Visible = true;
                    ShowInTaskbar = false;
                }
            }

            private void trayIcon_DoubleClick(object sender, EventArgs e)
            {
                Show();
                WindowState = FormWindowState.Normal;
                trayIcon.Visible = false;
                ShowInTaskbar = true;
            }

            private void Form_MouseDown(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                {
                    _isMouseDown = true;
                    _mouseOffset = e.Location;
                }
            }

            private void Form_MouseMove(object sender, MouseEventArgs e)
            {
                if (_isMouseDown)
                {
                    Point currentScreenPos = PointToScreen(e.Location);
                    Location = new Point(currentScreenPos.X - _mouseOffset.X, currentScreenPos.Y - _mouseOffset.Y);
                }
            }

            private void Form_MouseUp(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                {
                    _isMouseDown = false;
                }
            }
        }
    }
}
