using AutoCaptureOCR.Core.Interfaces;
using AutoCaptureOCR.Core.Models;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace AutoCaptureOCR.Core.OCR;

/// <summary>
/// Windows.Media.Ocr implementation for text recognition
/// </summary>
public class WindowsOCREngine : IOCREngine
{
    public string Name => "Windows OCR";

    public async Task<OCRResult> ProcessAsync(Bitmap image)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            // Convert Bitmap to SoftwareBitmap for WinRT OCR
            var softwareBitmap = await ConvertToSoftwareBitmapAsync(image);

            // Initialize OCR engine
            var language = new Language("en-US");
            var ocrEngine = OcrEngine.TryCreateFromLanguage(language);

            if (ocrEngine == null)
            {
                return new OCRResult
                {
                    Text = string.Empty,
                    Confidence = 0,
                    EngineName = Name,
                    ProcessingTime = DateTime.UtcNow - startTime
                };
            }

            // Perform OCR
            var ocrResult = await ocrEngine.RecognizeAsync(softwareBitmap);

            // Build result
            var result = new OCRResult
            {
                Text = ocrResult.Text,
                EngineName = Name,
                ProcessingTime = DateTime.UtcNow - startTime
            };

            // Extract lines with bounding boxes and confidence
            int lineNumber = 0;
            double totalConfidence = 0;
            int wordCount = 0;

            foreach (var line in ocrResult.Lines)
            {
                var ocrLine = new OCRLine
                {
                    Text = line.Text,
                    LineNumber = lineNumber++,
                    BoundingBox = new BoundingBox
                    {
                        X = line.Words.Count > 0 ? (int)line.Words[0].BoundingRect.X : 0,
                        Y = line.Words.Count > 0 ? (int)line.Words[0].BoundingRect.Y : 0,
                        Width = (int)line.Words.Sum(w => w.BoundingRect.Width),
                        Height = line.Words.Count > 0 ? (int)line.Words.Max(w => w.BoundingRect.Height) : 0
                    }
                };

                // Calculate line confidence from words and extract individual words
                double lineConfidence = 0;
                foreach (var word in line.Words)
                {
                    lineConfidence += 1.0; // Windows OCR doesn't provide confidence, assume 1.0
                    wordCount++;

                    // Add individual word to the line
                    ocrLine.Words.Add(new OCRWord
                    {
                        Text = word.Text,
                        BoundingBox = new BoundingBox
                        {
                            X = (int)word.BoundingRect.X,
                            Y = (int)word.BoundingRect.Y,
                            Width = (int)word.BoundingRect.Width,
                            Height = (int)word.BoundingRect.Height
                        }
                    });
                }

                ocrLine.Confidence = line.Words.Count > 0 ? lineConfidence / line.Words.Count : 0;
                totalConfidence += lineConfidence;

                result.Lines.Add(ocrLine);
            }

            // Calculate overall confidence
            result.Confidence = wordCount > 0 ? totalConfidence / wordCount : 0;

            return result;
        }
        catch (Exception ex)
        {
            return new OCRResult
            {
                Text = $"OCR Error: {ex.Message}",
                Confidence = 0,
                EngineName = Name,
                ProcessingTime = DateTime.UtcNow - startTime
            };
        }
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var language = new Language("en-US");
            var engine = OcrEngine.TryCreateFromLanguage(language);
            return await Task.FromResult(engine != null);
        }
        catch
        {
            return false;
        }
    }

    private async Task<SoftwareBitmap> ConvertToSoftwareBitmapAsync(Bitmap bitmap)
    {
        using var memoryStream = new MemoryStream();
        bitmap.Save(memoryStream, ImageFormat.Png);
        memoryStream.Position = 0;

        var decoder = await BitmapDecoder.CreateAsync(memoryStream.AsRandomAccessStream());
        var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied
        );

        return softwareBitmap;
    }
}
