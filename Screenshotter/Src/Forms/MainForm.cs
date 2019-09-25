﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScreenshotTool
{
    public partial class MainForm : Form
    {
        public static KeyboardHook keyHook = new KeyboardHook(true);

        List<Screenshot> RecordedImages = new List<Screenshot>();
        List<Button> MiddleButtons = new List<Button>();
        int RecordedImagesIndex = 0;
        Point pMouseDown = new Point(0,0);
        Point pMouseCurrently = new Point(0, 0);
        bool IsMouseDown = false;
        float HUDvisibility = 0;
        float HUDVisiblity
        {
            get
            {
                if (HUDvisibility < 1)
                    return HUDvisibility;
                else
                    return 1;
            }
        }
        SnippingToolWindow Snipper = new SnippingToolWindow();
        bool snippingWindowActive = false;
        DateTime lastKeyDownEvent = DateTime.Now;

        const int HalfExtraPreviewImages = 2;
        const int PreviewImageWidth = 100;
        const int PreviewImageHeight = 56;
        const int PreviewImageOutlineThickness = 7;
        const int PreviewImagePadding = 12;
        const int SavedSignFontSize = 15;

        public MainForm()
        {
            InitializeComponent();
            keyHook.KeyDown += KeyHook_KeyDown;
            CurrentlyFocusedWindow.SetEventHook();
        }
        private void MainForm_Load(object sender, EventArgs e)
        {
            if (config.Default.path == "<Unset>" || !Directory.Exists(config.Default.path))
                config.Default.path = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (config.Default.windowSize.Width != 0)
                Size = config.Default.windowSize;

            MiddleButtons.Add(bSave);
            MiddleButtons.Add(bDelete);

            int MiddleButtonWidth = 0;
            foreach (Button B in MiddleButtons)
                MiddleButtonWidth += B.Width + 6;
            MiddleButtonWidth -= 6;
            for (int i = 0; i < MiddleButtons.Count; i++)
                MiddleButtons[i].Location = new Point(Width / 2 - MiddleButtonWidth / 2 + i * (6 + MiddleButtons[i].Width) - 8, MiddleButtons[i].Location.Y);

            int MinWidth = MiddleButtonWidth + 6 * (MiddleButtons.Count + 1) + 90;
            MinimumSize = new Size(MinWidth, MinimumSize.Height);

            MainForm_SizeChanged(null, EventArgs.Empty);
            Minimize();
            
            while (RecordedImages.Count == 0)
            {
                try
                {
                    string pngPath = Directory.GetFiles(config.Default.path).OrderBy(x => Path.GetFileName(x)).FirstOrDefault(x => x.EndsWith(".png"));
                    if (pngPath != null)
                        RecordedImages.Add(new Screenshot(pngPath));
                    else
                        RecordedImages.Add(new Screenshot(ScreenshotHelper.GetFullScreenshot(), GetScreenshotName()));
                } catch { }
            }
            UpdateWindowRatioWidth();
            UpdateUI();
        }
        
        // Helper Methods
        public Size GetProperRatioSize(Size S, bool WidthFirst, float Ratio)
        {
            if (WidthFirst)
            {
                S.Width = (int)(S.Height * Ratio);
                S.Height = (int)(S.Width * (1 / Ratio));
            }
            else
            {
                S.Height = (int)(S.Width * (1 / Ratio));
                S.Width = (int)(S.Height * Ratio);
            }
            return S;
        }
        public void AddScreenShot()
        {
            try
            {
                System.Media.SystemSounds.Exclamation.Play();
                RecordedImages.Add(new Screenshot(ScreenshotHelper.GetFullScreenshot(), GetScreenshotName()));
                RecordedImagesIndex = RecordedImages.Count - 1;
                UpdateUI();
            }
            catch (Exception e)
            {
                if (MessageBox.Show("Oopsie woopsie, it seems like I cant make that screenshot!\nDo you want to see the error message in detail?", 
                    "Error", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    MessageBox.Show(e.Message + "\n\n" + e.InnerException + "\n\n" + e.StackTrace);
            }
        }
        public void AddScreenShotSnippingToolStyle()
        {
            if (snippingWindowActive)
                return;

            lock (Snipper)
            {
                try
                {
                    snippingWindowActive = true;
                    System.Media.SystemSounds.Exclamation.Play();
                    Snipper.ShowDialog();
                    if (Snipper.output != null)
                    {
                        RecordedImages.Add(new Screenshot(Snipper.output, GetScreenshotName()));
                        RecordedImagesIndex = RecordedImages.Count - 1;
                        UpdateUI();

                        BSave_Click(null, EventArgs.Empty);

                        Location = new Point(Snipper.pMouseDown.X + Snipper.ImageDimensions.X - 8 - pBox.Location.X,
                            Snipper.pMouseDown.Y - 32 - pBox.Location.Y);

                        WindowState = FormWindowState.Normal;
                        DLLImports.SetForegroundWindow(Handle);

                        SetOriginalSize();

                        // Ausgleichen des blankParts
                        int imgWidth = pBox.Image.Width;
                        int imgHeight = pBox.Image.Height;
                        int boxWidth = pBox.Size.Width;
                        int boxHeight = pBox.Size.Height;
                        float X = 0;
                        float Y = 0;
                        if (imgWidth / imgHeight > boxWidth / boxHeight)
                        {
                            float scale = boxWidth / (float)imgWidth;
                            float blankPart = (boxHeight - scale * imgHeight) / 2;
                            Y = blankPart;
                        }
                        else
                        {
                            float scale = boxHeight / (float)imgHeight;
                            float blankPart = (boxWidth - scale * imgWidth) / 2;
                            X = blankPart;
                        }
                        Location = new Point(Location.X - (int)X, Location.Y - (int)Y);
                        Snipper.CleanUp();
                    }
                }
                catch (Exception e)
                {
                    if (MessageBox.Show("Oopsie woopsie, it seems like I cant make that screenshot!\nDo you want to see the error message in detail?", "Error", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        MessageBox.Show(e.Message + "\n\n" + e.InnerException + "\n\n" + e.StackTrace);
                    }
                }
                finally
                {
                    snippingWindowActive = false;
                }
            }
        }
        public void UpdateUI()
        {
            int count = 0;

            foreach (string s in Directory.GetFiles(config.Default.path))
                if (Path.GetFileNameWithoutExtension(s).Contains("Screenshot"))
                    count++;
            
            Text = "Screenshot Tool - " + count + " saved screenshots! - TargetDir: " + config.Default.path;
            pBox.Image = RecordedImages[RecordedImagesIndex].Image;
            if (RecordedImages[RecordedImagesIndex].Saved)
                bSave.Text = "To Clipboard";
            else
                bSave.Text = "Save";

            if (RecordedImagesIndex == 0)
                bDelete.Enabled = false;
            else
                bDelete.Enabled = true;
            if (RecordedImagesIndex == 0)
                bPrevious.Enabled = false;
            else
                bPrevious.Enabled = true;
            if (RecordedImagesIndex == RecordedImages.Count - 1)
                bNext.Enabled = false;
            else
                bNext.Enabled = true;
        }
        public void UpdateWindowRatioWidth()
        {
            Size R = GetProperRatioSize(pBox.Size, true, RecordedImages[RecordedImagesIndex].Image.Width / 
                RecordedImages[RecordedImagesIndex].Image.Height);
            Width += R.Width - pBox.Width;
            Height += R.Height - pBox.Height;
        }
        public void UpdateWindowRatioHeight()
        {
            Size R = GetProperRatioSize(pBox.Size, false, RecordedImages[RecordedImagesIndex].Image.Width /
                RecordedImages[RecordedImagesIndex].Image.Height);
            Height += R.Height - pBox.Height;
            Width += R.Width - pBox.Width;
        }
        public void SetOriginalSize()
        {
            Width = RecordedImages[RecordedImagesIndex].Image.Width + Width - pBox.Width;
            Height = RecordedImages[RecordedImagesIndex].Image.Height + Height - pBox.Height;

            // doppelt hält besser :thonk:
            Width = RecordedImages[RecordedImagesIndex].Image.Width + Width - pBox.Width;
            Height = RecordedImages[RecordedImagesIndex].Image.Height + Height - pBox.Height;
        }
        public void Minimize()
        {
            DLLImports.ShowWindow(this.Handle, 2);
        }
        Rectangle GetRectangleFromPoints(Point P1, Point P2)
        {
            int X = Math.Min(P1.X, P2.X);
            int Width = Math.Max(P1.X, P2.X) - X;
            int Y = Math.Min(P1.Y, P2.Y);
            int Height = Math.Max(P1.Y, P2.Y) - Y;
            return new Rectangle(X, Y, Width, Height);
        }
        public void DeleteCurrentImage()
        {
            if (RecordedImages.Count > 1)
            {
                RecordedImages.RemoveAt(RecordedImagesIndex);
                if (RecordedImagesIndex > RecordedImages.Count - 1)
                    RecordedImagesIndex = RecordedImages.Count - 1;
                UpdateUI();
                GC.Collect();
            }
        }
        public void CenterAroundMouse()
        {
            Location = new Point(MousePosition.X - Width / 2, MousePosition.Y - Height / 2);
        }
        public Point ZoomPicBoxCoordsToImageCoords(Point P, PictureBox pBox)
        {
            int imgWidth = pBox.Image.Width;
            int imgHeight = pBox.Image.Height;
            int boxWidth = pBox.Size.Width;
            int boxHeight = pBox.Size.Height;

            //This variable will hold the result
            float X = P.X;
            float Y = P.Y;
            //Comparing the aspect ratio of both the control and the image itself.
            if (imgWidth / imgHeight > boxWidth / boxHeight)
            {
                //If true, that means that the image is stretched through the width of the control.
                //'In other words: the image is limited by the width.

                //The scale of the image in the Picture Box.
                float scale = boxWidth / (float)imgWidth;

                //Since the image is in the middle, this code is used to determinate the empty space in the height
                //'by getting the difference between the box height and the image actual displayed height and dividing it by 2.
                float blankPart = (boxHeight - scale * imgHeight) / 2;

                Y -= blankPart;

                //Scaling the results.
                X /= scale;
                Y /= scale;
            }
            else
            {
                //If true, that means that the image is stretched through the height of the control.
                //'In other words: the image is limited by the height.

                //The scale of the image in the Picture Box.
                float scale = boxHeight / (float)imgHeight;

                //Since the image is in the middle, this code is used to determinate the empty space in the width
                //'by getting the difference between the box width and the image actual displayed width and dividing it by 2.
                float blankPart = (boxWidth - scale * imgWidth) / 2;
                X -= blankPart;

                //Scaling the results.
                X /= scale;
                Y /= scale;
            }
            return new Point((int)X, (int)Y);
        }
        public string GetScreenshotName()
        {
            TimeSpan t = (DateTime.UtcNow - new DateTime(1999, 5, 4));
            string fileName = "Screenshot_" + (long.MaxValue - (long)t.TotalMilliseconds);
            try { fileName += "_" + CurrentlyFocusedWindow.ProcessName; }
            catch { }
            return fileName;
        }
        public void CopyCurrentImageToClipboard()
        {
            Clipboard.SetImage(RecordedImages[RecordedImagesIndex].Image);
        }
        public void SaveCurrentImage()
        {
            RecordedImages[RecordedImagesIndex].Save();
            RecordedImages[RecordedImagesIndex].PutInClipboard();
            UpdateUI();
        }
        private void ResetHudVisibility() => HUDvisibility = (7.5f - HUDvisibility) / 3f;

        // Button Events
        private void BSave_Click(object sender, EventArgs e)
        {
            if (RecordedImages[RecordedImagesIndex].Saved)
                CopyCurrentImageToClipboard();
            else
                SaveCurrentImage();
        }
        private void BPath_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog FBD = new FolderBrowserDialog
            {
                SelectedPath = config.Default.path,
                Description = "Set the folder ill dump all the screensohts in."
            };
            if (FBD.ShowDialog() == DialogResult.OK)
                config.Default.path = FBD.SelectedPath;
            config.Default.Save();
            UpdateUI();
        }
        private void BPrevious_Click(object sender, EventArgs e)
        {
            RecordedImagesIndex--;
            UpdateUI();
        }
        private void BNext_Click(object sender, EventArgs e)
        {
            RecordedImagesIndex++;
            UpdateUI();
        }
        private void BOpen_Click(object sender, EventArgs e)
        {
            Process.Start("explorer.exe", "/open , \"" + config.Default.path);
        }
        private void BCropScreenshot_Click(object sender, EventArgs e)
        {
            AddScreenShotSnippingToolStyle();
        }
        private void BScreenshot_Click(object sender, EventArgs e)
        {
            AddScreenShot();
        }
        private void BDelete_Click(object sender, EventArgs e)
        {
            DeleteCurrentImage();
        }

        // pBox Events
        private void PBox_Paint(object sender, PaintEventArgs e)
        {
            if (IsMouseDown)
            {
                Rectangle ee = GetRectangleFromPoints(pMouseDown, pMouseCurrently);
                using (Pen pen = new Pen(Color.Red, 1))
                    e.Graphics.DrawRectangle(pen, ee);
            }
            if (RecordedImages[RecordedImagesIndex].Saved && pBox.Height > 8)
            {
                using (Pen pen = new Pen(Color.Red, 1))
                    e.Graphics.DrawString("Saved!", new Font("BigNoodleTitling", Math.Min(SavedSignFontSize, pBox.Height) + 1, FontStyle.Italic), 
                        Brushes.Red, new PointF(0, HUDVisiblity * (SavedSignFontSize + 15) - SavedSignFontSize - 15));
            }
            for (int i = RecordedImagesIndex - HalfExtraPreviewImages; i < RecordedImagesIndex + HalfExtraPreviewImages + 1; i++)
            {
                if (i >= 0 && i < RecordedImages.Count)
                {
                    int index = i - RecordedImagesIndex;
                    Rectangle draw = new Rectangle(pBox.Width / 2 - (PreviewImageWidth/2) + index * (PreviewImageWidth + PreviewImagePadding), 
                        pBox.Height - (int)Math.Min((PreviewImageHeight + PreviewImageOutlineThickness * 2) * HUDVisiblity, pBox.Height) + PreviewImageOutlineThickness, 
                        PreviewImageWidth, PreviewImageHeight);
                    
                    if (index == 0)
                        using (Pen pen = new Pen(Color.Black, PreviewImageOutlineThickness))
                            e.Graphics.DrawRectangle(pen, draw);
                    e.Graphics.DrawImage(RecordedImages[i].Image, draw);
                }
            }
        }
        private void PBox_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                ContextMenu m = new ContextMenu();
                //m.MenuItems.Add(new MenuItem("Fix Width Ratio", ((object s, EventArgs ev) =>
                //{
                //    try
                //    {
                //        UpdateWindowRatioWidth();
                //    }
                //    catch { }
                //})));
                //m.MenuItems.Add(new MenuItem("Fix Height Ratio", ((object s, EventArgs ev) =>
                //{
                //    try
                //    {
                //        UpdateWindowRatioHeight();
                //    }
                //    catch { }
                //})));
                GraphicsUnit Unit = GraphicsUnit.Pixel;
                if (RecordedImages[RecordedImagesIndex].Image.GetBounds(ref Unit).Width == ScreenshotHelper.allScreenBounds.Width)
                {
                    int i = 1;
                    foreach (Screen S in Screen.AllScreens)
                    {
                        m.MenuItems.Add(new MenuItem("Crop to " + i.ToShitEnglishNumberThingy() + " Screen", ((object s, EventArgs ev) =>
                        {
                            try
                            {
                                RecordedImages.Insert(RecordedImagesIndex + 1, new Screenshot(ScreenshotHelper.CropImage(RecordedImages[RecordedImagesIndex].Image,
                                    new Rectangle(S.Bounds.X - ScreenshotHelper.allScreenBounds.X,
                                    S.Bounds.Y - ScreenshotHelper.allScreenBounds.Y,
                                    S.Bounds.Width, S.Bounds.Height)), RecordedImages[RecordedImagesIndex].FileName + "_CROPPED"));
                                RecordedImagesIndex++;
                                UpdateUI();
                            }
                            catch { }
                        })));
                        i++;
                    }
                }
                m.MenuItems.Add(new MenuItem("1:1 Size", ((object s, EventArgs ev) =>
                {
                    try
                    {
                        SetOriginalSize();
                    }
                    catch { }
                })));
                m.MenuItems.Add(new MenuItem("Smol Size", ((object s, EventArgs ev) =>
                {
                    try
                    {
                        Height = 350;
                        Width = 350;
                        CenterAroundMouse();
                    }
                    catch { }
                })));
                m.Show(pBox, e.Location);
            }
        }
        // Cropping
        private void PBox_MouseMove(object sender, MouseEventArgs e)
        {
            ResetHudVisibility();

            pMouseCurrently = e.Location;
            pBox.Refresh();
        }
        private void PBox_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                pMouseDown = e.Location;
                IsMouseDown = true;
            }
        }
        private void PBox_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Rectangle crop = GetRectangleFromPoints(
                        ZoomPicBoxCoordsToImageCoords(new Point(pMouseDown.X, pMouseDown.Y), pBox),
                        ZoomPicBoxCoordsToImageCoords(new Point(pMouseCurrently.X, pMouseCurrently.Y), pBox));
                if (crop.Width == 0 || crop.Height == 0)
                {
                    IsMouseDown = false;
                    return;
                }
                RecordedImages.Insert(RecordedImagesIndex + 1, new Screenshot(ScreenshotHelper.CropImage(RecordedImages[RecordedImagesIndex].Image, crop), 
                    RecordedImages[RecordedImagesIndex].FileName + "_CROPPED"));
                RecordedImagesIndex = RecordedImagesIndex + 1;
                UpdateUI();
            }
            IsMouseDown = false;
        }

        // Other Events
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (RecordedImages.Skip(1).ToList().Exists(x => !x.Saved) && 
                MessageBox.Show("Oi, you have unsaved Images! Do you really want to close me?", "Close?", MessageBoxButtons.YesNo) == DialogResult.No)
                e.Cancel = true;
        }
        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            config.Default.windowSize = Size;
            config.Default.Save();
        }
        private void MainForm_SizeChanged(object sender, EventArgs e)
        {
            ResetHudVisibility();

            // Buttons
            bPrevious.Width = bSave.Location.X - bPrevious.Location.X - 6;
            bNext.Location = new Point(bDelete.Location.X + bDelete.Width + 6, bNext.Location.Y);
            bNext.Width = pBox.Width + pBox.Location.X - bNext.Location.X;

            if (Height < 120)
            {
                foreach (Button b in MiddleButtons)
                    b.Location = new Point(b.Location.X, 46);
                bNext.Location = new Point(bNext.Location.X, 46);
                bPrevious.Location = new Point(bPrevious.Location.X, 46);
            }
            else
            {
                foreach (Button b in MiddleButtons)
                    b.Location = new Point(b.Location.X, Height - (120 - 46));
                bNext.Location = new Point(bNext.Location.X, Height - (120 - 46));
                bPrevious.Location = new Point(bPrevious.Location.X, Height - (120 - 46));
            }

            //// Snapping
            //int slurpSize = 10;
            //Size R = GetProperRatioSize(pBox.Size, Math.Abs(LastSize.Width - Width) > Math.Abs(LastSize.Height - Height),
            //    RecordedImages[0].Width / RecordedImages[0].Height);

            //if (R.Width + slurpSize > pBox.Width && R.Width - slurpSize < pBox.Width)
            //    Width += R.Width - pBox.Width;
            //if (R.Height + slurpSize > pBox.Height && R.Height - slurpSize < pBox.Height)
            //    Height += R.Height - pBox.Height;

            if (WindowState == FormWindowState.Maximized)
                IsMouseDown = false;
        }
        private void Form1_ResizeEnd(object sender, EventArgs e)
        {
            HudDisappearance.Enabled = this.WindowState != FormWindowState.Minimized;
        }
        private void HudDisappearance_Tick(object sender, EventArgs e)
        {
            HUDvisibility -= 0.06f;
            if (HUDvisibility < 0)
                HUDvisibility = 0;
            pBox.Refresh();
        }
        private void KeyHook_KeyDown(Keys key, bool Shift, bool Ctrl, bool Alt)
        {
            if (DateTime.Now.Subtract(lastKeyDownEvent).TotalMilliseconds > 300 && 
                key == Keys.Pause)
            {
                if (Alt)
                    AddScreenShotSnippingToolStyle();
                else
                    AddScreenShot();

                lastKeyDownEvent = DateTime.Now;
            }
        }
        
        // ToolStrip
        private void NeuToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RecordedImages.Add(new Screenshot(new Bitmap(1000, 1000), "newBitmap"));
            RecordedImagesIndex = RecordedImages.Count - 1;
            UpdateUI();
        }
        private void ÖffnenToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }
        private void ShowFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start(config.Default.path);
        }
        private void SpeichernToolStripMenuItem_Click(object sender, EventArgs e) => SaveCurrentImage();
        private void ToClipboardToolStripMenuItem_Click(object sender, EventArgs e) => CopyCurrentImageToClipboard();
        private void BeendenToolStripMenuItem_Click(object sender, EventArgs e) => Application.Exit();
        private void OptionenToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }
        private void ChangeFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog FBD = new FolderBrowserDialog();
            FBD.SelectedPath = config.Default.path;
            if (FBD.ShowDialog() == DialogResult.OK)
            {
                config.Default.path = FBD.SelectedPath;
                Program.Restart();
                Application.Exit();
            }
        }
    }
}