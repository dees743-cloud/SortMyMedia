using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public static class OcrHelper
{
    private static readonly HttpClient _httpClient = new()
    {
        BaseAddress = new Uri("http://127.0.0.1:8000"),
        Timeout = TimeSpan.FromSeconds(30)
    };

    public static string LastDeviceUsed { get; set; } = "unknown";

    // ---------------------------------------------------------
    //  SERVER HEALTH
    // ---------------------------------------------------------
    public static async Task<bool> IsServerRunningAsync()
    {
        try
        {
            // The PaddleOCR FastAPI server has no /health endpoint.
            // Any HTTP response (even 404) means the server is up.
            var response = await _httpClient.GetAsync("/");
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ---------------------------------------------------------
    //  SERVER START
    // ---------------------------------------------------------
    private static readonly string ServerPathConfigFile =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ocr_server.path");

    public static void StartOcrServer(string scriptPath)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{scriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Failed to start OCR server: " + ex.Message);
        }
    }

    public static string? ResolveServerScriptPath()
    {
        // 1. Check app directory
        string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "start_ocr_server.bat");
        if (File.Exists(localPath))
            return localPath;

        // 2. Check saved path from previous selection
        if (File.Exists(ServerPathConfigFile))
        {
            string saved = File.ReadAllText(ServerPathConfigFile).Trim();
            if (File.Exists(saved))
                return saved;
        }

        return null;
    }

    public static void SaveServerScriptPath(string path)
    {
        try { File.WriteAllText(ServerPathConfigFile, path); } catch { }
    }

    // ---------------------------------------------------------
    //  MAIN ENTRY
    // ---------------------------------------------------------
    public static async Task<bool> TryRunOcrAsync(string outputRoot, string sourceFilePath, Action<string> log)
    {
        try
        {
            var text = await ExtractTextAsync(sourceFilePath);

            if (!string.IsNullOrWhiteSpace(text))
            {
                SaveOcrText(outputRoot, sourceFilePath, text);
                log($"{Path.GetFileName(sourceFilePath)} → OCR extracted");
                return true;
            }

            log($"OCR produced no text for {Path.GetFileName(sourceFilePath)}.");
            return false;
        }
        catch (Exception ex)
        {
            log($"OCR error for {Path.GetFileName(sourceFilePath)}: {ex.Message}");
            return false;
        }
    }

    // ---------------------------------------------------------
    //  TEXT‑ONLY OCR
    // ---------------------------------------------------------
    private const float MinConfidence = 0.75f;
    private const int MinLineLength = 2;
    private const int MinTotalLength = 3;

    public static async Task<string?> ExtractTextAsync(string sourceFilePath)
    {
        var lines = await RunPaddleOcrAsync(sourceFilePath);
        if (lines == null || lines.Count == 0)
            return null;

        var filtered = lines
            .Where(l => !string.IsNullOrWhiteSpace(l.text))
            .Where(l => l.confidence.HasValue && l.confidence.Value >= MinConfidence)
            .Where(l => l.text.Trim().Length >= MinLineLength)
            .Where(l => ContainsReadableContent(l.text))
            .Select(l => l.text.Trim())
            .ToList();

        if (filtered.Count == 0)
            return null;

        string result = string.Join(" ", filtered).Trim();
        return result.Length >= MinTotalLength ? result : null;
    }

    private static bool ContainsReadableContent(string text)
    {
        // At least one letter or digit must be present — reject pure punctuation/symbols.
        return text.Any(char.IsLetterOrDigit);
    }

    // ---------------------------------------------------------
    //  PADDLE OCR CLIENT
    // ---------------------------------------------------------
    public class OcrResponse
    {
        public List<OcrLine> Results { get; set; }
        public string Device { get; set; }
    }

    public class OcrLine
    {
        public string text { get; set; }
        public float? confidence { get; set; }
        // Bounding box polygon from PaddleOCR: [[x1,y1],[x2,y2],[x3,y3],[x4,y4]]
        public List<List<float>>? box { get; set; }
    }

    private static async Task<List<OcrLine>?> RunPaddleOcrAsync(string imagePath)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            using var fs = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var streamContent = new StreamContent(fs);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(GetMimeType(imagePath));

            string fileName = Path.GetFileName(imagePath);
            form.Add(streamContent, "file", fileName);

            var response = await _httpClient.PostAsync("/ocr", form);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var ocrResponse = JsonSerializer.Deserialize<OcrResponse>(json, options);

            if (ocrResponse == null || ocrResponse.Results == null)
                return null;

            LastDeviceUsed = ocrResponse.Device;

            return ocrResponse.Results;
        }
        catch
        {
            return null;
        }
    }

    private static string GetMimeType(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".tif" or ".tiff" => "image/tiff",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".heic" or ".heif" => "image/heif",
            _ => "application/octet-stream"
        };
    }

    // ---------------------------------------------------------
    //  TXT OUTPUT
    // ---------------------------------------------------------
    public static void SaveOcrText(string outputRoot, string sourceFilePath, string ocrText)
    {
        if (string.IsNullOrWhiteSpace(ocrText))
            return;

        string ocrFolder = Path.Combine(outputRoot, "ocr");
        Directory.CreateDirectory(ocrFolder);

        string baseName = Path.GetFileNameWithoutExtension(sourceFilePath);
        string txtPath = Path.Combine(ocrFolder, baseName + ".txt");

        string content =
$@"Source file: {Path.GetFileName(sourceFilePath)}
OCR extracted on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
----------------------------------------
{ocrText}";

        File.WriteAllText(txtPath, content, Encoding.UTF8);
    }

    // ---------------------------------------------------------
    //  MAIN ENTRY (with optional HTML generation)
    // ---------------------------------------------------------
    public static async Task<bool> TryRunOcrWithHtmlAsync(
        string outputRoot, string sourceFilePath, bool generateHtml, Action<string> log)
    {
        try
        {
            var lines = await RunPaddleOcrAsync(sourceFilePath);
            if (lines == null || lines.Count == 0)
            {
                log($"OCR produced no text for {Path.GetFileName(sourceFilePath)}.");
                return false;
            }

            // Apply the same quality filters used by ExtractTextAsync
            var filtered = lines
                .Where(l => !string.IsNullOrWhiteSpace(l.text))
                .Where(l => l.confidence.HasValue && l.confidence.Value >= MinConfidence)
                .Where(l => l.text.Trim().Length >= MinLineLength)
                .Where(l => ContainsReadableContent(l.text))
                .ToList();

            if (filtered.Count == 0)
            {
                log($"OCR produced no text for {Path.GetFileName(sourceFilePath)}.");
                return false;
            }

            string combinedText = string.Join(" ", filtered.Select(l => l.text.Trim())).Trim();
            if (combinedText.Length < MinTotalLength)
            {
                log($"OCR produced no text for {Path.GetFileName(sourceFilePath)}.");
                return false;
            }

            // Save the .txt file (existing behavior)
            SaveOcrText(outputRoot, sourceFilePath, combinedText);

            // Generate .html file if the feature is enabled
            if (generateHtml)
                GenerateHtmlReport(outputRoot, sourceFilePath, combinedText, filtered);

            log($"{Path.GetFileName(sourceFilePath)} → OCR extracted");
            return true;
        }
        catch (Exception ex)
        {
            log($"OCR error for {Path.GetFileName(sourceFilePath)}: {ex.Message}");
            return false;
        }
    }

    // ---------------------------------------------------------
    //  HTML REPORT GENERATION
    // ---------------------------------------------------------
    private const int HtmlMaxImageWidth = 800;

    private static void GenerateHtmlReport(
        string outputRoot, string sourceFilePath, string combinedText, List<OcrLine> filteredLines)
    {
        string ocrFolder = Path.Combine(outputRoot, "ocr");
        Directory.CreateDirectory(ocrFolder);

        string baseName = Path.GetFileNameWithoutExtension(sourceFilePath);
        string htmlPath = Path.Combine(ocrFolder, baseName + ".html");

        // Load the image using OpenCV — the same decoder PaddleOCR uses.
        // This guarantees that bounding box coordinates from the OCR server
        // are in the exact same pixel coordinate space as our display image.
        // The original file is never modified.
        OpenCvSharp.Mat srcMat;
        try
        {
            byte[] fileBytes = File.ReadAllBytes(sourceFilePath);
            srcMat = OpenCvSharp.Cv2.ImDecode(fileBytes, OpenCvSharp.ImreadModes.Color);
            if (srcMat.Empty())
                return;
        }
        catch
        {
            return;
        }

        using (srcMat)
        {
            int ocrWidth = srcMat.Width;
            int ocrHeight = srcMat.Height;

            // Scale factor: resize so max width = HtmlMaxImageWidth
            float scale = ocrWidth > HtmlMaxImageWidth
                ? (float)HtmlMaxImageWidth / ocrWidth
                : 1.0f;
            int displayWidth = (int)(ocrWidth * scale);
            int displayHeight = (int)(ocrHeight * scale);

            // Resize and encode to JPEG using OpenCV (same pipeline as PaddleOCR)
            string base64DataUrl;
            try
            {
                using var resizedMat = new OpenCvSharp.Mat();
                if (Math.Abs(scale - 1.0f) > 0.001f)
                    OpenCvSharp.Cv2.Resize(srcMat, resizedMat, new OpenCvSharp.Size(displayWidth, displayHeight),
                        interpolation: OpenCvSharp.InterpolationFlags.Area);
                else
                    srcMat.CopyTo(resizedMat);

                OpenCvSharp.Cv2.ImEncode(".jpg", resizedMat, out byte[] jpegBytes);
                base64DataUrl = "data:image/jpeg;base64," + Convert.ToBase64String(jpegBytes);
            }
            catch
            {
                return;
            }

            // Build overlay div elements for each bounding box.
            // Both the OCR server and our display image use OpenCV's ImDecode,
            // so bounding box coordinates map directly — just scale to display size.
            var boxDivs = new StringBuilder();
            foreach (var line in filteredLines)
            {
                if (line.box == null || line.box.Count < 4)
                    continue;

                // box is a polygon: [[x1,y1],[x2,y2],[x3,y3],[x4,y4]]
                // Compute axis-aligned bounding rectangle from the polygon points.
                float minX = line.box.Min(p => p.Count > 0 ? p[0] : 0);
                float minY = line.box.Min(p => p.Count > 1 ? p[1] : 0);
                float maxX = line.box.Max(p => p.Count > 0 ? p[0] : 0);
                float maxY = line.box.Max(p => p.Count > 1 ? p[1] : 0);

                // Scale from OCR image coordinates to display coordinates
                int left = (int)(minX * scale);
                int top = (int)(minY * scale);
                int width = Math.Max(1, (int)((maxX - minX) * scale));
                int height = Math.Max(1, (int)((maxY - minY) * scale));

                string escapedText = System.Net.WebUtility.HtmlEncode(line.text.Trim());
                float conf = line.confidence ?? 0;

                boxDivs.AppendLine(
                    $"      <div class=\"box\" style=\"left:{left}px;top:{top}px;" +
                    $"width:{width}px;height:{height}px;\" " +
                    $"title=\"{escapedText} ({conf:P0})\"></div>");
            }

            string escapedCombinedText = System.Net.WebUtility.HtmlEncode(combinedText);
            string escapedFileName = System.Net.WebUtility.HtmlEncode(Path.GetFileName(sourceFilePath));

            string html =
$@"<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""utf-8"">
  <title>OCR – {escapedFileName}</title>
  <style>
    body {{ font-family: 'Segoe UI', sans-serif; background: #f7f9fc; margin: 20px; color: #222; }}
    h2 {{ margin-bottom: 4px; }}
    .source {{ color: #666; font-size: 0.9em; margin-bottom: 16px; }}
    .image-container {{ position: relative; display: inline-block; border-radius: 6px; overflow: hidden; line-height: 0; }}
    .image-container img {{ display: block; width: {displayWidth}px; height: {displayHeight}px; }}
    .box {{ position: absolute; border: 2px solid rgba(255, 60, 60, 0.7); background: rgba(255, 60, 60, 0.08); pointer-events: auto; cursor: default; box-sizing: border-box; }}
    .box:hover {{ background: rgba(255, 60, 60, 0.22); }}
    .text-output {{ margin-top: 20px; padding: 16px; background: #fff; border: 1px solid #ddd; border-radius: 6px; white-space: pre-wrap; font-size: 0.95em; line-height: 1.6; max-width: {displayWidth}px; }}
  </style>
</head>
<body>
  <h2>OCR Result</h2>
  <p class=""source"">Source: {escapedFileName}</p>
  <div class=""image-container"">
    <img src=""{base64DataUrl}"" alt=""OCR source image"">
{boxDivs}  </div>
  <div class=""text-output"">{escapedCombinedText}</div>
</body>
</html>";

            File.WriteAllText(htmlPath, html, Encoding.UTF8);
        }
    }
}