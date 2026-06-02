#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2026 ShareX Team
    GPL v3 – see LICENSE.txt
*/

#endregion License Information (GPL v3)

// ============================================================
//  ARCHIVO NUEVO: ShareX/Forms/ClipboardHistoryForm.cs
//  Formulario del Historial del Portapapeles.
//  Accesible desde el HotkeyType.ClipboardHistory o desde
//  el menú Tools de MainForm.
// ============================================================

using ShareX.HelpersLib;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ShareX.Forms
{
    public class ClipboardHistoryForm : Form
    {
        // Singleton flotante (se reutiliza igual que ActionsToolbarForm)
        private static ClipboardHistoryForm _instance;
        public static ClipboardHistoryForm Instance
        {
            get
            {
                if (_instance == null || _instance.IsDisposed)
                    _instance = new ClipboardHistoryForm();
                return _instance;
            }
        }

        public static bool IsInstanceActive => _instance != null && !_instance.IsDisposed && _instance.Visible;

        public void ForceActivate()
        {
            Show();
            BringToFront();
            Activate();
            RefreshList();
        }

        // ─── Controles ───────────────────────────────────────────
        private TextBox txtSearch;
        private ListBox lstItems;
        private PictureBox picPreview;
        private Label lblInfo;
        private Button btnCopy;
        private Button btnClear;
        private Button btnClose;
        private Panel panelTop;
        private Panel panelBottom;
        private SplitContainer split;

        private ClipboardHistoryForm()
        {
            BuildUI();
            NativeMethods.UseImmersiveDarkMode(Handle, true);
        }

        private void BuildUI()
        {
            Text = "ShareX – Historial del Portapapeles";
            Size = new Size(720, 480);
            MinimumSize = new Size(560, 360);
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(24, 24, 28);
            ForeColor = Color.White;

            // ── Panel superior (búsqueda) ─────────────────────────
            panelTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = Color.FromArgb(32, 32, 38),
                Padding = new Padding(6, 6, 6, 0)
            };
            txtSearch = new TextBox
            {
                Dock = DockStyle.Fill,
                PlaceholderText = "Buscar en historial…",
                BackColor = Color.FromArgb(45, 45, 52),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10)
            };
            txtSearch.TextChanged += (s, e) => RefreshList();
            panelTop.Controls.Add(txtSearch);

            // ── SplitContainer (lista | vista previa) ─────────────
            split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                BackColor = Color.FromArgb(24, 24, 28),
                Panel1MinSize = 200,
                Panel2MinSize = 180,
                SplitterWidth = 4,
                SplitterDistance = 300
            };

            lstItems = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(36, 36, 42),
                ForeColor = Color.WhiteSmoke,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9),
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 44,
                IntegralHeight = false
            };
            lstItems.DrawItem += LstItems_DrawItem;
            lstItems.SelectedIndexChanged += LstItems_SelectedIndexChanged;
            lstItems.DoubleClick += (s, e) => CopySelected();

            picPreview = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(20, 20, 25)
            };

            lblInfo = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.LightGray,
                BackColor = Color.FromArgb(28, 28, 34),
                Padding = new Padding(6, 0, 0, 0)
            };

            split.Panel1.Controls.Add(lstItems);
            split.Panel2.Controls.Add(picPreview);
            split.Panel2.Controls.Add(lblInfo);

            // ── Panel inferior (botones) ──────────────────────────
            panelBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 42,
                BackColor = Color.FromArgb(32, 32, 38)
            };

            btnCopy = new Button
            {
                Text = "Copiar al portapapeles",
                Width = 180,
                Height = 30,
                Left = 8,
                Top = 6,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 100, 180),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9)
            };
            btnCopy.FlatAppearance.BorderSize = 0;
            btnCopy.Click += (s, e) => CopySelected();

            btnClear = new Button
            {
                Text = "Limpiar historial",
                Width = 130,
                Height = 30,
                Left = 200,
                Top = 6,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(140, 40, 40),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9)
            };
            btnClear.FlatAppearance.BorderSize = 0;
            btnClear.Click += (s, e) =>
            {
                if (MessageBox.Show("¿Limpiar todo el historial?", "ShareX",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    SmartCaptureManager.ClipboardHistory.ForEach(i => i.Image?.Dispose());
                    SmartCaptureManager.ClipboardHistory.Clear();
                    RefreshList();
                }
            };

            btnClose = new Button
            {
                Text = "Cerrar",
                Width = 80,
                Height = 30,
                Left = 344,
                Top = 6,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 68),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9)
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => Hide();

            panelBottom.Controls.AddRange(new Control[] { btnCopy, btnClear, btnClose });

            Controls.AddRange(new Control[] { split, panelTop, panelBottom });
        }

        private void LstItems_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            ClipboardHistoryItem item = (ClipboardHistoryItem)lstItems.Items[e.Index];

            bool selected = (e.State & DrawItemState.Selected) != 0;
            Color bg = selected ? Color.FromArgb(0, 80, 150) : (e.Index % 2 == 0
                ? Color.FromArgb(36, 36, 42)
                : Color.FromArgb(42, 42, 50));

            e.Graphics.FillRectangle(new SolidBrush(bg), e.Bounds);

            // Icono de tipo
            string icon = item.Type == ClipboardItemType.Text ? "📝" : "🖼";
            using (Font f = new Font("Segoe UI Emoji", 14))
                e.Graphics.DrawString(icon, f, Brushes.White, e.Bounds.Left + 4, e.Bounds.Top + 10);

            // Texto principal
            using (Font fMain = new Font("Segoe UI", 9, FontStyle.Regular))
            using (Font fTime = new Font("Segoe UI", 8, FontStyle.Regular))
            {
                string preview = item.Preview ?? "";
                e.Graphics.DrawString(preview, fMain, selected ? Brushes.White : Brushes.WhiteSmoke,
                    new RectangleF(e.Bounds.Left + 32, e.Bounds.Top + 4, e.Bounds.Width - 36, 24));
                e.Graphics.DrawString(item.Timestamp.ToString("HH:mm:ss  dd/MM/yyyy"),
                    fTime, Brushes.Gray, e.Bounds.Left + 32, e.Bounds.Top + 26);
            }
        }

        private void LstItems_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstItems.SelectedItem is ClipboardHistoryItem item)
            {
                lblInfo.Text = $"  Tipo: {item.Type}  •  {item.Timestamp:dd/MM/yyyy HH:mm:ss}";
                if (item.Type == ClipboardItemType.Image)
                {
                    picPreview.Image = item.Image;
                }
                else
                {
                    picPreview.Image = null;
                    // Mostrar texto como mini "captura"
                    Bitmap textBmp = RenderTextPreview(item.Text, picPreview.Size);
                    picPreview.Image = textBmp;
                }
            }
        }

        private Bitmap RenderTextPreview(string text, Size size)
        {
            Bitmap bmp = new Bitmap(Math.Max(size.Width, 200), Math.Max(size.Height, 120));
            using (Graphics g = Graphics.FromImage(bmp))
            using (Font font = new Font("Consolas", 9))
            {
                g.Clear(Color.FromArgb(20, 20, 25));
                g.DrawString(text ?? "", font, Brushes.LightGray,
                    new RectangleF(6, 6, bmp.Width - 12, bmp.Height - 12));
            }
            return bmp;
        }

        public void RefreshList()
        {
            string query = txtSearch.Text?.ToLowerInvariant() ?? "";
            lstItems.BeginUpdate();
            lstItems.Items.Clear();
            var filtered = SmartCaptureManager.ClipboardHistory
                .Where(i => string.IsNullOrEmpty(query) ||
                            (i.Text?.ToLowerInvariant().Contains(query) == true) ||
                            i.Type.ToString().ToLowerInvariant().Contains(query));
            foreach (var item in filtered)
                lstItems.Items.Add(item);
            lstItems.EndUpdate();
        }

        private void CopySelected()
        {
            if (lstItems.SelectedItem is ClipboardHistoryItem item)
            {
                try
                {
                    if (item.Type == ClipboardItemType.Text)
                        Clipboard.SetText(item.Text);
                    else if (item.Image != null)
                        Clipboard.SetImage(item.Image);
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteException(ex, "ClipboardHistory Copy");
                }
            }
        }
    }
}
