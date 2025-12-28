using QRCoder;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace YourProject.Services
{
    /// <summary>
    /// var qrService = new QrCodeService();
    /// 1. Base64 string (embed in HTML <img src="...">)
    /// string base64Qr = qrService.GenerateBase64("https://example.com");
    /// 
    /// 2. With logo
    /// string base64QrLogo = qrService.GenerateWithLogo("https://example.com", @"C:\logo.png");
    /// 
    /// 3. Byte array (attach to email, save elsewhere)
    /// byte[] qrBytes = qrService.GenerateBytes("https://example.com");
    /// 
    /// 4. Save directly to file
    /// qrService.SaveToFile("https://example.com", @"C:\QRCode.png");
    /// 
    /// Dispose when done
    /// qrService.Dispose();
    /// </summary>

    /// <summary>
    /// Fast QR code generator for .NET Framework 4.8
    /// Supports: PNG, Base64, Bitmap, byte[], and logo embedding
    /// Suitable for tickets, 2FA, payments, WiFi, etc.
    /// QRCoder version: 1.4.1
    /// </summary>
    public sealed class QRCode : IDisposable
    {
        private readonly QRCodeGenerator _generator = new QRCodeGenerator();

        /// <summary>
        /// Generate QR code as Base64 string (HTML &lt;img&gt; src compatible)
        /// </summary>
        public string GenerateBase64(
            string data,
            int pixelsPerModule = 20,
            Color? darkColor = null,
            Color? lightColor = null,
            bool drawQuietZones = true)
        {
            var qrData = _generator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
            using (var qrCode = new QRCoder.QRCode(qrData))
            using (var bitmap = qrCode.GetGraphic(
                pixelsPerModule,
                darkColor ?? Color.Black,
                lightColor ?? Color.White,
                drawQuietZones))
            using (var ms = new MemoryStream())
            {
                bitmap.Save(ms, ImageFormat.Png);
                return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
            }
        }

        /// <summary>
        /// Generate QR code with a center logo
        /// </summary>
        public string GenerateWithLogo(
            string data,
            string logoPath,
            int pixelsPerModule = 20,
            double logoRelativeSize = 0.2,
            int iconBorderWidth = 6,
            bool drawQuietZones = true)
        {
            if (!File.Exists(logoPath))
                throw new FileNotFoundException("Logo not found", logoPath);

            using (var logo = new Bitmap(logoPath))
            {
                var qrData = _generator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
                using (var qrCode = new QRCoder.QRCode(qrData))
                using (var bitmap = qrCode.GetGraphic(
                    pixelsPerModule,
                    Color.Black,
                    Color.White,
                    logo,
                    (int)(logoRelativeSize * 100), // icon size %
                    iconBorderWidth,
                    drawQuietZones))
                using (var ms = new MemoryStream())
                {
                    bitmap.Save(ms, ImageFormat.Png);
                    return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        /// <summary>
        /// Generate QR code as byte array
        /// </summary>
        public byte[] GenerateBytes(string data, int pixelsPerModule = 20, bool drawQuietZones = true)
        {
            var qrData = _generator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
            using (var qrCode = new QRCoder.QRCode(qrData))
            using (var bitmap = qrCode.GetGraphic(pixelsPerModule, Color.Black, Color.White, drawQuietZones))
            using (var ms = new MemoryStream())
            {
                bitmap.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Generate QR code and save directly to PNG file
        /// </summary>
        public void SaveToFile(string data, string filePath, int pixelsPerModule = 20, bool drawQuietZones = true)
        {
            var bytes = GenerateBytes(data, pixelsPerModule, drawQuietZones);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            File.WriteAllBytes(filePath, bytes);
        }

        public void Dispose()
        {
            _generator?.Dispose();
        }

    }
}
