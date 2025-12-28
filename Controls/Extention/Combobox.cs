using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace Next.Controls.Extention
{
    public static class Combobox
    {

        #region Placeholder / Hint Text for ComboBox (when nothing selected)

            private const uint CB_SETCUEBANNER = 0x1703;

            [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
            private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, string lParam);

            /// <summary>
            /// Sets a placeholder text for ComboBox when no item is selected.
            /// Works like TextBox placeholder.
            /// </summary>
            public static void SetPlaceholder(this ComboBox comboBox, string placeholder)
            {
                if (comboBox == null || comboBox.IsDisposed) return;
                SendMessage(comboBox.Handle, CB_SETCUEBANNER, IntPtr.Zero, placeholder);
            }

        #endregion

        #region Search-as-You-Type (Auto-filter dropdown while typing)

            /// <summary>
            /// Enables filtering dropdown items as user types (like modern search combobox).
            /// Case-insensitive, highlights matching text.
            /// </summary>
            public static void EnableSearchFiltering(this ComboBox comboBox, bool caseSensitive = false)
            {
                if (comboBox == null) return;
                if (comboBox.DropDownStyle != ComboBoxStyle.DropDown)
                    comboBox.DropDownStyle = ComboBoxStyle.DropDown;

                string originalText = comboBox.Text;
                comboBox.TextChanged += (s, e) =>
                {
                    var cb = comboBox;
                    if (cb.DroppedDown) return;

                    originalText = cb.Text;
                    var items = cb.Items;
                    var filtered = new System.Collections.Generic.List<object>();

                    string filter = caseSensitive ? cb.Text : cb.Text.ToLower();

                    for (int i = 0; i < items.Count; i++)
                    {
                        string itemText = items[i].ToString();
                        if (caseSensitive ? itemText.Contains(filter) : itemText.ToLower().Contains(filter))
                            filtered.Add(items[i]);
                    }

                    cb.Items.Clear();
                    cb.Items.AddRange(filtered.ToArray());
                    cb.SelectionStart = cb.Text.Length;
                    cb.SelectionLength = 0;
                    cb.DroppedDown = true;
                    cb.Text = originalText;
                    cb.SelectionStart = originalText.Length;
                };

                comboBox.Leave += (s, e) =>
                {
                    // Optional: restore all items when losing focus
                    // Or keep filtered — depends on preference
                };
            }

        #endregion

        #region Add Clear Button to ComboBox

            public static void AddClearButton(this ComboBox comboBox)
            {
                if (comboBox == null || comboBox.IsDisposed) return;

                Button clearBtn = new Button
                {
                    Text = "✕",
                    Font = new Font("Segoe UI", 8F),
                    FlatStyle = FlatStyle.Flat,
                    Size = new Size(20, 20),
                    Visible = comboBox.SelectedIndex != -1 || !string.IsNullOrEmpty(comboBox.Text)
                };
                clearBtn.FlatAppearance.BorderSize = 0;
                clearBtn.FlatAppearance.MouseOverBackColor = Color.LightGray;

                clearBtn.Click += (s, e) =>
                {
                    comboBox.SelectedIndex = -1;
                    comboBox.Text = string.Empty;
                    comboBox.Focus();
                };

                comboBox.Controls.Add(clearBtn);
                UpdateClearPosition(comboBox, clearBtn);

                EventHandler updateVisibility = (s, e) => clearBtn.Visible = comboBox.SelectedIndex != -1 || !string.IsNullOrEmpty(comboBox.Text);
                EventHandler resizeHandler = (s, e) => UpdateClearPosition(comboBox, clearBtn);

                comboBox.TextChanged += updateVisibility;
                comboBox.SelectedIndexChanged += updateVisibility;
                comboBox.SizeChanged += resizeHandler;

                comboBox.Disposed += (s, e) =>
                {
                    comboBox.TextChanged -= updateVisibility;
                    comboBox.SelectedIndexChanged -= updateVisibility;
                    comboBox.SizeChanged -= resizeHandler;
                    clearBtn.Dispose();
                };
            }

            private static void UpdateClearPosition(ComboBox comboBox, Button btn)
            {
                btn.Location = new Point(comboBox.ClientSize.Width - btn.Width - 24, (comboBox.ClientSize.Height - btn.Height) / 2);
                btn.BringToFront();
            }

        #endregion

        #region Binding

            public static void BindData(this ComboBox comboBox, DataTable data, string displayMember, string valueMember)
            {
                if (comboBox == null) return;
                if (data == null) return;

                comboBox.DataSource = data;
                comboBox.DisplayMember = displayMember;
                comboBox.ValueMember = valueMember;
                comboBox.SelectedIndex = -1;
            }

        #endregion
    
    }
}
