using OpenCvSharp;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace SortMyMedia.FaceRecognition
{
    public class FaceDetectorSCRFD : IDisposable
    {
        private readonly InferenceSession session;
        private readonly string inputName;
        private readonly bool usingCuda;
        private readonly bool enableDetailedLogging;
        private readonly int inputSize;
        private readonly object inferenceLock = new();
        private float[] inputBuffer;

        private const float ScoreThreshold = 0.35f;
        private const float NmsIouThreshold = 0.45f;

        public int InputSize => inputSize;
        public bool UsingCuda => usingCuda;

        public FaceDetectorSCRFD(int inputSize = 640, bool enableDetailedLogging = false)
        {
            if (inputSize != 640 && inputSize != 800 && inputSize != 960 && inputSize != 1280)
                inputSize = 640;

            this.inputSize = inputSize;
            this.enableDetailedLogging = enableDetailedLogging;
            inputBuffer = new float[3 * inputSize * inputSize];

            session = OnnxRuntimeSessionFactory.Create("det_500m_fixed.onnx", "SCRFD", out usingCuda);
            inputName = session.InputMetadata.Keys.First();
            Console.WriteLine($"SCRFD: provider={(usingCuda ? "CUDA" : "CPU")}");
        }

        public DetectionResult[] DetectFaces(Mat image)
        {
            if (image.Empty())
                return Array.Empty<DetectionResult>();

            using Mat input = EnsureBgrInput(image);

            float scale;
            int padX, padY;
            using Mat letterboxed = Letterbox(input, inputSize, out scale, out padX, out padY);

            try
            {
                DenseTensor<float> inputTensor;
                IDisposableReadOnlyCollection<DisposableNamedOnnxValue> ortOutputs;
                var gpuTimer = Stopwatch.StartNew();

                lock (inferenceLock)
                {
                    inputTensor = CreateInputTensor(letterboxed);
                    var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, inputTensor) };
                    ortOutputs = session.Run(inputs);
                }

                gpuTimer.Stop();
                if (enableDetailedLogging)
                    Console.WriteLine($"SCRFD batch=1 size={inputSize} inference={gpuTimer.Elapsed.TotalMilliseconds:F1}ms provider={(usingCuda ? "CUDA" : "CPU")}");

                using (ortOutputs)
                {
                var groupedOutputs = ParseOutputs(ortOutputs.Select(o => o.AsTensor<float>()).ToList());

                if (groupedOutputs.Count == 0)
                    return Array.Empty<DetectionResult>();

                var orderedCounts = groupedOutputs.Keys.OrderByDescending(x => x).ToArray();
                int[] strides = { 8, 16, 32 };
                List<DetectionResult> results = new();

                for (int i = 0; i < Math.Min(strides.Length, orderedCounts.Length); i++)
                {
                    int countKey = orderedCounts[i];
                    var group = groupedOutputs[countKey];
                    DecodeStride(group.scores, group.bbox, group.landmarks, strides[i], input.Width, input.Height, scale, padX, padY, results);
                }

                return ApplyNms(results, NmsIouThreshold).ToArray();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SCRFD inference failed: {ex.Message}");
                return Array.Empty<DetectionResult>();
            }
        }

        private DenseTensor<float> CreateInputTensor(Mat bgr)
        {
            int requiredLength = 3 * inputSize * inputSize;
            if (inputBuffer.Length != requiredLength)
                inputBuffer = new float[requiredLength];

            var tensor = new DenseTensor<float>(inputBuffer, new[] { 1, 3, inputSize, inputSize });

            int planeSize = inputSize * inputSize;

            for (int y = 0; y < bgr.Rows; y++)
            {
                for (int x = 0; x < bgr.Cols; x++)
                {
                    Vec3b pixel = bgr.At<Vec3b>(y, x);
                    int pixelIndex = y * inputSize + x;
                    inputBuffer[pixelIndex] = (pixel.Item2 - 127.5f) / 128f;
                    inputBuffer[planeSize + pixelIndex] = (pixel.Item1 - 127.5f) / 128f;
                    inputBuffer[(planeSize * 2) + pixelIndex] = (pixel.Item0 - 127.5f) / 128f;
                }
            }

            return tensor;
        }

        private static Mat EnsureBgrInput(Mat src)
        {
            if (src.Dims > 2)
            {
                int h = (int)src.Size(0);
                int c = (int)src.Size(src.Dims - 1);

                if ((c == 1 || c == 3 || c == 4) && h > 0)
                {
                    using Mat reshaped = src.Reshape(c, h);
                    return EnsureBgrInput(reshaped);
                }

                throw new InvalidOperationException($"Unsupported input dims for SCRFD: dims={src.Dims}");
            }

            int channels = src.Channels();

            if (channels == 3)
                return src.Clone();

            Mat dst = new Mat();
            if (channels == 1)
            {
                Cv2.CvtColor(src, dst, ColorConversionCodes.GRAY2BGR);
                return dst;
            }

            if (channels == 4)
            {
                Cv2.CvtColor(src, dst, ColorConversionCodes.BGRA2BGR);
                return dst;
            }

            throw new InvalidOperationException($"Unsupported input channels for SCRFD: {channels}");
        }

        private Mat Letterbox(Mat img, int newSize, out float scale, out int padX, out int padY)
        {
            int w = img.Width;
            int h = img.Height;

            scale = Math.Min((float)newSize / w, (float)newSize / h);

            int newW = (int)(w * scale);
            int newH = (int)(h * scale);

            padX = (newSize - newW) / 2;
            padY = (newSize - newH) / 2;

            Mat resized = new Mat();
            Cv2.Resize(img, resized, new OpenCvSharp.Size(newW, newH));

            Mat output = new Mat(newSize, newSize, MatType.CV_8UC3, new Scalar(0, 0, 0));
            resized.CopyTo(output[new Rect(padX, padY, newW, newH)]);

            return output;
        }

        private void DecodeStride(
            float[] scores,
            float[] bbox,
            float[] lms,
            int stride,
            int originalWidth,
            int originalHeight,
            float scale,
            int padX,
            int padY,
            List<DetectionResult> results)
        {
            int anchorCount = scores.Length;
            int featureW = inputSize / stride;
            int featureH = inputSize / stride;
            int locations = featureW * featureH;
            int anchorsPerLocation = Math.Max(1, anchorCount / locations);

            for (int i = 0; i < anchorCount; i++)
            {
                float score = scores[i];
                if (score < ScoreThreshold) continue;

                int locationIndex = i / anchorsPerLocation;
                if (locationIndex >= locations)
                    continue;

                int bboxBase = i * 4;
                int lmkBase = i * 10;
                if (bboxBase + 3 >= bbox.Length || lmkBase + 9 >= lms.Length)
                    continue;

                int gridX = locationIndex % featureW;
                int gridY = locationIndex / featureW;

                float left = bbox[bboxBase + 0] * stride;
                float top = bbox[bboxBase + 1] * stride;
                float right = bbox[bboxBase + 2] * stride;
                float bottom = bbox[bboxBase + 3] * stride;

                float cx = (gridX + 0.5f) * stride;
                float cy = (gridY + 0.5f) * stride;

                float x1 = (cx - left - padX) / scale;
                float y1 = (cy - top - padY) / scale;
                float x2 = (cx + right - padX) / scale;
                float y2 = (cy + bottom - padY) / scale;

                x1 = Math.Max(0, Math.Min(x1, originalWidth - 1));
                y1 = Math.Max(0, Math.Min(y1, originalHeight - 1));
                x2 = Math.Max(0, Math.Min(x2, originalWidth - 1));
                y2 = Math.Max(0, Math.Min(y2, originalHeight - 1));

                Rect rect = new Rect(
                    (int)x1,
                    (int)y1,
                    (int)(x2 - x1),
                    (int)(y2 - y1)
                );

                if (rect.Width <= 0 || rect.Height <= 0)
                    continue;

                Point2f[] kps = new Point2f[5];
                for (int k = 0; k < 5; k++)
                {
                    float lx = (((gridX + 0.5f) * stride) + lms[lmkBase + k * 2 + 0] * stride - padX) / scale;
                    float ly = (((gridY + 0.5f) * stride) + lms[lmkBase + k * 2 + 1] * stride - padY) / scale;
                    kps[k] = new Point2f(lx, ly);
                }

                results.Add(new DetectionResult
                {
                    BBox = rect,
                    Keypoints = kps,
                    Score = score
                });
            }
        }

        private Dictionary<int, (float[] scores, float[] bbox, float[] landmarks)> ParseOutputs(List<Tensor<float>> outputs)
        {
            var scoresByCount = new Dictionary<int, float[]>();
            var bboxByCount = new Dictionary<int, float[]>();
            var lmkByCount = new Dictionary<int, float[]>();

            foreach (Tensor<float> tensor in outputs)
            {
                if (TryExtractByLastDim(tensor, 10, out int lmkCount, out float[] lmkValues))
                {
                    lmkByCount[lmkCount] = lmkValues;
                    continue;
                }

                if (TryExtractByLastDim(tensor, 4, out int bboxCount, out float[] bboxValues))
                {
                    bboxByCount[bboxCount] = bboxValues;
                    continue;
                }

                if (TryExtractScores(tensor, out int scoreCount, out float[] scoreValues))
                {
                    scoresByCount[scoreCount] = scoreValues;
                }
            }

            var grouped = new Dictionary<int, (float[] scores, float[] bbox, float[] landmarks)>();
            foreach (int count in scoresByCount.Keys.Intersect(bboxByCount.Keys).Intersect(lmkByCount.Keys))
                grouped[count] = (scoresByCount[count], bboxByCount[count], lmkByCount[count]);

            return grouped;
        }

        private bool TryExtractScores(Tensor<float> tensor, out int anchorCount, out float[] scores)
        {
            scores = Array.Empty<float>();
            anchorCount = 0;

            var dims = tensor.Dimensions.ToArray();
            if (dims.Length >= 2 && dims[^1] <= 2)
            {
                int classes = dims[^1];
                int total = (int)tensor.Length;
                anchorCount = total / classes;
                float[] data = tensor.ToArray();
                scores = new float[anchorCount];

                for (int i = 0; i < anchorCount; i++)
                    scores[i] = data[i * classes + (classes > 1 ? 1 : 0)];

                return true;
            }

            if (dims.Length == 4)
            {
                int c = dims[1];
                int h = dims[2];
                int w = dims[3];
                if (c == 1 || c == 2)
                {
                    int anchorsPerLocation = c;
                    anchorCount = h * w * anchorsPerLocation;
                    scores = new float[anchorCount];

                    int idx = 0;
                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            for (int a = 0; a < anchorsPerLocation; a++)
                                scores[idx++] = tensor[0, a, y, x];
                        }
                    }
                    return true;
                }
            }

            return false;
        }

        private bool TryExtractByLastDim(Tensor<float> tensor, int featureSize, out int anchorCount, out float[] values)
        {
            values = Array.Empty<float>();
            anchorCount = 0;

            var dims = tensor.Dimensions.ToArray();
            if (dims.Length >= 2 && dims[^1] == featureSize)
            {
                int total = (int)tensor.Length;
                anchorCount = total / featureSize;
                values = tensor.ToArray();
                return true;
            }

            if (dims.Length == 4)
            {
                int c = dims[1];
                int h = dims[2];
                int w = dims[3];
                if (c % featureSize == 0)
                {
                    int anchorsPerLocation = c / featureSize;
                    anchorCount = h * w * anchorsPerLocation;
                    values = new float[anchorCount * featureSize];

                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            int loc = y * w + x;
                            for (int a = 0; a < anchorsPerLocation; a++)
                            {
                                int baseIndex = (loc * anchorsPerLocation + a) * featureSize;
                                for (int f = 0; f < featureSize; f++)
                                {
                                    int channel = a * featureSize + f;
                                    values[baseIndex + f] = tensor[0, channel, y, x];
                                }
                            }
                        }
                    }

                    return true;
                }
            }

            return false;
        }

        private List<DetectionResult> ApplyNms(List<DetectionResult> detections, float iouThreshold)
        {
            List<DetectionResult> ordered = detections
                .OrderByDescending(d => d.Score)
                .ToList();

            List<DetectionResult> finalDetections = new();

            while (ordered.Count > 0)
            {
                DetectionResult best = ordered[0];
                finalDetections.Add(best);

                ordered.RemoveAt(0);

                ordered = ordered
                    .Where(d => IoU(best.BBox, d.BBox) < iouThreshold)
                    .ToList();
            }

            return finalDetections;
        }

        private float IoU(Rect a, Rect b)
        {
            int x1 = Math.Max(a.X, b.X);
            int y1 = Math.Max(a.Y, b.Y);
            int x2 = Math.Min(a.X + a.Width, b.X + b.Width);
            int y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);

            int interArea = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
            int unionArea = a.Width * a.Height + b.Width * b.Height - interArea;

            if (unionArea == 0) return 0;
            return (float)interArea / unionArea;
        }

        public void Dispose()
        {
            session.Dispose();
        }
    }
}