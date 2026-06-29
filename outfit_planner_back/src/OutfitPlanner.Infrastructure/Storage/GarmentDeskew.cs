using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace OutfitPlanner.Infrastructure.Storage;

/// <summary>
/// Estimates the rotation (in degrees) needed to straighten a garment cutout from the principal
/// axis of its alpha silhouette. Deliberately conservative: it only corrects when the silhouette
/// has a clear dominant axis (elongated enough) and the required correction is within
/// <paramref name="maxDegrees"/>. Otherwise it returns 0 so a fine photo is never made worse.
/// The returned value is the angle to pass to a clockwise image rotation to bring the dominant
/// axis upright.
/// </summary>
public static class GarmentDeskew
{
    public const double DefaultMaxDegrees = 30d;
    public const double DefaultMinElongation = 1.25d;
    private const byte AlphaThreshold = 16;
    private const int MinForegroundPixels = 64;

    public static double ComputeCorrectionDegrees(
        Image<Rgba32> cutout,
        double maxDegrees = DefaultMaxDegrees,
        double minElongation = DefaultMinElongation)
    {
        double count = 0;
        double sumX = 0;
        double sumY = 0;
        cutout.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    if (row[x].A >= AlphaThreshold)
                    {
                        count++;
                        sumX += x;
                        sumY += y;
                    }
                }
            }
        });

        if (count < MinForegroundPixels)
        {
            return 0d;
        }

        var centroidX = sumX / count;
        var centroidY = sumY / count;

        double m20 = 0;
        double m02 = 0;
        double m11 = 0;
        cutout.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    if (row[x].A >= AlphaThreshold)
                    {
                        var dx = x - centroidX;
                        var dy = y - centroidY;
                        m20 += dx * dx;
                        m02 += dy * dy;
                        m11 += dx * dy;
                    }
                }
            }
        });

        m20 /= count;
        m02 /= count;
        m11 /= count;

        // Covariance eigenvalues give the elongation; skip near-round silhouettes with no clear axis.
        var mean = (m20 + m02) / 2d;
        var spread = Math.Sqrt(Math.Pow((m20 - m02) / 2d, 2) + (m11 * m11));
        var major = mean + spread;
        var minor = mean - spread;
        if (minor <= 1e-6)
        {
            return 0d;
        }

        var elongation = Math.Sqrt(major / minor);
        if (elongation < minElongation)
        {
            return 0d;
        }

        // Angle of the major axis from the x-axis (image y grows downward); vertical == 90 degrees.
        var majorAxisDegrees = 0.5 * Math.Atan2(2d * m11, m20 - m02) * (180d / Math.PI);

        // Rotation that brings the major axis upright, normalized to (-90, 90].
        var correction = 90d - majorAxisDegrees;
        if (correction > 90d)
        {
            correction -= 180d;
        }
        else if (correction <= -90d)
        {
            correction += 180d;
        }

        return Math.Abs(correction) > maxDegrees ? 0d : correction;
    }
}
