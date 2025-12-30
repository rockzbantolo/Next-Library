using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Next.Controls.Extention
{
    public class Radius
    {
        /// <summary>
        /// Sample Usage:
        /// 
        /// 1. Apply default style to specific controls
        /// new RadiusBatch()
        ///  .Default(10, Color.Gray, 2)
        ///  .Add(panel1, panel2)
        ///  .Add(lb_Title, radius: 5, borderColor: Color.Red)
        ///  .Apply();
        ///  
        /// 2. Apply to all panels on a form
        /// new RadiusBatch()
        ///  .Default(12, Color.Black, 1)
        ///  .AddToAllChildren(this, c => c is Panel)
        ///  .Apply();
        ///  
        /// </summary>
        public sealed class RadiusBatch
        {
            private readonly List<(Control control, RadiusStyle style)> _controls = new();
            private RadiusStyle _defaultStyle = new RadiusStyle(6, Color.White, 1);

            // Set global defaults
            public RadiusBatch Default(int radius = 0, Color? borderColor = null, int? width = null)
            {
                _defaultStyle = new RadiusStyle(
                    radius,
                    borderColor ?? _defaultStyle.BorderColor,
                    width ?? _defaultStyle.BorderWidth
                );
                return this;
            }

            // Add single control with optional overrides
            public RadiusBatch Add(Control control, int? radius = null, Color? borderColor = null, int? borderWidth = null)
            {
                if (control == null) return this;

                var style = new RadiusStyle(
                    radius ?? _defaultStyle.Radius,
                    borderColor ?? _defaultStyle.BorderColor,
                    borderWidth ?? _defaultStyle.BorderWidth
                );

                _controls.Add((control, style));
                return this;
            }

            // Add multiple controls with default style
            public RadiusBatch Add(params Control[] controls)
            {
                foreach (var c in controls)
                    if (c != null)
                        _controls.Add((c, new RadiusStyle(_defaultStyle.Radius, _defaultStyle.BorderColor, _defaultStyle.BorderWidth)));
                return this;
            }

            // Apply radius to all children recursively, optional filter
            public RadiusBatch AddToAllChildren(
                Control parent,
                Func<Control, bool> filter = null,
                int? radius = null,
                Color? borderColor = null,
                int? borderWidth = null)
            {
                if (parent == null) return this;

                void AddRecursive(Control c)
                {
                    foreach (Control child in c.Controls)
                    {
                        if ((filter == null || filter(child)) && !_controls.Any(x => x.control == child))
                        {
                            var style = new RadiusStyle(
                                radius ?? _defaultStyle.Radius,
                                borderColor ?? _defaultStyle.BorderColor,
                                borderWidth ?? _defaultStyle.BorderWidth
                            );
                            _controls.Add((child, style));
                        }

                        if (child.HasChildren)
                            AddRecursive(child);
                    }
                }

                AddRecursive(parent);
                return this;
            }

            // Apply all stored radius styles
            public void Apply()
            {
                if (_controls.Count == 0) return;

                var provider = new RadiusProvider();
                foreach (var (control, style) in _controls)
                {
                    provider.SetRadius(control, style.Radius);
                    provider.SetBorderColor(control, style.BorderColor);
                    provider.SetBorderWidth(control, style.BorderWidth);
                }
            }
        }

        // Helper struct for radius style
        public readonly struct RadiusStyle
        {
            public int Radius { get; }
            public Color BorderColor { get; }
            public int BorderWidth { get; }

            public RadiusStyle(int radius, Color borderColor, int borderWidth)
            {
                Radius = radius;
                BorderColor = borderColor;
                BorderWidth = borderWidth;
            }
        }

    }
}
