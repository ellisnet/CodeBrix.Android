using CodeBrix.VideoProcessing.OpenCV5;
using CodeBrix.VideoProcessing.OpenCV5.Dnn;
using System;

namespace WebcamPainter.Vision.Internal;

/// <summary>
/// The rotated region of interest that a detected palm implies for the hand-landmark model:
/// a square around the whole hand, in ORIGINAL frame pixels, rotated so the fingers point up.
/// </summary>
internal sealed class PalmDetection
{
    internal PalmDetection(float score, float roiCenterX, float roiCenterY, float roiSize, float rotationRadians)
    {
        Score = score;
        RoiCenterX = roiCenterX;
        RoiCenterY = roiCenterY;
        RoiSize = roiSize;
        RotationRadians = rotationRadians;
    }

    internal float Score { get; }
    internal float RoiCenterX { get; }
    internal float RoiCenterY { get; }
    internal float RoiSize { get; }
    internal float RotationRadians { get; }
}

/// <summary>
/// Runs MediaPipe's palm-detection TFLite model through OpenCV DNN and decodes its raw SSD
/// output into the best palm found in the frame. The model reports offsets against a fixed
/// grid of 2016 anchor points (a 24x24 grid with 2 anchors per cell plus a 12x12 grid with
/// 6 per cell, for its 192x192 input); this class regenerates that grid, picks the
/// highest-scoring anchor, and converts the detection into a rotated hand ROI using
/// MediaPipe's rect-transformation constants (rotate wrist->middle-finger to vertical,
/// expand 2.6x, shift half a box toward the fingers).
/// </summary>
internal sealed class PalmDetector : IDisposable
{
    private const int InputSize = 192;
    private const float ScoreThreshold = 0.5f;
    private const float RoiScale = 2.6f;
    private const float RoiShiftY = -0.5f;

    private static readonly float[] AnchorsX;
    private static readonly float[] AnchorsY;

    private readonly Net _net;
    private readonly Mat _letterboxed = new Mat(InputSize, InputSize, MatType.CV_8UC3, Scalar.All(0));
    private readonly Mat _resized = new Mat();

    static PalmDetector()
    {
        //Anchor grids: stride 8 -> 24x24 cells x 2 anchors; stride 16 -> 12x12 cells x 6
        var anchorsX = new float[2016];
        var anchorsY = new float[2016];
        var index = 0;
        foreach (var (gridSize, perCell) in new[] { (24, 2), (12, 6) })
        {
            for (int y = 0; y < gridSize; y++)
            {
                for (int x = 0; x < gridSize; x++)
                {
                    for (int n = 0; n < perCell; n++)
                    {
                        anchorsX[index] = (x + 0.5f) / gridSize;
                        anchorsY[index] = (y + 0.5f) / gridSize;
                        index++;
                    }
                }
            }
        }
        AnchorsX = anchorsX;
        AnchorsY = anchorsY;
    }

    internal PalmDetector(byte[] modelBytes)
    {
        _net = Cv2.Dnn.ReadNetFromTFLite(modelBytes);
    }

    /// <summary>
    /// Finds the most confident palm in the frame.
    /// </summary>
    /// <param name="bgrFrame">The frame, in 8-bit BGR.</param>
    /// <returns>The best palm's rotated hand ROI; or null when no palm scores above threshold.</returns>
    internal PalmDetection Detect(Mat bgrFrame)
    {
        //Letterbox the frame into the model's square input
        float scale = (float)InputSize / Math.Max(bgrFrame.Width, bgrFrame.Height);
        int scaledW = Math.Max(1, (int)Math.Round(bgrFrame.Width * scale));
        int scaledH = Math.Max(1, (int)Math.Round(bgrFrame.Height * scale));
        int padX = (InputSize - scaledW) / 2;
        int padY = (InputSize - scaledH) / 2;

        _letterboxed.SetTo(Scalar.All(0));
        Cv2.Resize(bgrFrame, _resized, new Size(scaledW, scaledH));
        using (var window = new Mat(_letterboxed, new Rect(padX, padY, scaledW, scaledH)))
        {
            _resized.CopyTo(window);
        }

        using Mat blob = Cv2.Dnn.BlobFromImage(_letterboxed, 1.0 / 255,
            new Size(InputSize, InputSize), new Scalar(0, 0, 0), swapRB: true, crop: false);
        _net.SetInput(blob);

        //Identity_1 = per-anchor score logits; Identity = per-anchor box+keypoint offsets.
        //Read them with separate single-name forwards (not ForwardAll) so the no-hand case
        //  below can early-out before ever reading the far larger box tensor.
        float[] rawScores;
        using (Mat scores = _net.Forward("Identity_1"))
        {
            rawScores = scores.ToArray<float>();
        }

        var bestAnchor = -1;
        var bestScore = 0f;
        for (int a = 0; a < rawScores.Length; a++)
        {
            float score = Sigmoid(rawScores[a]);
            if (score > bestScore)
            {
                bestScore = score;
                bestAnchor = a;
            }
        }
        if (bestAnchor < 0 || bestScore < ScoreThreshold) { return null; }

        float[] regressors;
        using (Mat boxes = _net.Forward("Identity"))
        {
            regressors = boxes.ToArray<float>();
        }

        //Decode the winning anchor. All values are normalized to the square input space.
        int b = bestAnchor * 18;
        float cx = (regressors[b] / InputSize) + AnchorsX[bestAnchor];
        float cy = (regressors[b + 1] / InputSize) + AnchorsY[bestAnchor];
        float w = regressors[b + 2] / InputSize;
        float h = regressors[b + 3] / InputSize;

        //Keypoint 0 = wrist center, keypoint 2 = middle-finger MCP: their vector gives the
        //  hand's rotation (target: fingers pointing straight up)
        float kp0X = (regressors[b + 4] / InputSize) + AnchorsX[bestAnchor];
        float kp0Y = (regressors[b + 5] / InputSize) + AnchorsY[bestAnchor];
        float kp2X = (regressors[b + 8] / InputSize) + AnchorsX[bestAnchor];
        float kp2Y = (regressors[b + 9] / InputSize) + AnchorsY[bestAnchor];
        float rotation = NormalizeRadians(
            (float)((Math.PI / 2) - Math.Atan2(-(kp2Y - kp0Y), kp2X - kp0X)));

        //MediaPipe rect transformation: shift half a box toward the fingers (along the
        //  rotated vertical axis), then expand to 2.6x the palm box
        float boxSize = Math.Max(w, h);
        float shift = RoiShiftY * boxSize;
        float roiCx = cx - ((float)Math.Sin(rotation) * shift);
        float roiCy = cy + ((float)Math.Cos(rotation) * shift);
        float roiSize = boxSize * RoiScale;

        //Convert from the letterboxed square space back to original frame pixels
        float imageCx = ((roiCx * InputSize) - padX) / scale;
        float imageCy = ((roiCy * InputSize) - padY) / scale;
        float imageSize = roiSize * InputSize / scale;

        return new PalmDetection(bestScore, imageCx, imageCy, imageSize, rotation);
    }

    internal static float Sigmoid(float x) => 1f / (1f + (float)Math.Exp(-Math.Clamp(x, -50f, 50f)));

    internal static float NormalizeRadians(float angle) =>
        angle - (float)(2 * Math.PI * Math.Floor((angle + Math.PI) / (2 * Math.PI)));

    /// <summary>Exposed for unit tests: the anchor grid's X centers.</summary>
    internal static float[] TestAnchorsX => AnchorsX;

    /// <summary>Exposed for unit tests: the anchor grid's Y centers.</summary>
    internal static float[] TestAnchorsY => AnchorsY;

    public void Dispose()
    {
        _net?.Dispose();
        _letterboxed.Dispose();
        _resized.Dispose();
    }
}
