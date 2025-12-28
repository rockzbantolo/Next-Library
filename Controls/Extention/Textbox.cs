using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Next.Controls.Extention
{
    public static class Textbox
    {
        // Default
        private static readonly Image DefaultEyeOpen = LoadDefault("Next.Assets.Images.eye_opened.png");
        private static readonly Image DefaultEyeClosed = LoadDefault("Next.Assets.Images.eye_closed.png");
        private static readonly Image DefaultSearchIcon = LoadDefault("Next.Assets.search.png");

        // Placeholder
        private const uint ECM_FIRST = 0x1500;
        private const uint EM_SETCUEBANNER = ECM_FIRST + 1;
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, string lParam);


        #region Password Visibility Toggle

            private static Image LoadDefault(string name)
            {
                try
                {
                    var asm = Assembly.GetExecutingAssembly();
                    using (var s = asm.GetManifestResourceStream(name))
                        return s != null ? Image.FromStream(s) : null;
                }
                catch { return null; }
            }

            public static void ShowPasswordEye(this TextBox tb, Image eyeOpen = null, Image eyeClosed = null)
            {
                if (tb == null) return;

                HidePasswordEye(tb);

                var openImg = eyeOpen ?? DefaultEyeOpen;
                var closeImg = eyeClosed ?? DefaultEyeClosed;

                if (openImg == null || closeImg == null)
                    return; // no icons available

                var btn = new Button
                {
                    Width = 20,
                    Height = tb.Height + 2,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.Transparent,
                    BackgroundImageLayout = ImageLayout.Zoom,
                    Cursor = Cursors.Default,
                    Tag = "prime_eye"
                };
                btn.FlatAppearance.BorderSize = 0;

                // Start hidden
                btn.BackgroundImage = closeImg;
                tb.UseSystemPasswordChar = true;

                btn.Click += (s, e) =>
                {
                    tb.UseSystemPasswordChar = !tb.UseSystemPasswordChar;
                    btn.BackgroundImage = tb.UseSystemPasswordChar ? closeImg : openImg;
                };

                // Perfect right position
                btn.Location = new Point(tb.ClientRectangle.Width - btn.Width - 4, -2);
                tb.Controls.Add(btn);

                // Keep perfect position
                tb.SizeChanged += (s, e) =>
                    btn.Location = new Point(tb.ClientRectangle.Width - btn.Width - 4, -2);
            }

            private static void HidePasswordEye(this TextBox tb)
            {
                if (tb == null) return;
                for (int i = tb.Controls.Count - 1; i >= 0; i--)
                {
                    if (tb.Controls[i] is Button b && (string)b.Tag == "prime_eye")
                    {
                        tb.Controls.RemoveAt(i);
                        b.Dispose();
                    }
                }
            }

        #endregion

        #region Clear Button

            public static void AddClearButton(this TextBox textBox)
            {
                if (textBox == null || textBox.IsDisposed) return;

                Button clearBtn = new Button
                {
                    Text = "✕",
                    Font = new Font("Segoe UI", 8F),
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = Color.Gray,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Default,
                    Size = new Size(20, 20),
                    Visible = !string.IsNullOrEmpty(textBox.Text)
                };

                clearBtn.FlatAppearance.BorderSize = 0;
                clearBtn.FlatAppearance.MouseOverBackColor = Color.LightGray;

                clearBtn.Click += (s, e) =>
                {
                    textBox.Clear();
                    textBox.Focus();
                };

                textBox.Controls.Add(clearBtn);
                UpdateClearButtonPosition(textBox, clearBtn);

                // Handlers with lambda to allow easy removal
                EventHandler textChangedHandler = (s, e) => clearBtn.Visible = !string.IsNullOrEmpty(textBox.Text);
                EventHandler sizeChangedHandler = (s, e) => UpdateClearButtonPosition(textBox, clearBtn);
                LayoutEventHandler layoutHandler = (s, e) => UpdateClearButtonPosition(textBox, clearBtn);

                textBox.TextChanged += textChangedHandler;
                textBox.SizeChanged += sizeChangedHandler;
                textBox.ParentChanged += (s, e) => textBox.Layout += layoutHandler; // Re-position on parent layout

                // Clean up on dispose to prevent leaks
                textBox.Disposed += (s, e) =>
                {
                    textBox.TextChanged -= textChangedHandler;
                    textBox.SizeChanged -= sizeChangedHandler;
                    clearBtn.Dispose();
                };
            }

            private static void UpdateClearButtonPosition(TextBox textBox, Button clearBtn)
            {
                clearBtn.Location = new Point(textBox.ClientSize.Width - clearBtn.Width - 4,
                                                (textBox.ClientSize.Height - clearBtn.Height) / 2);
                clearBtn.BringToFront();
            }

        #endregion

        #region Input Validations

            public static void AllowOnlyNumbers(this TextBox textBox, bool allowDecimal = false, bool allowNegative = false)
            {
                textBox.KeyPress += (sender, e) =>
                {
                    if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
                    {
                        if (allowDecimal && e.KeyChar == '.' && !textBox.Text.Contains('.'))
                            return;
                        if (allowNegative && e.KeyChar == '-' && textBox.SelectionStart == 0 && !textBox.Text.Contains('-'))
                            return;
                        e.Handled = true;
                    }
                };
            }

            public static void ValidateWithRegex(this TextBox textBox, string pattern, Color errorColor)
            {
                if (textBox == null) return;
                Color normalColor = textBox.BackColor;

                textBox.Validating += (sender, e) =>
                {
                    bool valid = string.IsNullOrEmpty(textBox.Text) || Regex.IsMatch(textBox.Text, pattern);
                    textBox.BackColor = valid ? normalColor : errorColor;
                };

                // Reset on focus if you want
                textBox.Enter += (s, e) => { if (textBox.BackColor == errorColor) textBox.BackColor = normalColor; };
            }

            // Auto Case
            public static void AutoUpperCase(this TextBox textBox)
                {
                    textBox.TextChanged += (s, e) =>
                    {
                        var tb = textBox;
                        int selStart = tb.SelectionStart;
                        tb.Text = tb.Text.ToUpper();
                        tb.SelectionStart = selStart;
                    };
                }

            public static void AutoLowerCase(this TextBox textBox)
            {
                textBox.TextChanged += (s, e) =>
                {
                    var tb = textBox;
                    int selStart = tb.SelectionStart;
                    tb.Text = tb.Text.ToLower();
                    tb.SelectionStart = selStart;
                };
            }

            public static void AutoTitleCase(this TextBox textBox)
            {
                textBox.TextChanged += (s, e) =>
                {
                    var tb = textBox;
                    int selStart = tb.SelectionStart;
                    System.Globalization.TextInfo textInfo = System.Globalization.CultureInfo.CurrentCulture.TextInfo;
                    tb.Text = textInfo.ToTitleCase(tb.Text.ToLower());
                    tb.SelectionStart = selStart;
                };
            }

        #endregion

        #region Masked Input (Phone, Date, etc.)

            public static void ApplyPhoneMask(this TextBox textBox)
            {
                // Allows formats like (123) 456-7890 or 123-456-7890
                textBox.KeyPress += (s, e) =>
                {
                    if (char.IsControl(e.KeyChar)) return;
                    if (!char.IsDigit(e.KeyChar)) e.Handled = true;
                };

                textBox.TextChanged += (s, e) =>
                {
                    var tb = textBox;
                    var text = new string(tb.Text.Where(char.IsDigit).ToArray());
                    tb.TextChanged -= (EventHandler)s; // Prevent recursion

                    if (text.Length >= 10)
                        tb.Text = $"({text.Substring(0, 3)}) {text.Substring(3, 3)}-{text.Substring(6, Math.Min(4, text.Length - 6))}";
                    else if (text.Length >= 7)
                        tb.Text = $"{text.Substring(0, 3)}-{text.Substring(3, 3)}-{text.Substring(6)}";
                    else if (text.Length >= 4)
                        tb.Text = $"{text.Substring(0, 3)}-{text.Substring(3)}";

                    tb.SelectionStart = tb.Text.Length;
                    tb.TextChanged += (EventHandler)s;
                };
            }

            public static void ApplyDateMask(this TextBox textBox)
            {
                // MM/DD/YYYY
                textBox.MaxLength = 10;
                textBox.KeyPress += (s, e) => { if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true; };

                textBox.TextChanged += (s, e) =>
                {
                    var tb = textBox;
                    var text = new string(tb.Text.Where(char.IsDigit).ToArray());
                    tb.TextChanged -= (EventHandler)s;

                    if (text.Length >= 5)
                        tb.Text = $"{text.Substring(0, 2)}/{text.Substring(2, 2)}/{text.Substring(4, Math.Min(4, text.Length - 4))}";
                    else if (text.Length >= 3)
                        tb.Text = $"{text.Substring(0, 2)}/{text.Substring(2)}";

                    tb.SelectionStart = tb.Text.Length;
                    tb.TextChanged += (EventHandler)s;
                };
            }

        #endregion

        #region Placeholder

            public static void SetPlaceholder(this TextBox textBox, string placeholder, bool showWhenFocused = false)
            {
                if (textBox == null) throw new ArgumentNullException(nameof(textBox));
                if (textBox.IsDisposed) return;

                // wParam: TRUE = show even when focused, FALSE = hide when focused
                IntPtr wParam = new IntPtr(showWhenFocused ? 1 : 0);
                SendMessage(textBox.Handle, EM_SETCUEBANNER, wParam, placeholder);
            }

            public static void RemovePlaceholder(this TextBox textBox)
            {
                if (textBox == null || textBox.IsDisposed) return;
                SendMessage(textBox.Handle, EM_SETCUEBANNER, IntPtr.Zero, string.Empty);
            }

        #endregion

        #region Reusable Left Icon (Uses Your Embedded Default + Optional Custom Image)

            /// <summary>
            /// Adds a left icon to the TextBox.
            /// Uses the library's embedded default search icon if no custom image is provided.
            /// </summary>
            /// <param name="textBox">The TextBox to add the icon to.</param>
            /// <param name="customImage">Optional custom image. If null, uses the default embedded search icon.</param>
            /// <param name="iconSize">Optional icon size (default 20x20).</param>
            public static void AddLeftIcon(this TextBox textBox, Image customImage = null, Size? iconSize = null)
            {
                if (textBox == null || textBox.IsDisposed) return;

                Size size = iconSize ?? new Size(20, 20);
                int horizontalPadding = size.Width + 8; // icon width + margin

                PictureBox iconBox = new PictureBox
                {
                    Size = size,
                    Location = new Point(6, (textBox.Height - size.Height) / 2),
                    BackColor = Color.Transparent,
                    SizeMode = PictureBoxSizeMode.StretchImage,
                    Image = customImage ?? DefaultSearchIcon  // Use default if none provided
                };

                textBox.Controls.Add(iconBox);
                iconBox.BringToFront();

                // Add left padding to prevent text from overlapping the icon
                textBox.Padding = new Padding(
                    textBox.Padding.Left + horizontalPadding,
                    textBox.Padding.Top,
                    textBox.Padding.Right,
                    textBox.Padding.Bottom
                );

                // Reposition on resize
                EventHandler resizeHandler = (s, e) =>
                {
                    iconBox.Location = new Point(6, (textBox.Height - iconBox.Height) / 2);
                };
                textBox.SizeChanged += resizeHandler;

                // Cleanup to prevent memory leaks
                textBox.Disposed += (s, e) =>
                {
                    textBox.SizeChanged -= resizeHandler;
                    // Don't dispose DefaultSearchIcon (it's shared and static)
                    if (iconBox.Image != null && iconBox.Image != DefaultSearchIcon)
                        iconBox.Image.Dispose();
                    iconBox.Dispose();
                };
            }

        #endregion
    
    }

}
