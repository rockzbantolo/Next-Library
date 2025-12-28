using System.Windows.Forms;

namespace Next.Extentions
{
    public static class Dialog
    {
        public static void ShowInfo(this string message, string title = "Information")
            => MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);

        public static void ShowError(this string message, string title = "Error")
            => MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);

        public static void ShowWarning(this string message, string title = "Warning")
            => MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);

        public static bool AskYesNo(this string message, string title = "Confirm")
            => MessageBox.Show(message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
    }
}
