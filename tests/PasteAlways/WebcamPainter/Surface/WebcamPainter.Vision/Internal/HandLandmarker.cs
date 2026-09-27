using CodeBrix.VideoProcessing.OpenCV5;
using CodeBrix.VideoProcessing.OpenCV5.Dnn;
using System;

namespace WebcamPainter.Vision.Internal;

/// <summary>
/// The 21 hand landmarks inferred for one frame, in ORIGINAL frame pixels, plus the model's
/// confidence that a hand is actually present in the crop it examined.
/// </summary>
internal sealed class LandmarkInference
{
    internal LandmarkInference(Point2f[] imageLandmarks, float presenceScore)
    {
        ImageLandmarks = imageLandmarks;
        PresenceScore = presenceScore;
    }

    /// <summary>The 21 landmarks (MediaPipe hand topology), in frame pixels.</summary>
    internal Point2f[] ImageLandmarks { get; }

    /// <summary>The model's hand-presence confidence, 0..1 (already a probability - do not sigmoid).</summary>
    internal float PresenceScore { get; }
}

/// <summary>
/// Runs MediaPipe's hand-landmark TFLite model through OpenCV DNN: crops the rotated hand
/// ROI that <see cref="PalmDetector"/> produced (via an affine warp into the model's
/// 224x224 input), reads back the 21 landmarks, and projects them into original frame
/// pixel coordinates.
/// </summary>
internal sealed class HandLandmarker : IDisposable
{
    private const int InputSize = 224;

    private readonly Net _net;
    private readonly Mat _crop = new Mat();

    internal HandLandmarker(byte[] modelBytes)
    {
        _net = Cv2.Dnn.ReadNetFromTFLite(modelBytes);
    }

    /// <summary>
    /// Infers the hand landmarks inside the given rotated ROI.
    /// </summary>
    /// <param name="bgrFrame">The frame, in 8-bit BGR.</param>
    /// <param name="roi">The rotated hand region from palm detection.</param>
    /// <returns>The landmarks in frame pixels and the hand-presence confidence.</returns>
    internal LandmarkInference Infer(Mat bgrFrame, PalmDetection roi)
    {
        float cos = (float)Math.Cos(roi.RotationRadians);
        float sin = (float)Math.Sin(roi.RotationRadians);
        float half = roi.RoiSize / 2f;

        Point2f Corner(float offsetX, float offsetY) => new Point2f(
            roi.RoiCenterX + ((offsetX * cos) - (offsetY * sin)),
            roi.RoiCenterY + ((offsetX * sin) + (offsetY * cos)));

        Point2f[] source = { Corner(-half, -half), Corner(half, -half), Corner(-half, half) };
        Point2f[] destination =
        {
            new Point2f(0, 0),
            new Point2f(InputSize, 0),
            new Point2f(0, InputSize),
        };

        using (Mat affine = Cv2.GetAffineTransform(source, destination))
        {
            Cv2.WarpAffine(bgrFrame, _crop, affine, new Size(InputSize, InputSize));
        }

        using Mat blob = Cv2.Dnn.BlobFromImage(_crop, 1.0 / 255,
            new Size(InputSize, InputSize), new Scalar(0, 0, 0), swapRB: true, crop: false);
        _net.SetInput(blob);

        //Identity = 21 x (x, y, z) in crop pixels; Identity_1 = presence probability. Both
        //  outputs are always needed, so read them in one pass with ForwardAll (the second
        //  read reuses the first forward's results).
        float[] rawLandmarks;
        float presence;
        Mat[] outputs = _net.ForwardAll("Identity", "Identity_1");
        try
        {
            rawLandmarks = outputs[0].ToArray<float>();
            presence = outputs[1].ToArray<float>()[0];
        }
        finally
        {
            foreach (Mat output in outputs) { output.Dispose(); }
        }

        //Project crop-space landmarks back into frame pixels through the same rotation
        var imageLandmarks = new Point2f[21];
        for (int i = 0; i < 21; i++)
        {
            float normX = (rawLandmarks[i * 3] / InputSize) - 0.5f;
            float normY = (rawLandmarks[(i * 3) + 1] / InputSize) - 0.5f;
            imageLandmarks[i] = new Point2f(
                roi.RoiCenterX + (((normX * cos) - (normY * sin)) * roi.RoiSize),
                roi.RoiCenterY + (((normX * sin) + (normY * cos)) * roi.RoiSize));
        }

        return new LandmarkInference(imageLandmarks, presence);
    }

    public void Dispose()
    {
        _net?.Dispose();
        _crop.Dispose();
    }
}
