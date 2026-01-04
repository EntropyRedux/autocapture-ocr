using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using AutoCaptureOCR.Core.Interfaces;
using AutoCaptureOCR.Core.Models;
using AutoCaptureOCR.Core.Utils;
using Newtonsoft.Json;

namespace AutoCaptureOCR.Core.Export;

/// <summary>
/// Exports project/session data to JSON format
/// </summary>
public class JsonExporter : IExporter
{
    public string FileExtension => ".json";

    public async Task<ExportResult> ExportProjectAsync(Project project, string outputPath, ExportOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ExportResult();

        try
        {
            var exportData = BuildProjectExportData(project, options, result.Warnings);

            await WriteJsonAsync(exportData, outputPath, options.CompressOutput);

            var fileInfo = new FileInfo(outputPath);
            result.Success = true;
            result.FilePath = outputPath;
            result.CapturesExported = project.Sessions.Sum(s => s.Captures.Count);
            result.FileSizeBytes = fileInfo.Length;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
        }

        return result;
    }

    public async Task<ExportResult> ExportSessionAsync(CaptureSession session, string outputPath, ExportOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ExportResult();

        try
        {
            var exportData = BuildSessionExportData(session, options, result.Warnings);

            await WriteJsonAsync(exportData, outputPath, options.CompressOutput);

            var fileInfo = new FileInfo(outputPath);
            result.Success = true;
            result.FilePath = outputPath;
            result.CapturesExported = session.Captures.Count;
            result.FileSizeBytes = fileInfo.Length;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
        }

        return result;
    }

    private object BuildProjectExportData(Project project, ExportOptions options, List<string> warnings)
    {
        return new
        {
            ExportInfo = new
            {
                ExportedAt = DateTime.UtcNow,
                Format = "JSON",
                Version = "1.0",
                Options = options
            },
            Project = new
            {
                project.Id,
                project.Name,
                project.Description,
                project.Created,
                project.SavePath,
                SessionCount = project.Sessions.Count,
                TotalCaptures = project.Sessions.Sum(s => s.Captures.Count)
            },
            Sessions = project.Sessions.Select(s => BuildSessionData(s, options, warnings)).ToList()
        };
    }

    private object BuildSessionExportData(CaptureSession session, ExportOptions options, List<string> warnings)
    {
        return new
        {
            ExportInfo = new
            {
                ExportedAt = DateTime.UtcNow,
                Format = "JSON",
                Version = "1.0",
                Options = options
            },
            Session = BuildSessionData(session, options, warnings)
        };
    }

    private object BuildSessionData(CaptureSession session, ExportOptions options, List<string> warnings)
    {
        return new
        {
            session.Id,
            session.Name,
            session.Created,
            CaptureCount = session.Captures.Count,
            Captures = session.Captures.Select(c => BuildCaptureData(c, options, warnings)).ToList()
        };
    }

    private object BuildCaptureData(ScreenCapture capture, ExportOptions options, List<string> warnings)
    {
        var data = new Dictionary<string, object>
        {
            ["Id"] = capture.Id,
            ["SequenceNumber"] = capture.SequenceNumber,
            ["FileName"] = capture.FileName,
            ["FilePath"] = capture.FilePath,
            ["Timestamp"] = capture.Timestamp,
            ["Status"] = capture.Status.ToString()
        };

        // Check for incomplete captures
        if (capture.Status != CaptureStatus.Completed && !warnings.Contains("Some captures are incomplete"))
        {
            warnings.Add("Some captures are incomplete");
        }

        // OCR Results
        if (options.IncludeOCRResults && capture.OCRResult != null)
        {
            var ocrData = new Dictionary<string, object>
            {
                ["Text"] = capture.OCRResult.Text,
                ["Confidence"] = capture.OCRResult.Confidence,
                ["EngineName"] = capture.OCRResult.EngineName
            };

            if (options.IncludeBoundingBoxes && capture.OCRResult.Lines != null)
            {
                // Perform layout analysis
                var layoutAnalysis = LayoutAnalyzer.AnalyzeLayout(capture.OCRResult);

                ocrData["Lines"] = capture.OCRResult.Lines.Select((line, index) => new
                {
                    line.Text,
                    line.Confidence,
                    line.LineNumber,
                    BoundingBox = new
                    {
                        line.BoundingBox.X,
                        line.BoundingBox.Y,
                        line.BoundingBox.Width,
                        line.BoundingBox.Height
                    },
                    Words = line.Words.Select(word => new
                    {
                        word.Text,
                        BoundingBox = new
                        {
                            word.BoundingBox.X,
                            word.BoundingBox.Y,
                            word.BoundingBox.Width,
                            word.BoundingBox.Height
                        }
                    }).ToList(),
                    // Add spatial relationship data
                    SpatialRelationship = index < layoutAnalysis.Lines.Count ? new
                    {
                        layoutAnalysis.Lines[index].RelativePosition,
                        layoutAnalysis.Lines[index].VerticalGap
                    } : null
                }).ToList();

                // Add overall layout info
                ocrData["Layout"] = new
                {
                    CanvasBounds = new
                    {
                        layoutAnalysis.CanvasBounds.X,
                        layoutAnalysis.CanvasBounds.Y,
                        layoutAnalysis.CanvasBounds.Width,
                        layoutAnalysis.CanvasBounds.Height
                    },
                    TotalLines = capture.OCRResult.Lines.Count,
                    TotalWords = capture.OCRResult.Lines.Sum(l => l.Words.Count)
                };
            }
            else if (capture.OCRResult.Lines != null)
            {
                ocrData["Lines"] = capture.OCRResult.Lines.Select(line => new
                {
                    line.Text,
                    line.Confidence,
                    line.LineNumber
                }).ToList();
            }

            data["OCRResult"] = ocrData;
        }

        // Metadata
        if (options.IncludeMetadata)
        {
            if (capture.Metadata.Count > 0)
            {
                data["Metadata"] = capture.Metadata;
            }

            if (capture.TemplateMetadata != null)
            {
                data["TemplateMetadata"] = new
                {
                    capture.TemplateMetadata.TemplateId,
                    capture.TemplateMetadata.TemplateName,
                    capture.TemplateMetadata.Values,
                    capture.TemplateMetadata.AppliedAt
                };
            }
        }

        // Thumbnails (base64 encoded)
        if (options.IncludeThumbnails && !string.IsNullOrEmpty(capture.ThumbnailPath) && File.Exists(capture.ThumbnailPath))
        {
            try
            {
                var thumbnailBytes = File.ReadAllBytes(capture.ThumbnailPath);
                data["ThumbnailBase64"] = Convert.ToBase64String(thumbnailBytes);
            }
            catch
            {
                // Silently skip if thumbnail can't be read
            }
        }

        return data;
    }

    private async Task WriteJsonAsync(object data, string outputPath, bool compress)
    {
        var json = JsonConvert.SerializeObject(data, Formatting.Indented);

        if (compress)
        {
            // Write compressed JSON
            var compressedPath = Path.ChangeExtension(outputPath, ".json.gz");
            using var fileStream = File.Create(compressedPath);
            using var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal);
            using var writer = new StreamWriter(gzipStream);
            await writer.WriteAsync(json);
        }
        else
        {
            // Write plain JSON
            await File.WriteAllTextAsync(outputPath, json);
        }
    }
}
