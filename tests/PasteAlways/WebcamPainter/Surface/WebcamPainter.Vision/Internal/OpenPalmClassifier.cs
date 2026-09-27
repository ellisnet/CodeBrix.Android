using CodeBrix.VideoProcessing.OpenCV5;
using System;

namespace WebcamPainter.Vision.Internal;

/// <summary>
/// Decides geometrically whether 21 hand landmarks show an open palm - the painting
/// ("frosting spatula") gesture. The bundled MediaPipe gesture-classifier models cannot run
/// through OpenCV's TFLite importer, but they are not needed: an open palm is simply a hand
/// whose four fingers are extended, and extension falls straight out of the landmark
/// geometry (each fingertip is farther from the wrist than that finger's middle joint).
/// A curled finger folds back toward the wrist, so its tip/joint ratio drops below 1.
/// </summary>
internal static class OpenPalmClassifier
{
    /// <summary>
    /// How much farther from the wrist a fingertip must be than its PIP joint (as a ratio)
    /// to count as extended. Raise toward 1.3 to demand flatter hands; lower toward 1.0 to
    /// accept slightly cupped hands.
    /// </summary>
    internal const float ExtendedRatio = 1.1f;

    //MediaPipe hand-landmark topology: 0 = wrist; each finger runs MCP -> PIP -> DIP -> TIP
    //  (index 5-8, middle 9-12, ring 13-16, pinky 17-20; thumb 1-4)
    private static readonly (int Tip, int Pip)[] Fingers = { (8, 6), (12, 10), (16, 14), (20, 18) };

    /// <summary>
    /// Classifies the landmarks as open palm (all four fingers extended) or not.
    /// </summary>
    /// <param name="landmarks">The 21 hand landmarks, in any consistent coordinate space.</param>
    /// <returns><c>true</c> for an open palm.</returns>
    internal static bool IsOpenPalm(Point2f[] landmarks)
    {
        if (landmarks == null || landmarks.Length < 21) { return false; }

        Point2f wrist = landmarks[0];
        foreach ((int tip, int pip) in Fingers)
        {
            if (Distance(landmarks[tip], wrist) <= Distance(landmarks[pip], wrist) * ExtendedRatio)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// The palm's center: the mean of the wrist and the four finger MCP knuckles.
    /// </summary>
    /// <param name="landmarks">The 21 hand landmarks.</param>
    /// <returns>The palm center, in the landmarks' coordinate space.</returns>
    internal static Point2f GetPalmCenter(Point2f[] landmarks)
    {
        var sumX = 0f;
        var sumY = 0f;
        foreach (int i in new[] { 0, 5, 9, 13, 17 })
        {
            sumX += landmarks[i].X;
            sumY += landmarks[i].Y;
        }
        return new Point2f(sumX / 5f, sumY / 5f);
    }

    private static float Distance(Point2f a, Point2f b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return (float)Math.Sqrt((dx * dx) + (dy * dy));
    }
}
