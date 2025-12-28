using System.IO;

namespace Next.Controls
{
    internal class FileExtentions
    {
        public static void OpenFolderAndSelectFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                string arg = $"/select,\"{filePath}\"";
                System.Diagnostics.Process.Start("explorer.exe", arg);
            }
        }

        public static string GetUniqueFileName(string fullPath)
        {
            if (!File.Exists(fullPath)) return fullPath;
            string folder = Path.GetDirectoryName(fullPath);
            string fileName = Path.GetFileNameWithoutExtension(fullPath);
            string extension = Path.GetExtension(fullPath);
            int i = 1;
            while (File.Exists(Path.Combine(folder, $"{fileName} ({i}){extension}")))
                i++;
            return Path.Combine(folder, $"{fileName} ({i}){extension}");
        }
    }
}
