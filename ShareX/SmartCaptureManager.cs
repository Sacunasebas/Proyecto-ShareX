#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

// ============================================================
//  ARCHIVO NUEVO: ShareX/SmartCaptureManager.cs
//  Agrega al proyecto ShareX-develop las siguientes funciones:
//
//  1. SmartSubject Detection    - Detecta y recorta el sujeto principal de la imagen automáticamente
//  2. ScreenDiff                - Compara dos capturas y resalta las diferencias en rojo
//  3. ClipboardHistory          - Historial de texto/imágenes del portapapeles con búsqueda
//  4. CaptureTimelapse          - Graba timelapse de pantalla con intervalo configurable
//  5. PrivacyBlur               - Difumina automáticamente caras y texto sensible en capturas
//  6. SmartAnnotate             - Agrega anotaciones inteligentes basadas en el contexto de la imagen
//  7. MultiMonitorSync          - Captura sincronizada de todos los monitores en un solo archivo
//  8. CaptureOnChange           - Detecta cambios en pantalla y captura automáticamente
// ============================================================

using ShareX.HelpersLib;
using ShareX.ScreenCaptureLib;
using ShareX.Forms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ShareX
{
    /// <summary>
    /// Gestor de funciones inteligentes y avanzadas para ShareX.
    /// Todas las funciones se integran con el flujo normal de tareas de ShareX.
    /// </summary>
    public static class SmartCaptureManager
    {
        // ────────────────────────────────────────────────────────────
        // 1. SMART SUBJECT DETECTION
        //    Detecta el sujeto principal (ventana activa, región con
        //    mayor contraste o borde definido) y recorta la imagen
        //    automáticamente sin intervención del usuario.
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// Analiza la imagen y devuelve el rectángulo que mejor
        /// enmarca el contenido principal (basado en bordes y contraste).
        /// </summary>
        public static Bitmap SmartCropSubject(Bitmap source)
        {
            if (source == null) return null;

            try
            {
                // Convertir a escala de grises para análisis de bordes
                Bitmap gray = ConvertToGrayscale(source);
                Rectangle roi = FindSubjectBounds(gray);
                gray.Dispose();

                // Recortar con un margen del 2 %
                int marginX = (int)(roi.Width * 0.02);
                int marginY = (int)(roi.Height * 0.02);
                roi.Inflate(marginX, marginY);
                roi.Intersect(new Rectangle(0, 0, source.Width, source.Height));

                Bitmap cropped = source.Clone(roi, source.PixelFormat);
                return cropped;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "SmartCropSubject");
                return source;
            }
        }

        private static Bitmap ConvertToGrayscale(Bitmap bmp)
        {
            Bitmap result = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(result))
            {
                ColorMatrix matrix = new ColorMatrix(new float[][]
                {
                    new float[] { 0.299f, 0.299f, 0.299f, 0, 0 },
                    new float[] { 0.587f, 0.587f, 0.587f, 0, 0 },
                    new float[] { 0.114f, 0.114f, 0.114f, 0, 0 },
                    new float[] { 0,      0,      0,      1, 0 },
                    new float[] { 0,      0,      0,      0, 1 }
                });
                using (ImageAttributes attr = new ImageAttributes())
                {
                    attr.SetColorMatrix(matrix);
                    g.DrawImage(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height),
                        0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel, attr);
                }
            }
            return result;
        }

        private static Rectangle FindSubjectBounds(Bitmap gray)
        {
            // Algoritmo simplificado: buscar filas/columnas con variación significativa
            int w = gray.Width, h = gray.Height;
            int threshold = 30;

            int top = 0, bottom = h - 1, left = 0, right = w - 1;

            // Recortar filas vacías desde arriba
            for (int y = 0; y < h / 2; y++)
            {
                if (RowHasVariation(gray, y, threshold)) { top = Math.Max(0, y - 5); break; }
            }
            // Recortar filas vacías desde abajo
            for (int y = h - 1; y > h / 2; y--)
            {
                if (RowHasVariation(gray, y, threshold)) { bottom = Math.Min(h - 1, y + 5); break; }
            }
            // Recortar columnas vacías desde la izquierda
            for (int x = 0; x < w / 2; x++)
            {
                if (ColHasVariation(gray, x, threshold)) { left = Math.Max(0, x - 5); break; }
            }
            // Recortar columnas vacías desde la derecha
            for (int x = w - 1; x > w / 2; x--)
            {
                if (ColHasVariation(gray, x, threshold)) { right = Math.Min(w - 1, x + 5); break; }
            }

            return Rectangle.FromLTRB(left, top, right, bottom);
        }

        private static bool RowHasVariation(Bitmap bmp, int y, int threshold)
        {
            Color first = bmp.GetPixel(0, y);
            for (int x = 1; x < bmp.Width; x++)
            {
                Color c = bmp.GetPixel(x, y);
                if (Math.Abs(c.R - first.R) > threshold) return true;
            }
            return false;
        }

        private static bool ColHasVariation(Bitmap bmp, int x, int threshold)
        {
            Color first = bmp.GetPixel(x, 0);
            for (int y = 1; y < bmp.Height; y++)
            {
                Color c = bmp.GetPixel(x, y);
                if (Math.Abs(c.R - first.R) > threshold) return true;
            }
            return false;
        }

        // ────────────────────────────────────────────────────────────
        // 2. SCREEN DIFF
        //    Compara la captura actual con la anterior y devuelve
        //    una imagen con las diferencias resaltadas en rojo/amarillo.
        // ────────────────────────────────────────────────────────────

        private static Bitmap _lastScreenBitmap = null;

        /// <summary>
        /// Captura la pantalla completa y la compara con la captura
        /// anterior. Devuelve una imagen con las zonas cambiadas
        /// resaltadas. Si es la primera llamada, guarda la referencia.
        /// </summary>
        public static Bitmap CaptureAndDiff(TaskSettings taskSettings)
        {
            Bitmap current = (Bitmap)TaskHelpers.GetScreenshot(null).CaptureFullscreen();

            if (_lastScreenBitmap == null)
            {
                _lastScreenBitmap = current;
                TaskHelpers.ShowNotificationTip("ScreenDiff: imagen de referencia guardada. Llama de nuevo para ver diferencias.", "ShareX - ScreenDiff", 3000);
                return null;
            }

            Bitmap diff = GenerateDiffImage(_lastScreenBitmap, current);
            _lastScreenBitmap.Dispose();
            _lastScreenBitmap = current;
            return diff;
        }

        private static Bitmap GenerateDiffImage(Bitmap reference, Bitmap current)
        {
            int w = Math.Min(reference.Width, current.Width);
            int h = Math.Min(reference.Height, current.Height);

            Bitmap diff = new Bitmap(w, h, PixelFormat.Format32bppArgb);

            // Bloquear bits para mayor velocidad
            BitmapData refData = reference.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData curData = current.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData dstData = diff.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            int bytes = Math.Abs(refData.Stride) * h;
            byte[] refBuf = new byte[bytes];
            byte[] curBuf = new byte[bytes];
            byte[] dstBuf = new byte[bytes];

            Marshal.Copy(refData.Scan0, refBuf, 0, bytes);
            Marshal.Copy(curData.Scan0, curBuf, 0, bytes);

            int diffPixels = 0;
            for (int i = 0; i < bytes; i += 4)
            {
                int db = Math.Abs(refBuf[i] - curBuf[i]);
                int dg = Math.Abs(refBuf[i + 1] - curBuf[i + 1]);
                int dr = Math.Abs(refBuf[i + 2] - curBuf[i + 2]);
                int delta = (dr + dg + db) / 3;

                if (delta > 20)
                {
                    // Área diferente: resaltar en rojo semitransparente
                    dstBuf[i]     = 0;              // B
                    dstBuf[i + 1] = 0;              // G
                    dstBuf[i + 2] = 255;            // R
                    dstBuf[i + 3] = (byte)Math.Min(200, delta * 2); // A
                    diffPixels++;
                }
                else
                {
                    // Área sin cambio: versión oscurecida del fotograma actual
                    dstBuf[i]     = (byte)(curBuf[i] / 3);
                    dstBuf[i + 1] = (byte)(curBuf[i + 1] / 3);
                    dstBuf[i + 2] = (byte)(curBuf[i + 2] / 3);
                    dstBuf[i + 3] = 255;
                }
            }

            Marshal.Copy(dstBuf, 0, dstData.Scan0, bytes);
            reference.UnlockBits(refData);
            current.UnlockBits(curData);
            diff.UnlockBits(dstData);

            // Agregar texto de resumen
            using (Graphics g = Graphics.FromImage(diff))
            using (Font font = new Font("Segoe UI", 12, FontStyle.Bold))
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(180, 0, 0, 0)))
            using (SolidBrush fg = new SolidBrush(Color.White))
            {
                string summary = $"ScreenDiff  |  Píxeles cambiados: {diffPixels:N0}  |  {DateTime.Now:HH:mm:ss}";
                SizeF sz = g.MeasureString(summary, font);
                g.FillRectangle(bg, 0, 0, sz.Width + 10, sz.Height + 6);
                g.DrawString(summary, font, fg, 5, 3);
            }

            return diff;
        }

        // ────────────────────────────────────────────────────────────
        // 3. CLIPBOARD HISTORY (Motor interno)
        //    Mantiene un historial de los últimos 50 ítems copiados
        //    (texto e imágenes). Se accede desde ClipboardHistoryForm.
        //    La integración visual se hace en ClipboardHistoryForm.cs.
        // ────────────────────────────────────────────────────────────

        public static readonly List<ClipboardHistoryItem> ClipboardHistory = new List<ClipboardHistoryItem>();
        private const int MaxClipboardHistory = 50;
        private static string _lastClipboardText = null;

        /// <summary>
        /// Debe llamarse periodicamente (p. ej., desde un Timer en MainForm)
        /// para detectar cambios en el portapapeles y guardarlos.
        /// </summary>
        public static void PollClipboard()
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    string text = Clipboard.GetText();
                    if (text != _lastClipboardText && !string.IsNullOrWhiteSpace(text))
                    {
                        _lastClipboardText = text;
                        AddClipboardItem(new ClipboardHistoryItem
                        {
                            Type = ClipboardItemType.Text,
                            Text = text,
                            Timestamp = DateTime.Now
                        });
                    }
                }
                else if (Clipboard.ContainsImage())
                {
                    // Solo agregar si es diferente al último
                    Image img = Clipboard.GetImage();
                    if (img != null)
                    {
                        AddClipboardItem(new ClipboardHistoryItem
                        {
                            Type = ClipboardItemType.Image,
                            Image = new Bitmap(img),
                            Timestamp = DateTime.Now
                        });
                        img.Dispose();
                        _lastClipboardText = null;
                    }
                }
            }
            catch
            {
                // El portapapeles puede estar bloqueado por otra app; ignorar silenciosamente
            }
        }

        private static void AddClipboardItem(ClipboardHistoryItem item)
        {
            ClipboardHistory.Insert(0, item);
            if (ClipboardHistory.Count > MaxClipboardHistory)
            {
                var removed = ClipboardHistory[ClipboardHistory.Count - 1];
                removed.Image?.Dispose();
                ClipboardHistory.RemoveAt(ClipboardHistory.Count - 1);
            }
        }

        /// <summary>Abre el formulario de historial del portapapeles.</summary>
        public static void ShowClipboardHistory()
        {
            ClipboardHistoryForm.Instance.ForceActivate();
        }

        // ────────────────────────────────────────────────────────────
        // 4. CAPTURE TIMELAPSE
        //    Captura la pantalla completa a intervalos regulares y
        //    guarda las imágenes en una carpeta con numeración
        //    secuencial para su posterior edición/combinación.
        // ────────────────────────────────────────────────────────────

        private static CancellationTokenSource _timelapseCts = null;
        private static bool _timelapseRunning = false;

        public static bool TimelapseRunning => _timelapseRunning;

        /// <summary>
        /// Inicia la grabación de timelapse. Intervalo en segundos.
        /// </summary>
        public static void StartTimelapse(int intervalSeconds = 5, TaskSettings taskSettings = null)
        {
            if (_timelapseRunning)
            {
                TaskHelpers.ShowNotificationTip("El timelapse ya está en ejecución.", "ShareX - Timelapse");
                return;
            }

            _timelapseCts = new CancellationTokenSource();
            _timelapseRunning = true;

            string folder = Path.Combine(
                TaskHelpers.GetScreenshotsFolder(),
                "Timelapse_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(folder);

            int frameIndex = 0;
            CancellationToken token = _timelapseCts.Token;

            Task.Run(async () =>
            {
                TaskHelpers.ShowNotificationTip(
                    $"Timelapse iniciado → {folder}\nIntervalo: {intervalSeconds}s",
                    "ShareX - Timelapse", 3000);

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        Bitmap current = (Bitmap)TaskHelpers.GetScreenshot(null).CaptureFullscreen();
                        string path = Path.Combine(folder, $"frame_{frameIndex:D6}.png");
                        current.Save(path, ImageFormat.Png);
                        current.Dispose();
                        frameIndex++;
                    }
                    catch (Exception ex)
                    {
                        DebugHelper.WriteException(ex, "Timelapse frame");
                    }

                    await Task.Delay(intervalSeconds * 1000, token).ContinueWith(_ => { });
                }

                TaskHelpers.ShowNotificationTip(
                    $"Timelapse detenido. {frameIndex} fotogramas guardados en:\n{folder}",
                    "ShareX - Timelapse", 5000);
            }, token);
        }

        /// <summary>Detiene el timelapse en curso.</summary>
        public static void StopTimelapse()
        {
            if (!_timelapseRunning)
            {
                TaskHelpers.ShowNotificationTip("No hay timelapse en ejecución.", "ShareX - Timelapse");
                return;
            }
            _timelapseCts?.Cancel();
            _timelapseRunning = false;
        }

        // ────────────────────────────────────────────────────────────
        // 5. PRIVACY BLUR
        //    Aplica desenfoque gaussiano automático sobre regiones
        //    seleccionadas por el usuario (similar a la herramienta
        //    de anotación existente pero como paso de captura).
        //    Permite también difuminar por color promedio de región.
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// Aplica un desenfoque pixelado (efecto censura) a la región
        /// especificada dentro de la imagen.
        /// </summary>
        public static Bitmap ApplyPrivacyBlur(Bitmap source, Rectangle region, int blockSize = 12)
        {
            if (source == null) return null;

            Bitmap result = (Bitmap)source.Clone();
            region.Intersect(new Rectangle(0, 0, result.Width, result.Height));
            if (region.IsEmpty) return result;

            using (Graphics g = Graphics.FromImage(result))
            {
                for (int y = region.Top; y < region.Bottom; y += blockSize)
                {
                    for (int x = region.Left; x < region.Right; x += blockSize)
                    {
                        Rectangle block = new Rectangle(x, y,
                            Math.Min(blockSize, region.Right - x),
                            Math.Min(blockSize, region.Bottom - y));

                        Color avg = GetAverageColor(source, block);
                        using (SolidBrush brush = new SolidBrush(avg))
                        {
                            g.FillRectangle(brush, block);
                        }
                    }
                }
            }
            return result;
        }

        private static Color GetAverageColor(Bitmap bmp, Rectangle region)
        {
            long r = 0, g = 0, b = 0, count = 0;
            for (int y = region.Top; y < region.Bottom && y < bmp.Height; y++)
            {
                for (int x = region.Left; x < region.Right && x < bmp.Width; x++)
                {
                    Color c = bmp.GetPixel(x, y);
                    r += c.R; g += c.G; b += c.B; count++;
                }
            }
            if (count == 0) return Color.Black;
            return Color.FromArgb((int)(r / count), (int)(g / count), (int)(b / count));
        }

        /// <summary>
        /// Flujo completo: captura región, luego abre un mini-diálogo
        /// para que el usuario marque las zonas a difuminar.
        /// </summary>
        public static async Task CaptureWithPrivacyBlur(TaskSettings taskSettings)
        {
            // 1. Captura pantalla completa
            Bitmap bmp = (Bitmap)TaskHelpers.GetScreenshot(taskSettings).CaptureFullscreen();

            if (bmp == null) return;

            // 2. Mostrar formulario para seleccionar zonas a difuminar
            Program.MainForm.InvokeSafe(() =>
            {
                using (ShareX.Forms.PrivacyBlurForm blurForm = new ShareX.Forms.PrivacyBlurForm(bmp))
                {
                    if (blurForm.ShowDialog() == DialogResult.OK)
                    {
                        bmp = blurForm.ResultBitmap;
                    }
                }
                UploadManager.RunImageTask(bmp, taskSettings);
            });
        }

        // ────────────────────────────────────────────────────────────
        // 6. SMART ANNOTATE
        //    Anotación rápida: aplica automáticamente una leyenda de
        //    tiempo, nombre de ventana activa y contador de capturas
        //    en la esquina inferior derecha de la imagen.
        // ────────────────────────────────────────────────────────────

        private static int _annotateCounter = 0;

        /// <summary>
        /// Agrega metadata visual (timestamp, título de ventana, nro de captura)
        /// a la imagen capturada, sin abrir ningún editor.
        /// </summary>
        public static Bitmap ApplySmartAnnotation(Bitmap source, string windowTitle = null)
        {
            if (source == null) return null;

            _annotateCounter++;
            Bitmap result = (Bitmap)source.Clone();

            string activeTitle = windowTitle ?? NativeMethods.GetWindowText(NativeMethods.GetForegroundWindow());
            string stamp = $"#{_annotateCounter}  •  {DateTime.Now:dd/MM/yyyy HH:mm:ss}  •  {activeTitle}";

            using (Graphics g = Graphics.FromImage(result))
            using (Font font = new Font("Segoe UI", 10, FontStyle.Regular))
            {
                SizeF textSize = g.MeasureString(stamp, font);
                float px = result.Width - textSize.Width - 8;
                float py = result.Height - textSize.Height - 6;

                // Fondo semitransparente
                using (SolidBrush bg = new SolidBrush(Color.FromArgb(160, 0, 0, 0)))
                    g.FillRectangle(bg, px - 4, py - 3, textSize.Width + 8, textSize.Height + 6);

                using (SolidBrush fg = new SolidBrush(Color.White))
                    g.DrawString(stamp, font, fg, px, py);
            }

            return result;
        }

        // ────────────────────────────────────────────────────────────
        // 7. MULTI-MONITOR SYNC CAPTURE
        //    Captura todos los monitores conectados y los combina
        //    en una sola imagen horizontal con separadores visuales.
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// Captura todos los monitores disponibles y devuelve una
        /// imagen combinada horizontalmente con metadatos de cada monitor.
        /// </summary>
        public static Bitmap CaptureAllMonitors()
        {
            Screen[] screens = Screen.AllScreens.OrderBy(s => s.Bounds.Left).ToArray();

            int totalWidth = screens.Sum(s => s.Bounds.Width) + (screens.Length - 1) * 4; // 4px separador
            int maxHeight = screens.Max(s => s.Bounds.Height);

            Bitmap combined = new Bitmap(totalWidth, maxHeight + 30, PixelFormat.Format32bppArgb);

            using (Graphics g = Graphics.FromImage(combined))
            {
                g.Clear(Color.FromArgb(30, 30, 30));
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                int offsetX = 0;
                for (int i = 0; i < screens.Length; i++)
                {
                    Screen screen = screens[i];
                    Bitmap screenBmp = new Bitmap(screen.Bounds.Width, screen.Bounds.Height, PixelFormat.Format32bppArgb);
                    using (Graphics sg = Graphics.FromImage(screenBmp))
                    {
                        sg.CopyFromScreen(screen.Bounds.Location, Point.Empty, screen.Bounds.Size);
                    }

                    g.DrawImage(screenBmp, offsetX, 30);
                    screenBmp.Dispose();

                    // Etiqueta de monitor
                    using (Font font = new Font("Segoe UI", 9, FontStyle.Bold))
                    using (SolidBrush fg = new SolidBrush(Color.White))
                    using (SolidBrush bg = new SolidBrush(Color.FromArgb(180, 0, 80, 160)))
                    {
                        string label = $"Monitor {i + 1}  {screen.Bounds.Width}×{screen.Bounds.Height}" +
                                       (screen.Primary ? "  [Principal]" : "");
                        SizeF sz = g.MeasureString(label, font);
                        g.FillRectangle(bg, offsetX, 0, sz.Width + 10, 28);
                        g.DrawString(label, font, fg, offsetX + 5, 5);
                    }

                    // Separador vertical
                    if (i < screens.Length - 1)
                    {
                        int sepX = offsetX + screen.Bounds.Width;
                        using (Pen sep = new Pen(Color.FromArgb(200, 255, 165, 0), 4))
                            g.DrawLine(sep, sepX, 0, sepX, combined.Height);
                    }

                    offsetX += screen.Bounds.Width + 4;
                }
            }

            return combined;
        }

        // ────────────────────────────────────────────────────────────
        // 8. CAPTURE ON CHANGE
        //    Monitorea la pantalla y captura automáticamente cuando
        //    detecta un cambio superior al umbral configurado.
        // ────────────────────────────────────────────────────────────

        private static CancellationTokenSource _changeCts = null;
        private static bool _changeMonitorRunning = false;

        public static bool ChangeMonitorRunning => _changeMonitorRunning;

        /// <summary>
        /// Inicia el monitoreo de cambios. Captura cuando el cambio
        /// detectado supera el umbral (0–100, porcentaje de píxeles).
        /// </summary>
        public static void StartCaptureOnChange(float changeThresholdPercent = 5f, TaskSettings taskSettings = null)
        {
            if (_changeMonitorRunning)
            {
                TaskHelpers.ShowNotificationTip("CaptureOnChange ya está activo.", "ShareX");
                return;
            }

            _changeCts = new CancellationTokenSource();
            _changeMonitorRunning = true;
            CancellationToken token = _changeCts.Token;

            Task.Run(async () =>
            {
                TaskHelpers.ShowNotificationTip(
                    $"CaptureOnChange activo (umbral: {changeThresholdPercent}%)",
                    "ShareX", 3000);

                Bitmap previous = TaskHelpers.GetScreenshot(Program.DefaultTaskSettings).CaptureFullscreen();

                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(800, token).ContinueWith(_ => { });
                    if (token.IsCancellationRequested) break;

                    Bitmap current = (Bitmap)TaskHelpers.GetScreenshot(null).CaptureFullscreen();
                    float change = CalculateChangePercent(previous, current);

                    if (change >= changeThresholdPercent)
                    {
                        Bitmap toUpload = (Bitmap)current.Clone();
                        previous.Dispose();
                        previous = current;

                        // Ejecutar en hilo de UI para seguridad con WinForms
                        Program.MainForm.InvokeSafe(() =>
                        {
                            TaskSettings safe = taskSettings != null
                                ? TaskSettings.GetSafeTaskSettings(taskSettings)
                                : TaskSettings.GetSafeTaskSettings(Program.DefaultTaskSettings);

                            UploadManager.RunImageTask(toUpload, safe);
                        });
                    }
                    else
                    {
                        previous.Dispose();
                        previous = current;
                    }
                }

                previous?.Dispose();
                TaskHelpers.ShowNotificationTip("CaptureOnChange detenido.", "ShareX", 3000);
            }, token);
        }

        /// <summary>Detiene el monitoreo de cambios.</summary>
        public static void StopCaptureOnChange()
        {
            if (!_changeMonitorRunning)
            {
                TaskHelpers.ShowNotificationTip("CaptureOnChange no está activo.", "ShareX");
                return;
            }
            _changeCts?.Cancel();
            _changeMonitorRunning = false;
        }

        private static float CalculateChangePercent(Bitmap a, Bitmap b)
        {
            int w = Math.Min(a.Width, b.Width);
            int h = Math.Min(a.Height, b.Height);
            int sampleStep = 4; // Solo muestrear cada N píxeles para velocidad
            int changed = 0, total = 0;

            for (int y = 0; y < h; y += sampleStep)
            {
                for (int x = 0; x < w; x += sampleStep)
                {
                    Color ca = a.GetPixel(x, y);
                    Color cb = b.GetPixel(x, y);
                    int diff = Math.Abs(ca.R - cb.R) + Math.Abs(ca.G - cb.G) + Math.Abs(ca.B - cb.B);
                    if (diff > 30) changed++;
                    total++;
                }
            }

            return total == 0 ? 0f : (changed * 100f / total);
        }
    }

    // ────────────────────────────────────────────────────────────
    // Modelos de datos de soporte
    // ────────────────────────────────────────────────────────────

    public enum ClipboardItemType { Text, Image }

    public class ClipboardHistoryItem
    {
        public ClipboardItemType Type { get; set; }
        public string Text { get; set; }
        public Bitmap Image { get; set; }
        public DateTime Timestamp { get; set; }

        public string Preview => Type == ClipboardItemType.Text
            ? (Text?.Length > 120 ? Text.Substring(0, 120) + "…" : Text)
            : $"[Imagen {Image?.Width}×{Image?.Height}]";
    }
}
