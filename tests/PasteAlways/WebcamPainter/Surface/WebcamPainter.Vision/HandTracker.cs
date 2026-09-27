using CodeBrix.VideoProcessing.OpenCV5;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using WebcamPainter.Vision.Internal;

namespace WebcamPainter.Vision;

/// <summary>
/// The hand-tracking pipeline: feed it webcam frames (BGRA, any size) with
/// <see cref="SubmitFrame"/> and it raises <see cref="TrackingUpdated"/> with the palm's
/// position and open-palm state. Inference runs on the tracker's own worker thread with
/// latest-frame-wins semantics - submitting faster than the models can process simply
/// drops stale frames, so the capture pipeline is never blocked.
/// </summary>
public sealed class HandTracker : IDisposable
{
    private const string DetectorResourceName = "WebcamPainter.Vision.Models.hand_detector.tflite";
    private const string LandmarkerResourceName = "WebcamPainter.Vision.Models.hand_landmarks_detector.tflite";

    /// <summary>The minimum landmark-model presence confidence for a hand to count as present.</summary>
    public const float PresenceThreshold = 0.5f;

    /// <summary>
    /// The exponential-moving-average factor for the palm position (1 = no smoothing,
    /// smaller = smoother but laggier brush).
    /// </summary>
    public const float SmoothingAlpha = 0.5f;

    private readonly object _pendingLock = new object();
    private byte[] _pendingFrame;
    private int _pendingWidth;
    private int _pendingHeight;
    private bool _hasPending;

    private byte[] _workingFrame;
    private Mat _bgraMat;
    private Mat _bgrMat;

    private Thread _worker;
    private AutoResetEvent _frameSignal;
    private volatile bool _running;

    private bool _hasSmoothed;
    private float _smoothedX;
    private float _smoothedY;

    /// <summary>Indicates whether the tracker's worker is running.</summary>
    public bool IsRunning => _running;

    /// <summary>
    /// Raised after each processed frame - including hand-lost frames, so subscribers can
    /// end an in-progress paint stroke. Raised on the tracker's WORKER thread: handlers
    /// must marshal any UI work themselves.
    /// </summary>
    public event EventHandler<HandTrackingEventArgs> TrackingUpdated;

    /// <summary>
    /// Starts the tracker: loads the embedded models and spins up the inference worker.
    /// Safe to call when already started.
    /// </summary>
    public void Start()
    {
        if (_running) { return; }

        _frameSignal = new AutoResetEvent(false);
        _running = true;
        _worker = new Thread(WorkerLoop)
        {
            Name = "WebcamPainter.HandTracker",
            IsBackground = true,
        };
        _worker.Start();
    }

    /// <summary>Stops the inference worker. Safe to call when already stopped.</summary>
    public void Stop()
    {
        if (!_running) { return; }

        _running = false;
        _frameSignal.Set();
        _worker.Join();
        _worker = null;

        _frameSignal.Dispose();
        _frameSignal = null;

        lock (_pendingLock) { _hasPending = false; }
        _hasSmoothed = false;
    }

    /// <summary>
    /// Offers a frame to the tracker. The pixels are copied before returning, so the caller
    /// may reuse its buffer immediately. When the worker is still busy with an earlier
    /// frame, the previous pending frame is silently replaced (latest wins).
    /// </summary>
    /// <param name="bgraPixels">The frame's tightly packed 32-bit BGRA pixels.</param>
    /// <param name="width">The frame's width in pixels.</param>
    /// <param name="height">The frame's height in pixels.</param>
    public void SubmitFrame(byte[] bgraPixels, int width, int height)
    {
        if (!_running || bgraPixels == null || width < 1 || height < 1) { return; }

        int needed = width * height * 4;
        if (bgraPixels.Length < needed) { return; }

        lock (_pendingLock)
        {
            if (_pendingFrame == null || _pendingFrame.Length != needed)
            {
                _pendingFrame = new byte[needed];
            }
            Array.Copy(bgraPixels, _pendingFrame, needed);
            _pendingWidth = width;
            _pendingHeight = height;
            _hasPending = true;
        }
        _frameSignal.Set();
    }

    private void WorkerLoop()
    {
        PalmDetector detector = null;
        HandLandmarker landmarker = null;
        try
        {
            detector = new PalmDetector(LoadEmbeddedModel(DetectorResourceName));
            landmarker = new HandLandmarker(LoadEmbeddedModel(LandmarkerResourceName));

            while (_running)
            {
                _frameSignal.WaitOne();
                if (!_running) { break; }

                int width;
                int height;
                lock (_pendingLock)
                {
                    if (!_hasPending) { continue; }

                    //Swap the pending buffer out under the lock; copy-free hand-off
                    (_workingFrame, _pendingFrame) = (_pendingFrame, _workingFrame);
                    width = _pendingWidth;
                    height = _pendingHeight;
                    _hasPending = false;
                }

                try
                {
                    HandTrackingResult result = ProcessFrame(detector, landmarker, _workingFrame, width, height);
                    TrackingUpdated?.Invoke(this, new HandTrackingEventArgs(result));
                }
                catch (Exception ex) when (!_running)
                {
                    //Shutting down: a frame was in flight when the tracker - or the native
                    //  OpenCV runtime at process exit - began tearing down (e.g. "terminated
                    //  TLS container"). The app is going away; exit the loop quietly rather
                    //  than surfacing this as a fatal unhandled exception on the worker thread.
                    Debug.WriteLine($"HandTracker worker stopping during shutdown: {ex.Message}");
                    break;
                }
                catch (Exception ex)
                {
                    //A single frame failed to process - drop it and keep tracking rather than
                    //  taking down the whole application over one bad frame.
                    Debug.WriteLine($"HandTracker skipped a frame: {ex.Message}");
                }
            }
        }
        finally
        {
            detector?.Dispose();
            landmarker?.Dispose();
            _bgraMat?.Dispose();
            _bgraMat = null;
            _bgrMat?.Dispose();
            _bgrMat = null;
        }
    }

    private HandTrackingResult ProcessFrame(PalmDetector detector, HandLandmarker landmarker,
        byte[] bgraPixels, int width, int height)
    {
        if (_bgraMat == null || _bgraMat.Width != width || _bgraMat.Height != height)
        {
            _bgraMat?.Dispose();
            _bgraMat = new Mat(height, width, MatType.CV_8UC4);
            _bgrMat?.Dispose();
            _bgrMat = new Mat();
        }
        Marshal.Copy(bgraPixels, 0, _bgraMat.Data, width * height * 4);
        Cv2.CvtColor(_bgraMat, _bgrMat, ColorConversionCodes.BGRA2BGR);

        PalmDetection palm = detector.Detect(_bgrMat);
        if (palm == null)
        {
            _hasSmoothed = false;
            return HandTrackingResult.NoHand;
        }

        LandmarkInference inference = landmarker.Infer(_bgrMat, palm);
        if (inference.PresenceScore < PresenceThreshold)
        {
            _hasSmoothed = false;
            return HandTrackingResult.NoHand;
        }

        Point2f palmCenter = OpenPalmClassifier.GetPalmCenter(inference.ImageLandmarks);
        float normX = Math.Clamp(palmCenter.X / width, 0f, 1f);
        float normY = Math.Clamp(palmCenter.Y / height, 0f, 1f);

        if (_hasSmoothed)
        {
            _smoothedX += (normX - _smoothedX) * SmoothingAlpha;
            _smoothedY += (normY - _smoothedY) * SmoothingAlpha;
        }
        else
        {
            _smoothedX = normX;
            _smoothedY = normY;
            _hasSmoothed = true;
        }

        bool isOpenPalm = OpenPalmClassifier.IsOpenPalm(inference.ImageLandmarks);
        return new HandTrackingResult(true, isOpenPalm,
            _smoothedX, _smoothedY, palm.Score, inference.PresenceScore);
    }

    internal static byte[] LoadEmbeddedModel(string resourceName)
    {
        using Stream stream = typeof(HandTracker).Assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new InvalidOperationException($"Embedded model not found: {resourceName}");
        }
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        TrackingUpdated = null;
        Stop();
    }
}
