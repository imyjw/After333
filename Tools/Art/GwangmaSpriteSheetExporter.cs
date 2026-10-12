using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

public static class GwangmaSpriteSheetExporter
{
    // Image coordinates start at the top left. These are the original runtime crops.
    private static readonly string[] Names = { "Idle", "Attack", "BeAttacked", "Run", "Death" };
    private static readonly int[] RowTop = { 82, 311, 543, 758, 960 };

    public static void Export(string sourcePath, string outputDirectory, bool overwrite)
    {
        Directory.CreateDirectory(outputDirectory);
        foreach (string name in Names)
            if (!overwrite && File.Exists(Path.Combine(outputDirectory, name + ".png")))
                throw new IOException("File already exists; protect manual edits: " + name + ".png");
        using (Bitmap loaded = new Bitmap(sourcePath))
        using (Bitmap source = loaded.Clone(new Rectangle(0, 0, loaded.Width, loaded.Height), PixelFormat.Format32bppArgb))
        {
            if (source.Width != 1374 || source.Height != 1145)
                throw new InvalidDataException("Expected the unchanged 1374x1145 Gwangma source atlas.");
            BitmapData sourceData = source.LockBits(new Rectangle(0, 0, source.Width, source.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            byte[] pixels;
            int sourceStride = sourceData.Stride;
            try
            {
                if (sourceStride <= 0) throw new InvalidDataException("Unsupported negative image stride.");
                pixels = new byte[sourceStride * source.Height];
                Marshal.Copy(sourceData.Scan0, pixels, 0, pixels.Length);
            }
            finally { source.UnlockBits(sourceData); }
            for (int row = 0; row < Names.Length; row++)
            using (Bitmap output = new Bitmap(3072, 512, PixelFormat.Format32bppArgb))
            {
                BitmapData data = output.LockBits(new Rectangle(0, 0, output.Width, output.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try
                {
                    byte[] target = new byte[data.Stride * output.Height];
                    for (int column = 0; column < 6; column++)
                    for (int y = 0; y < 512; y++)
                    for (int x = 0; x < 512; x++)
                    {
                        // Nearest-neighbour pixel-center sampling, no smoothing or added detail.
                        int sx = column * 229 + 45 + (int)((x + 0.5) * 144 / 512);
                        int sy = RowTop[row] + (int)((y + 0.5) * 144 / 512);
                        int from = sy * sourceStride + sx * 4;
                        int to = y * data.Stride + (column * 512 + x) * 4;
                        Buffer.BlockCopy(pixels, from, target, to, 4);
                    }
                    Marshal.Copy(target, 0, data.Scan0, target.Length);
                }
                finally { output.UnlockBits(data); }
                output.Save(Path.Combine(outputDirectory, Names[row] + ".png"), ImageFormat.Png);
            }
        }
    }
}