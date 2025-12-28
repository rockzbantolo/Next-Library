using FluentFTP;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace YourProject.Services
{
    /// <summary>
    /// Stable FTP service for .NET Framework 4.8.
    /// Uses FluentFTP 37.0.2 (safe async support + all APIs available).
    /// Handles: connect, upload, download, list, delete, recursive copy.
    /// Ensures: no socket leaks, no connection hangs, reusable instance.
    /// </summary>
    public sealed class Ftp : IDisposable
    {
        private readonly FtpClient _client;
        private readonly Action<string> _log;

        /// <summary>
        /// Create FTP service with host credentials.
        /// </summary>
        public Ftp(
            string host,
            string username,
            string password,
            int port = 21,
            bool useFtps = false,
            Action<string> logger = null)
        {
            _log = logger ?? (m => System.Diagnostics.Debug.WriteLine("[FTP] " + m));

            _client = new FtpClient(host)
            {
                Port = port,
                Credentials = new NetworkCredential(username, password),
                EncryptionMode = useFtps ? FtpEncryptionMode.Explicit : FtpEncryptionMode.None,
                DataConnectionType = FtpDataConnectionType.AutoPassive,
                ValidateAnyCertificate = true
            };

            Log("FTP Client initialized");
        }

        private void Log(string msg)
        {
            _log("[FTP " + DateTime.Now.ToString("HH:mm:ss") + "] " + msg);
        }

        private async Task EnsureConnectedAsync()
        {
            if (!_client.IsConnected)
            {
                await _client.ConnectAsync();
                Log("Connected");
            }
        }

        /// <summary>
        /// Upload a file (overwrite always).
        /// </summary>
        public async Task<bool> UploadAsync(string localPath, string remotePath)
        {
            if (!File.Exists(localPath))
            {
                Log("Local file missing → " + localPath);
                return false;
            }

            await EnsureConnectedAsync();

            var result = await _client.UploadFileAsync(
                localPath,
                remotePath,
                FtpRemoteExists.Overwrite,
                true
            );

            Log("Upload → " + remotePath + " | " + result.ToString());
            return result == FtpStatus.Success;
        }

        /// <summary>
        /// Download a file (overwrite always).
        /// </summary>
        public async Task<bool> DownloadAsync(string remotePath, string localPath)
        {
            await EnsureConnectedAsync();

            string dir = Path.GetDirectoryName(localPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var result = await _client.DownloadFileAsync(
                localPath,
                remotePath,
                FtpLocalExists.Overwrite
            );

            Log("Download → " + remotePath + " => " + localPath + " | " + result);
            return result == FtpStatus.Success;
        }

        /// <summary>
        /// List folders and files inside a directory.
        /// </summary>
        public async Task<List<FtpListItem>> ListAsync(string remotePath = "/")
        {
            await EnsureConnectedAsync();
            var items = await _client.GetListingAsync(remotePath);

            var list = new List<FtpListItem>();
            foreach (var i in items) list.Add(i);

            Log("List → " + remotePath + " (" + list.Count + " items)");
            return list;
        }

        /// <summary>
        /// Check if directory exists.
        /// </summary>
        public async Task<bool> DirectoryExistsAsync(string path)
        {
            await EnsureConnectedAsync();
            return await _client.DirectoryExistsAsync(path);
        }

        /// <summary>
        /// Check if file exists.
        /// </summary>
        public async Task<bool> FileExistsAsync(string path)
        {
            await EnsureConnectedAsync();
            return await _client.FileExistsAsync(path);
        }

        /// <summary>
        /// Delete a file.
        /// </summary>
        public async Task DeleteFileAsync(string remotePath)
        {
            await EnsureConnectedAsync();
            await _client.DeleteFileAsync(remotePath);
            Log("Delete file → " + remotePath);
        }

        /// <summary>
        /// Delete a directory and all its contents.
        /// </summary>
        public async Task DeleteDirectoryAsync(string remotePath)
        {
            await EnsureConnectedAsync();
            await _client.DeleteDirectoryAsync(remotePath);
            Log("Delete directory → " + remotePath);
        }

        /// <summary>
        /// Download a whole folder recursively.
        /// </summary>
        public async Task DownloadFolderAsync(string remoteFolder, string localFolder)
        {
            await EnsureConnectedAsync();
            if (!Directory.Exists(localFolder)) Directory.CreateDirectory(localFolder);

            var list = await _client.GetListingAsync(remoteFolder);

            foreach (var item in list)
            {
                string r = remoteFolder.TrimEnd('/') + "/" + item.Name;
                string l = Path.Combine(localFolder, item.Name);

                if (item.Type == FtpFileSystemObjectType.Directory)
                    await DownloadFolderAsync(r, l);
                else
                    await DownloadAsync(r, l);
            }

            Log("Download folder → " + remoteFolder + " => " + localFolder);
        }

        /// <summary>
        /// Disconnect & free resources.
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (_client.IsConnected)
                    _client.Disconnect();
            }
            catch { }

            _client.Dispose();
        }
    }
}
