#:property TargetFramework=net10.0-windows
#:property UseWindowsForms=true
#:property PublishAot=false

// Generates the multi-resolution app.ico plus PNG previews for visual review.
// Run once: dotnet run --file tools/MakeIcon.cs -- src/GlobalHotKey/app.ico <preview-dir>

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

const int SS = 4;                                   // supersampling factor
int[] sizes = [16, 20, 24, 32, 48, 64, 256];

string icoPath = args.Length > 0 ? args[0] : "app.ico";
string? previewDir = args.Length > 1 ? args[1] : null;

Color Hex(string h) => ColorTranslator.FromHtml(h);
Color BodyTop = Hex("#4F8CFF"), BodyBottom = Hex("#2D5BE3"), Depth = Hex("#1B3FA6"), Bolt = Hex("#FFD84D");

GraphicsPath RoundedRect(RectangleF r, float radius)
{
    float d = radius * 2;
    var p = new GraphicsPath();
    p.AddArc(r.X, r.Y, d, d, 180, 90);
    p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
    p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
    p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
    p.CloseFigure();
    return p;
}

PointF[] BoltPoints(RectangleF box)
{
    (float X, float Y)[] u =
    [
        (0.62f, 0.00f), (0.18f, 0.56f), (0.46f, 0.56f),
        (0.36f, 1.00f), (0.82f, 0.42f), (0.54f, 0.42f)
    ];
    return [.. u.Select(p => new PointF(box.X + p.X * box.Width, box.Y + p.Y * box.Height))];
}

Bitmap Render(int size)
{
    bool bold = size <= 32;                          // small sizes drop every detail that muddies the silhouette
    int s = size * SS;
    var canvas = new Bitmap(s, s, PixelFormat.Format32bppArgb);

    using (var g = Graphics.FromImage(canvas))
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        float margin = s * 0.05f, radius = s * 0.20f;
        var shell = new RectangleF(margin, margin, s - 2 * margin, s - 2 * margin);

        if (!bold)
        {
            var lip = shell;
            lip.Offset(0, s * 0.045f);
            using var lipPath = RoundedRect(lip, radius);
            using var lipBrush = new SolidBrush(Depth);
            g.FillPath(lipBrush, lipPath);
        }

        using (var shellPath = RoundedRect(shell, radius))
        using (var shellBrush = new LinearGradientBrush(shell, BodyBottom, Depth, LinearGradientMode.Vertical))
            g.FillPath(shellBrush, shellPath);

        // Inset top face: the rim between shell and face is what reads as "keycap" and not "app tile".
        float rim = s * (bold ? 0.105f : 0.085f);
        var face = RectangleF.Inflate(shell, -rim, -rim);
        face.Offset(0, -s * 0.012f);                 // thicker skirt at the bottom, like a real keycap
        using (var facePath = RoundedRect(face, radius * 0.7f))
        using (var faceBrush = new LinearGradientBrush(face, BodyTop, BodyBottom, LinearGradientMode.Vertical))
            g.FillPath(faceBrush, facePath);

        if (!bold)
        {
            // Highlight on the face's top inner edge only: two arcs, auto-joined by a line.
            var inner = RectangleF.Inflate(face, -s * 0.022f, -s * 0.022f);
            float d = radius * 1.1f;
            using var hi = new GraphicsPath();
            hi.AddArc(inner.X, inner.Y, d, d, 180, 80);
            hi.AddArc(inner.Right - d, inner.Y, d, d, 280, 80);
            using var pen = new Pen(Color.FromArgb(89, 255, 255, 255), s * 0.014f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawPath(pen, hi);
        }

        float side = face.Height * (bold ? 0.86f : 0.72f);
        var box = new RectangleF(face.X + (face.Width - side) / 2f, face.Y + (face.Height - side) / 2f, side, side);

        if (!bold)
        {
            var shadow = box;
            shadow.Offset(0, s * 0.02f);
            using var shadowBrush = new SolidBrush(Color.FromArgb(77, 0, 0, 0));
            g.FillPolygon(shadowBrush, BoltPoints(shadow));
        }

        var points = BoltPoints(box);
        using (var boltBrush = new SolidBrush(Bolt))
            g.FillPolygon(boltBrush, points);

        if (bold)
        {
            // Thicken the strokes so the bolt survives the downscale to 16px.
            using var pen = new Pen(Bolt, s * 0.04f) { LineJoin = LineJoin.Round };
            g.DrawPolygon(pen, points);
        }
    }

    var scaled = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using (var g = Graphics.FromImage(scaled))
    {
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.Clear(Color.Transparent);
        g.DrawImage(canvas, new Rectangle(0, 0, size, size));
    }
    canvas.Dispose();
    return scaled;
}

byte[] PngPayload(Bitmap b)
{
    var ms = new MemoryStream();
    b.Save(ms, ImageFormat.Png);
    return ms.ToArray();
}

byte[] DibPayload(Bitmap b)
{
    int w = b.Width, h = b.Height;
    int maskStride = (w + 31) / 32 * 4;              // 1bpp AND mask, rows padded to 4 bytes
    var ms = new MemoryStream();
    var bw = new BinaryWriter(ms);

    bw.Write(40);                                    // biSize
    bw.Write(w);                                     // biWidth
    bw.Write(h * 2);                                 // biHeight = XOR + AND
    bw.Write((ushort)1);                             // biPlanes
    bw.Write((ushort)32);                            // biBitCount
    bw.Write(0);                                     // biCompression = BI_RGB
    bw.Write(w * h * 4 + maskStride * h);            // biSizeImage
    bw.Write(0); bw.Write(0);                        // pels per meter
    bw.Write(0); bw.Write(0);                        // clrUsed, clrImportant

    for (int y = h - 1; y >= 0; y--)                 // BGRA, bottom-up
        for (int x = 0; x < w; x++)
        {
            var c = b.GetPixel(x, y);
            bw.Write(c.B); bw.Write(c.G); bw.Write(c.R); bw.Write(c.A);
        }

    bw.Write(new byte[maskStride * h]);              // alpha rules; mask stays zero
    return ms.ToArray();
}

var frames = sizes.Select(s => (Size: s, Bitmap: Render(s))).ToList();
var payloads = frames.Select(f => (f.Size, Data: f.Size >= 256 ? PngPayload(f.Bitmap) : DibPayload(f.Bitmap))).ToList();

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(icoPath))!);
using (var fs = File.Create(icoPath))
using (var w = new BinaryWriter(fs))
{
    w.Write((ushort)0);                              // reserved
    w.Write((ushort)1);                              // type = icon
    w.Write((ushort)payloads.Count);
    int offset = 6 + 16 * payloads.Count;
    foreach (var (size, data) in payloads)
    {
        w.Write((byte)(size >= 256 ? 0 : size));     // 0 means 256
        w.Write((byte)(size >= 256 ? 0 : size));
        w.Write((byte)0);                            // palette colors
        w.Write((byte)0);                            // reserved
        w.Write((ushort)1);                          // planes
        w.Write((ushort)32);                         // bpp
        w.Write((uint)data.Length);
        w.Write((uint)offset);
        offset += data.Length;
    }
    foreach (var (_, data) in payloads) w.Write(data);
}
Console.WriteLine($"{icoPath}: {payloads.Count} frames, {new FileInfo(icoPath).Length} bytes");

if (previewDir is not null)
{
    Directory.CreateDirectory(previewDir);
    foreach (var f in frames) f.Bitmap.Save(Path.Combine(previewDir, $"preview-{f.Size}.png"), ImageFormat.Png);

    int gap = 20, pad = 24;
    int rowW = pad * 2 + sizes.Sum() + gap * (sizes.Length - 1);
    int[] zoomed = [16, 20, 24, 32];
    int zoomW = pad * 2 + zoomed.Sum(z => z * 6) + gap * (zoomed.Length - 1);
    int sheetW = Math.Max(rowW, zoomW);
    int bandH = 256 + pad * 2, zoomH = 32 * 6 + pad * 2;

    using var sheet = new Bitmap(sheetW, bandH * 2 + zoomH, PixelFormat.Format32bppArgb);
    using (var g = Graphics.FromImage(sheet))
    {
        g.Clear(Color.White);
        g.FillRectangle(new SolidBrush(Color.FromArgb(32, 32, 32)), 0, bandH, sheetW, bandH);

        foreach (var band in new[] { 0, 1 })         // same icons on light then dark
        {
            int x = pad;
            foreach (var f in frames)
            {
                g.DrawImage(f.Bitmap, x, band * bandH + pad + (256 - f.Size), f.Size, f.Size);
                x += f.Size + gap;
            }
        }

        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        int zx = pad;
        foreach (var z in zoomed)                    // pixel-level look at the tray sizes
        {
            var src = frames.First(f => f.Size == z).Bitmap;
            g.DrawImage(src, new Rectangle(zx, bandH * 2 + pad, z * 6, z * 6));
            zx += z * 6 + gap;
        }
    }
    sheet.Save(Path.Combine(previewDir, "contact-sheet.png"), ImageFormat.Png);
    Console.WriteLine($"previews -> {previewDir}");
}

foreach (var f in frames) f.Bitmap.Dispose();
