using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

internal static class IconBuilder
{
    private static readonly int[] Sizes = { 16, 24, 32, 48, 64, 128, 256 };

    private static void Main(string[] args)
    {
        string output = args.Length > 0 ? args[0] : "libro.ico";
        var images = new List<byte[]>();
        foreach (int size in Sizes) images.Add(Render(size));

        using (var stream = new FileStream(output, FileMode.Create, FileAccess.Write))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)Sizes.Length);

            int offset = 6 + 16 * Sizes.Length;
            for (int i = 0; i < Sizes.Length; i++)
            {
                int size = Sizes[i];
                writer.Write((byte)(size >= 256 ? 0 : size));
                writer.Write((byte)(size >= 256 ? 0 : size));
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write(images[i].Length);
                writer.Write(offset);
                offset += images[i].Length;
            }

            foreach (byte[] image in images) writer.Write(image);
        }
    }

    private static byte[] Render(int size)
    {
        using (var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb))
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.Clear(Color.Transparent);

            float radius = size * 0.22f;
            using (var path = RoundedRect(new RectangleF(0, 0, size, size), radius))
            using (var brush = new SolidBrush(Color.FromArgb(15, 107, 79)))
            {
                graphics.FillPath(brush, path);
            }

            float margin = size * 0.125f;
            float scale = size * 0.03125f;
            float stroke = Math.Max(1f, size * 0.055f);

            Func<float, float, PointF> point = (x, y) =>
                new PointF(margin + x * scale, margin + y * scale);

            using (var pen = new Pen(Color.White, stroke)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            })
            {
                using (var book = new GraphicsPath())
                {
                    book.AddLine(point(5, 4.5f), point(15.5f, 4.5f));
                    PointF corner = point(15.5f, 4.5f);
                    book.AddArc(corner.X, corner.Y, 7 * scale, 7 * scale, 270, 90);
                    book.AddLine(point(19, 8), point(19, 19.5f));
                    book.AddLine(point(19, 19.5f), point(8.5f, 19.5f));
                    PointF bottom = point(5, 12.5f);
                    book.AddArc(bottom.X, bottom.Y, 7 * scale, 7 * scale, 90, 90);
                    book.CloseFigure();
                    graphics.DrawPath(pen, book);
                }

                using (var page = new GraphicsPath())
                {
                    PointF bottom = point(5, 12.5f);
                    page.AddArc(bottom.X, bottom.Y, 7 * scale, 7 * scale, 180, 90);
                    page.AddLine(point(8.5f, 12.5f), point(19, 12.5f));
                    graphics.DrawPath(pen, page);
                }

                graphics.DrawLine(pen, point(9, 8), point(15, 8));
            }

            using (var memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Png);
                return memory.ToArray();
            }
        }
    }

    private static GraphicsPath RoundedRect(RectangleF rect, float radius)
    {
        float diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
