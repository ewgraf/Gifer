﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.VisualBasic.FileIO;

namespace gifer {
	public partial class GiferForm : Form {
		private GifImage _gifImage;
		private Image _currentFrame;
		private string _currentImagePath;
		private List<string> _imagesInFolder;
		private MoveFormWithControlsHandler _handler;
		private bool _helpWindow = true;

		public GiferForm() {
			this.Initialize();			
		}

		public GiferForm(string imagePath) : this() {
			this.groupBox1.Visible = false;
			this.labelDragAndDrop.Visible = false;
			_helpWindow = false;
			LoadImageAndFolder(imagePath);
		}

		private void Initialize() {
			_currentFrame = null;
			this.InitializeComponent();
			this.timer1.Stop();
			this.timerUpdateTaskbarIcon.Stop();
			this.FormBorderStyle = FormBorderStyle.None;
			this.AllowDrop = true;
			// Form.BackgroungImage flickers when updated, therefore can not be used as a control to draw a gif on, so we have to use PictureBox
			this.pictureBox1.MouseWheel += pictureBox1_MouseWheel;
			this.pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;
			this.pictureBox1.Image = null;
			Screen currentScreen = Screen.FromControl(this);
			Point center = (Point)currentScreen.Bounds.Size.Divide(2);
			this.Location = Point.Subtract(center, this.Size.Divide(2));
			Control[] controls = this.Controls.ToArray().Concat(this.groupBox1.Controls.ToArray()).ToArray();
			_handler = new MoveFormWithControlsHandler(form: this, controls: controls);
			
			foreach (Control c in controls) {
				c.MouseClick += (s, e) => pictureBox1_MouseClick(s, e);
			}			
		}

		private void Reinitialize() {
			this.Controls.Clear();
			this.Initialize();
		}

		//private void SetupStandalone(bool start) {
		//    if (start && !_openWithListener.Running) {
		//        Task.Run(() => _openWithListener.Start(filePath => {
		//            try {
		//                LoadImageAndFolder(filePath);
		//            } catch (Exception ex) {
		//                MessageBox.Show(ex.ToString());
		//                Application.Exit();
		//            }
		//        }));
		//    } else if (!start && _openWithListener.Running) {
		//        _openWithListener.Stop();
		//    }
		//}

        private void LoadImageAndFolder(string imagePath) {
			if (string.IsNullOrEmpty(imagePath)) {
                return;
            }
            if (Gifer.KnownImageFormats.Any(imagePath.ToUpper().EndsWith)) {
				Bitmap image = (Bitmap)LoadImage(imagePath);
				if (image == null) {
					MessageBox.Show($"Can not load image: '{imagePath}'");
				}
                if (this.InvokeRequired) {
                    this.Invoke(new MethodInvoker(() => {
                        SetImage(image);
                    }));
                } else {
                    SetImage(image);
                }
				_currentImagePath = imagePath;
				_imagesInFolder = Directory.GetFiles(Path.GetDirectoryName(_currentImagePath))
					.Where(path => Gifer.KnownImageFormats.Any(path.ToUpper().EndsWith))
					.ToList();
			} else {
				MessageBox.Show($"Unknown image extension at: '{imagePath}' '{Path.GetExtension(imagePath)}'");
			}
		}

		private Image LoadImage(string imagePath) {
			try {
				switch (Path.GetExtension(imagePath)) {
					default:
						return Image.FromFile(imagePath);
				}
			} catch (Exception ex) {
				MessageBox.Show(ex.ToString());
				return null;
			}
		}

		private void SetImage(Bitmap image) {
            //SuspendDrawing(this);
            timer1.Stop();
            timerUpdateTaskbarIcon.Stop();
			Screen currentScreen = Screen.FromControl(this);
			if (image.Width > currentScreen.Bounds.Width || image.Height > currentScreen.Bounds.Height) {
				this.Size = ResizeProportionaly(image.Size, currentScreen.Bounds.Size);
			} else {
				this.Size = image.Size;
			}
			
            Point center = (Point)currentScreen.Bounds.Size.Divide(2);
            this.Location = Point.Subtract(center, this.Size.Divide(2));

            pictureBox1.Image?.Dispose();
            pictureBox1.Image = null;
            _gifImage?.Dispose();
            GC.Collect();
            if (image.RawFormat == ImageFormat.Gif && ImageAnimator.CanAnimate(image) 
                || image.RawFormat.Guid == new Guid("b96b3cb0-0728-11d3-9d7b-0000f81ef32e")) {
				_gifImage = new GifImage(image);
				//pictureBox1.Image = _gifImage.Next();
                timer1.Interval = _gifImage.CurrentFrameDelayMilliseconds;
                timer1.Start();
                timerUpdateTaskbarIcon.Start();
            } else { // if plain image
                pictureBox1.Image = image;
                this.Icon = Icon.FromHandle(((Bitmap)image).GetHicon());                
            }
			_currentFrame = image;
            this.BringToFront();
            //ResumeDrawing(this);
		}

		public static Size ResizeProportionaly(Size size, Size fitSize) {
            double ratioX = (double)fitSize.Width  / (double)size.Width;
            double ratioY = (double)fitSize.Height / (double)size.Height;
            double ratio  = Math.Min(ratioX, ratioY);
            return size.Multiply(ratio);
		}
		
        private void Form1_DragEnter(object sender, DragEventArgs e) {
            e.Effect = DragDropEffects.All;
        }

		private void Form1_DragDrop(object sender, DragEventArgs e) {
			this.groupBox1.Visible = false;
			this.labelDragAndDrop.Visible = false;
			_helpWindow = false;
			string imagePath = ((string[])e.Data.GetData(DataFormats.FileDrop))[0];
			LoadImageAndFolder(imagePath);
			this.Activate();
		}

		#region Resizing

		private bool _resizing;

		public void pictureBox1_MouseWheel(object sender, MouseEventArgs e) {
			if (_helpWindow) {
				return;
			}

			pictureBox1_Resize(sender, e);
		}
		
		private void pictureBox1_Resize(object sender, EventArgs e) {
			var args = e as MouseEventArgs;
			if(args == null) { // if resize is caused not by mouse wheel, but by 'pictureBox1.Size = ' or '+='.
				return;
			}

			if (_resizing) {
				//_resizing = false;
				return;
			}

			int delta = args.Delta;
			if(delta == 0) {
				return;
			}

			float ratio = 1.35f;
			if (ModifierKeys.HasFlag(Keys.Control)) {
				ratio = 1.05f;
			} else if (ModifierKeys.HasFlag(Keys.Shift)) {
				ratio = 2.0f;
			}

			_resizing = true;
			Zoom(Math.Sign(delta)*ratio, Screen.FromControl(this), this, this.pictureBox1);
			_resizing = false;
		}

        // Gaussiana [0.01, 0.05, 0.1, 0.15, 0.2, 0.25, 0.3, 0.35, 0.4, 0.35, 0.3, 0.25, 0.2, 0.15, 0.1, 0.05, 0.01] / 3 => Sum = ~1
        //private static double[] Gaussiana = new[] { 0.003, 0.016, 0.03, 0.05, 0.06, 0.083, 0.1, 0.116,  0.13,  0.116, 0.1, 0.083, 0.06, 0.05, 0.03, 0.016, 0.003 };
        //private static int[] Gaussiana = new[] { 64, 32, 16, 8, 4, 2, 4, 8, 16, 32, 64 };
        //private float Gauss(float x) {            
        //    return exp(-(x - mu) ^ 2 / (2 * sigma ^ 2)) / sqrt(2 * pi * sigma ^ 2)
        //}

        [DllImport("User32.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int w, int h, bool repaint);

        private void Zoom(float ratio, Screen screen, Form form, PictureBox pictureBox) {
            float enlargementRatio = AnimationHelper.GetEnlargementValue(ratio);
            var newSize = new SizeF {
                Width = form.Size.Width * enlargementRatio,
				Height = form.Size.Height * enlargementRatio
            };
            SizeF widening = newSize - form.Size;
            var newLocation = PointF.Add(form.Location, widening.Divide(-2));
			form.MaximumSize = new Size(int.MaxValue, int.MaxValue);
			Point cursorLocationOnImage = this.pictureBox1.PointToClient(Cursor.Position);
			float xRatio = cursorLocationOnImage.X / (float)form.Size.Width;
			float yRatio = cursorLocationOnImage.Y / (float)form.Size.Height;
			var newCursorPosition = new PointF(newSize.Width * xRatio, newSize.Height * yRatio);
			float widthDifference = (newSize.Width - form.Size.Width) / 2;
			float heightDifference = (newSize.Height - form.Size.Height) / 2;
			float shiftX = (widthDifference - (newCursorPosition.X - cursorLocationOnImage.X));
			float shiftY = (heightDifference - (newCursorPosition.Y - cursorLocationOnImage.Y));
			MoveWindow(this.Handle,
					   (int)Math.Round(newLocation.X + shiftX),
					   (int)Math.Round(newLocation.Y + (int)shiftY),
					   (int)Math.Round(newSize.Width),
					   (int)Math.Round(newSize.Height),
					   repaint: false);
		}

		private void ZoomSmooth(float ratio, Form form, PictureBox pictureBox) {
            Size size;
            if (pictureBox.Width >= Screen.PrimaryScreen.Bounds.Width &&
                pictureBox.Height >= Screen.PrimaryScreen.Bounds.Height) {
                size = pictureBox.Size;
            } else {
                size = form.Size;
            }

            Point location;
            if (pictureBox.Width >= Screen.PrimaryScreen.Bounds.Width &&
                pictureBox.Height >= Screen.PrimaryScreen.Bounds.Height) {
                location = pictureBox.Location;
            } else {
                location = form.Location;
            }
            
            float enlargementRatio = AnimationHelper.GetEnlargementValue(ratio);
            var newSize = new Size {
                Width  = Convert.ToInt32(size.Width  * enlargementRatio),
                Height = Convert.ToInt32(size.Height * enlargementRatio)
            };
            Size widening = newSize - size;
            var newLocation = Point.Add(location, widening.Divide(-2));
            //   const
            // ---------- -> steps : so that when 'widening' rizes, 'steps' reduses, to resize larger window faster
            //  widening
            //
            // Ex: let widening 64 -> be steps 64, diff 128 -> steps 32
            //    c
            // -------- -> 64 steps of resizing => c = 64*64 = 4096
            //    64
            // so let it be 4096. pretty round, huh
            int steps = 4096 / (Math.Abs(widening.Width + widening.Height) / 2);
            Debug.WriteLine($"Steps: {steps}");
            widening = widening.Divide(steps).RoundToPowerOf2();
            Size shift = widening.Divide(2);
            //parent.Size = newSize;
            //parent.Location = newLocation;
            while (_resizing && !ModifierKeys.HasFlag(Keys.Alt) && (pictureBox.Size - newSize).AbsMore(widening)) {
                pictureBox.Size += widening;
                //Application.DoEvents();
                //Application.DoEvents();
                //Application.DoEvents();
                if (this.Size.Width < Screen.PrimaryScreen.Bounds.Width &&
                    this.Size.Height < Screen.PrimaryScreen.Bounds.Height) {
                    form.Size += widening;
                    form.Location -= shift;
                } else {
                    pictureBox.Location -= shift;
                }
                //parent.Size += widening;
                //Application.DoEvents();
                //Application.DoEvents();
                //parent.Location -= shift;
                Application.DoEvents();
            }
            if (this.Size.Width < Screen.PrimaryScreen.Bounds.Width &&
                this.Size.Height < Screen.PrimaryScreen.Bounds.Height) {
                form.Size += widening;
                form.Location -= shift;
            } else {
                pictureBox.Size = newSize;
                pictureBox.Location = newLocation;
            }
        }

		#endregion

		private void GiferForm_KeyDown(object sender, KeyEventArgs e) {
			if (_currentImagePath != null && (e.KeyCode == Keys.Right || e.KeyCode == Keys.Left)) {
				if (e.KeyCode == Keys.Right) {
					_currentImagePath = _imagesInFolder.Next(_currentImagePath);
				} else if (e.KeyCode == Keys.Left) {
					_currentImagePath = _imagesInFolder.Previous(_currentImagePath);
				}
				SetImage((Bitmap)Bitmap.FromFile(_currentImagePath));
			} else if (e.KeyCode == Keys.H) {
				_helpWindow = true;
				this.Reinitialize();
			} else if (e.KeyCode == Keys.Delete) {
				if (_currentImagePath == null) {
					return;
				}
				string imageToDeletePath = _currentImagePath;
				_currentImagePath = _imagesInFolder.Next(_currentImagePath);
				LoadImageAndFolder(_currentImagePath);
				_imagesInFolder.Remove(imageToDeletePath);
				if (imageToDeletePath == _currentImagePath) {
					_currentImagePath = null;
					this.Reinitialize();
				}
				FileSystem.DeleteFile(imageToDeletePath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
			} else if (e.Modifiers == Keys.Control && e.KeyCode == Keys.P) {
				if (_currentImagePath == null) {
					return;
				}
				var p = new Process();
				p.StartInfo.FileName = _currentImagePath;
				p.StartInfo.Verb = "Print";
				p.Start();
			} else if (e.KeyCode == Keys.Escape) {
				Application.Exit();
			} else if (e.KeyCode == Keys.D1) {
				PaintWith(InterpolationMode.NearestNeighbor);
			} else if (e.KeyCode == Keys.D2) {
				PaintWith(InterpolationMode.Bilinear);
			} else if (e.KeyCode == Keys.D3) {
				PaintWith(InterpolationMode.HighQualityBilinear);
			} else if (e.KeyCode == Keys.D4) {
				PaintWith(InterpolationMode.HighQualityBicubic);
			}
		}

		private void timer1_Tick(object sender, EventArgs e) {
            pictureBox1.Image = _gifImage.Next();
			this.timer1.Interval = _gifImage.CurrentFrameDelayMilliseconds;
        }

        private void timerUpdateTaskbarIcon_Tick(object sender, EventArgs e) {
            if (pictureBox1.Image != null) {
                this.Icon = Icon.FromHandle((PadImage(pictureBox1.Image)).GetHicon());
            }            
        }

        public static Bitmap PadImage(Image image) {
            int largestDimension = Math.Max(image.Height, image.Width);
            var squareSize = new Size(largestDimension, largestDimension);
            Bitmap squareImage = new Bitmap(squareSize.Width, squareSize.Height);
            using (Graphics graphics = Graphics.FromImage(squareImage)) {
                //graphics.FillRectangle(Brushes.White, 0, 0, squareSize.Width, squareSize.Height);
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Low;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                graphics.DrawImage(image, (squareSize.Width / 2) - (image.Width / 2), (squareSize.Height / 2) - (image.Height / 2), image.Width, image.Height);
            }
            return squareImage;
        }

        private void pictureBox1_MouseClick(object sender, MouseEventArgs e) {
            if (e.Button == MouseButtons.Right) {
                Application.Exit();
            }
        }

        private void GiferForm_Activated(object sender, EventArgs e) {
            this.TopMost = true;
            Debug.WriteLine("this.TopMost: " + this.TopMost);
        }

        private void GiferForm_Deactivate(object sender, EventArgs e) {
            this.TopMost = false;
            Debug.WriteLine("this.TopMost: " + this.TopMost);
        }

		private void groupBox1_DragDrop(object s, DragEventArgs e) => this.Form1_DragDrop(s, e);

		private void pictureBox1_Paint(object sender, PaintEventArgs e) {
			//e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
			e.Graphics.InterpolationMode = _interpolationMode;
			if (_currentFrame != null) {
				e.Graphics.DrawImage(
					_currentFrame,
					new Rectangle(0, 0, this.Width, this.Height), // destination rectangle
					0, 0, // upper-left corner of source rectangle
					_currentFrame.Width, // width of source rectangle
					_currentFrame.Height, // height of source rectangle
					GraphicsUnit.Pixel);
			} else {
				base.OnPaint(e);
			}
			//base.OnPaint(e);
		}

		private InterpolationMode _interpolationMode;

		private void PaintWith(InterpolationMode interpolationMode) {
			_interpolationMode = interpolationMode;
			this.pictureBox1.Invalidate();
		}

		private void GiferForm_Load(object sender, EventArgs e) {
			this.MaximumSize = new Size(int.MaxValue, int.MaxValue);
		}
	}
}
