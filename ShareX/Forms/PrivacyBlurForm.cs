#region License Information (GPL v3)
// ShareX – GPL v3
#endregion

// ============================================================
//  ARCHIVO NUEVO: ShareX/Forms/PrivacyBlurForm.cs
//  Formulario interactivo para seleccionar zonas a difuminar
//  sobre una captura antes de guardar/subir.
// ============================================================

using ShareX.HelpersLib;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ShareX.Forms
{
    /// <summary>
    /// Muestra la imagen capturada y permite al usuario pintar
    /// rectángulos de privacidad (censura pixelada) antes de
    /// continuar con el flujo de ShareX.
    /// </summary>
    public class PrivacyBlurForm : Form
    {
        public Bitmap ResultBitmap { get; private set; }

        private readonly Bitmap _original;
        private readonly List<Rectangle> _blurZones = new List<Rectangle>();
        private bool _drawing = false;
        private Point _dragStart;
        private Rectangle _currentRect = Rectangle.Empty;
        private PictureBox _canvas;
        private Label _hint;

        public PrivacyBlurForm(Bitmap source)
        {
            _original = source ?? throw new ArgumentNullException(nameof(source));
            ResultBitmap = (Bitmap)source.Clone();
            BuildUI();
        }

        private void BuildUI()
        {
            Text = "ShareX – Privacy Blur: dibuja las zonas a difuminar";
            BackColor = Color.FromArgb(20, 20, 24);
            ForeColor = Color.White;
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;

            // Calcular tamaño del formulario respetando la pantalla
            int maxW = Screen.PrimaryScreen.WorkingArea.Width - 80;
            int maxH = Screen.PrimaryScreen.WorkingArea.Height - 120;
            float ratio = Math.Min((float)maxW / _original.Width, (float)maxH / _original.Height);
            int cw = (int)(_original.Width * ratio);
            int ch = (int)(_original.Height * ratio);
            ClientSize = new Size(cw, ch + 70);

            _hint = new Label
            {
                Dock = DockStyle.Top,
                Height = 32,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "Arrastra para marcar zonas a difuminar  •  Clic derecho = deshacer última  •  Enter = aplicar  •  Esc = cancelar",
                BackColor = Color.FromArgb(35, 35, 42),
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 9)
            };

            _canvas = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = (Bitmap)_original.Clone(),
                BackColor = Color.Black,
                Cursor = Cursors.Cross
            };
            _canvas.MouseDown += Canvas_MouseDown;
            _canvas.MouseMove += Canvas_MouseMove;
            _canvas.MouseUp += Canvas_MouseUp;
            _canvas.Paint += Canvas_Paint;

            Panel panelButtons = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 38,
                BackColor = Color.FromArgb(30, 30, 36)
            };

            Button btnApply = new Button
            {
                Text = "✔  Aplicar y continuar",
                Width = 180, Height = 28, Left = 10, Top = 5,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 120, 60),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            btnApply.FlatAppearance.BorderSize = 0;
            btnApply.Click += (s, e) => ApplyAndClose();

            Button btnUndo = new Button
            {
                Text = "↩  Deshacer",
                Width = 110, Height = 28, Left = 200, Top = 5,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(80, 80, 95),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9)
            };
            btnUndo.FlatAppearance.BorderSize = 0;
            btnUndo.Click += (s, e) => UndoLast();

            Button btnCancel = new Button
            {
                Text = "✖  Cancelar",
                Width = 100, Height = 28, Left = 320, Top = 5,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(140, 40, 40),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9)
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            panelButtons.Controls.AddRange(new Control[] { btnApply, btnUndo, btnCancel });
            Controls.AddRange(new Control[] { _canvas, _hint, panelButtons });
        }

        // ── Dibujo interactivo ──────────────────────────────────

        private Point CanvasToImage(Point p)
        {
            // Convertir coordenadas del PictureBox (Zoom) a coordenadas de imagen
            float imgRatio = (float)_original.Width / _original.Height;
            float ctrlRatio = (float)_canvas.Width / _canvas.Height;
            RectangleF dest;
            if (imgRatio > ctrlRatio)
            {
                float h = _canvas.Width / imgRatio;
                dest = new RectangleF(0, (_canvas.Height - h) / 2f, _canvas.Width, h);
            }
            else
            {
                float w = _canvas.Height * imgRatio;
                dest = new RectangleF((_canvas.Width - w) / 2f, 0, w, _canvas.Height);
            }
            float ix = (p.X - dest.Left) * _original.Width / dest.Width;
            float iy = (p.Y - dest.Top) * _original.Height / dest.Height;
            return new Point((int)Math.Max(0, Math.Min(_original.Width - 1, ix)),
                             (int)Math.Max(0, Math.Min(_original.Height - 1, iy)));
        }

        private void Canvas_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _drawing = true;
                _dragStart = CanvasToImage(e.Location);
                _currentRect = Rectangle.Empty;
            }
            else if (e.Button == MouseButtons.Right)
            {
                UndoLast();
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_drawing)
            {
                Point cur = CanvasToImage(e.Location);
                _currentRect = Rectangle.FromLTRB(
                    Math.Min(_dragStart.X, cur.X), Math.Min(_dragStart.Y, cur.Y),
                    Math.Max(_dragStart.X, cur.X), Math.Max(_dragStart.Y, cur.Y));
                _canvas.Invalidate();
            }
        }

        private void Canvas_MouseUp(object sender, MouseEventArgs e)
        {
            if (_drawing && e.Button == MouseButtons.Left)
            {
                _drawing = false;
                if (_currentRect.Width > 4 && _currentRect.Height > 4)
                    _blurZones.Add(_currentRect);
                _currentRect = Rectangle.Empty;
                RedrawPreview();
            }
        }

        private void Canvas_Paint(object sender, PaintEventArgs e)
        {
            if (_drawing && !_currentRect.IsEmpty)
            {
                // Proyectar rectángulo de imagen a coordenadas del control
                // (usa la misma lógica inversa de CanvasToImage)
                float imgRatio = (float)_original.Width / _original.Height;
                float ctrlRatio = (float)_canvas.Width / _canvas.Height;
                RectangleF dest;
                if (imgRatio > ctrlRatio)
                {
                    float h = _canvas.Width / imgRatio;
                    dest = new RectangleF(0, (_canvas.Height - h) / 2f, _canvas.Width, h);
                }
                else
                {
                    float w = _canvas.Height * imgRatio;
                    dest = new RectangleF((_canvas.Width - w) / 2f, 0, w, _canvas.Height);
                }

                float sx = dest.Width / _original.Width;
                float sy = dest.Height / _original.Height;

                RectangleF visual = new RectangleF(
                    dest.Left + _currentRect.Left * sx,
                    dest.Top + _currentRect.Top * sy,
                    _currentRect.Width * sx,
                    _currentRect.Height * sy);

                using (Pen pen = new Pen(Color.OrangeRed, 2) { DashStyle = DashStyle.Dash })
                    e.Graphics.DrawRectangle(pen, visual.X, visual.Y, visual.Width, visual.Height);

                using (SolidBrush fill = new SolidBrush(Color.FromArgb(60, 255, 80, 0)))
                    e.Graphics.FillRectangle(fill, visual);
            }
        }

        private void RedrawPreview()
        {
            Bitmap preview = (Bitmap)_original.Clone();
            foreach (Rectangle r in _blurZones)
                preview = SmartCaptureManager.ApplyPrivacyBlur(preview, r, 14);
            _canvas.Image?.Dispose();
            _canvas.Image = preview;
        }

        private void UndoLast()
        {
            if (_blurZones.Count > 0)
            {
                _blurZones.RemoveAt(_blurZones.Count - 1);
                RedrawPreview();
            }
        }

        private void ApplyAndClose()
        {
            // Generar imagen final con todas las zonas difuminadas
            ResultBitmap = (Bitmap)_original.Clone();
            foreach (Rectangle r in _blurZones)
                ResultBitmap = SmartCaptureManager.ApplyPrivacyBlur(ResultBitmap, r, 14);

            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Return) ApplyAndClose();
            else if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
            else if (e.KeyCode == Keys.Z && e.Control) UndoLast();
        }
    }
}
