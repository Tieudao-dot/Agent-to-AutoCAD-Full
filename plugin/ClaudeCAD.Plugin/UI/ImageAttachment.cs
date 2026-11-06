using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ClaudeCAD.Plugin.UI
{
    /// <summary>An image attached to the next message (feature 6): base64 for the API plus a thumbnail.</summary>
    public sealed class ImageAttachment
    {
        /// <summary>Longest side sent to Claude; larger images cost tokens without adding detail.</summary>
        public const int MaxSide = 1568;
        public const int MaxBytes = 3_500_000;

        public string MediaType;
        public string Base64;
        public BitmapSource Thumbnail;
        public int Width, Height;

        public static ImageAttachment FromBitmap(BitmapSource src)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            BitmapSource bmp = src;
            double scale = Math.Min(1.0, (double)MaxSide / Math.Max(src.PixelWidth, src.PixelHeight));
            if (scale < 1.0) bmp = new TransformedBitmap(src, new ScaleTransform(scale, scale));
            if (bmp.Format != PixelFormats.Bgra32 && bmp.Format != PixelFormats.Bgr32 && bmp.Format != PixelFormats.Pbgra32)
                bmp = new FormatConvertedBitmap(bmp, PixelFormats.Bgra32, null, 0);
            bmp.Freeze();

            byte[] data = Encode(new PngBitmapEncoder(), bmp);
            string mt = "image/png";
            if (data.Length > MaxBytes)
            {
                data = Encode(new JpegBitmapEncoder { QualityLevel = 85 }, new FormatConvertedBitmap(bmp, PixelFormats.Bgr24, null, 0));
                mt = "image/jpeg";
            }
            if (data.Length > MaxBytes) throw new InvalidOperationException("Ảnh quá lớn kể cả sau khi nén.");

            double ts = 56.0 / Math.Max(bmp.PixelWidth, bmp.PixelHeight);
            var thumb = new TransformedBitmap(bmp, new ScaleTransform(Math.Min(1, ts), Math.Min(1, ts)));
            thumb.Freeze();
            return new ImageAttachment { MediaType = mt, Base64 = Convert.ToBase64String(data), Thumbnail = thumb, Width = bmp.PixelWidth, Height = bmp.PixelHeight };
        }

        public static ImageAttachment FromFile(string path)
        {
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.UriSource = new Uri(Path.GetFullPath(path));
            bi.EndInit();
            bi.Freeze();
            return FromBitmap(bi);
        }

        private static byte[] Encode(BitmapEncoder enc, BitmapSource bmp)
        {
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using (var ms = new MemoryStream())
            {
                enc.Save(ms);
                return ms.ToArray();
            }
        }

        public static bool IsImageFile(string path)
        {
            var ext = Path.GetExtension(path ?? "").ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif" || ext == ".tif" || ext == ".tiff";
        }
    }
}
