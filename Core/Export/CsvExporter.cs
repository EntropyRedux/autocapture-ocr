using System.Diagnostics;
using System.IO;
using System.Text;
using AutoCaptureOCR.Core.Interfaces;
using AutoCaptureOCR.Core.Models;

namespace AutoCaptureOCR.Core.Export;

/// <summary>
/// Exports project/session data to CSV format
/// </summary>
public class CsvExporter : IExporter
{
    public string FileExtension => ".csv";

    public async Task<ExportResult> ExportProjectAsync(Project project, string outputPath, ExportOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ExportResult();

        try
        {
            var csv = new StringBuilder();

            // Collect all captures first
            var allCaptures = project.Sessions
                .SelectMany(s => s.Captures.Select(c => (projectName: project.Name, sessionName: s.Name, capture: c)))
                .ToList();

            // Determine dynamic template field columns
            var templateFields = GetUniqueTemplateFields(allCaptures.Select(x => x.capture).ToList());

            // Write header
            WriteHeader(csv, options, templateFields);

            // Write all captures
            foreach (var (projectName, sessionName, capture) in allCaptures)
            {
                WriteCaptureLine(csv, projectName, sessionName, capture, options, templateFields, result.Warnings);
            }

            await File.WriteAllTextAsync(outputPath, csv.ToString());

            var fileInfo = new FileInfo(outputPath);
            result.Success = true;
            result.FilePath = outputPath;
            result.CapturesExported = allCaptures.Count;
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
            var csv = new StringBuilder();

            // Determine dynamic template field columns
            var templateFields = GetUniqueTemplateFields(session.Captures.ToList());

            // Write header
            WriteHeader(csv, options, templateFields);

            // Write all captures
            foreach (var capture in session.Captures)
            {
                WriteCaptureLine(csv, "", session.Name, capture, options, templateFields, result.Warnings);
            }

            await File.WriteAllTextAsync(outputPath, csv.ToString());

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

    private List<string> GetUniqueTemplateFields(List<ScreenCapture> captures)
    {
        // Collect all unique field names from template metadata across all captures
        var fieldNames = new HashSet<string>();

        foreach (var capture in captures)
        {
            if (capture.TemplateMetadata?.Values != null)
            {
                foreach (var key in capture.TemplateMetadata.Values.Keys)
                {
                    fieldNames.Add(key);
                }
            }
        }

        return fieldNames.OrderBy(f => f).ToList();
    }

    private void WriteHeader(StringBuilder csv, ExportOptions options, List<string> templateFields)
    {
        var headers = new List<string>
        {
            "Project",
            "Session",
            "Sequence",
            "FileName",
            "FilePath",
            "Timestamp",
            "Status"
        };

        if (options.IncludeOCRResults)
        {
            headers.AddRange(new[]
            {
                "OCR_Text",
                "OCR_Confidence",
                "OCR_EngineName",
                "OCR_WordCount",
                "OCR_LineCount"
            });
        }

        if (options.IncludeMetadata)
        {
            headers.Add("Template_Name");

            // Add dynamic columns for each unique template field
            if (templateFields.Count > 0)
            {
                headers.AddRange(templateFields.Select(f => $"Field_{f}"));
            }
        }

        csv.AppendLine(string.Join(",", headers.Select(EscapeCsvValue)));
    }

    private void WriteCaptureLine(StringBuilder csv, string projectName, string sessionName,
        ScreenCapture capture, ExportOptions options, List<string> templateFields, List<string> warnings)
    {
        var values = new List<string>
        {
            projectName,
            sessionName,
            capture.SequenceNumber.ToString(),
            capture.FileName,
            capture.FilePath,
            capture.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
            capture.Status.ToString()
        };

        // Check for incomplete captures
        if (capture.Status != CaptureStatus.Completed && !warnings.Contains("Some captures are incomplete"))
        {
            warnings.Add("Some captures are incomplete");
        }

        if (options.IncludeOCRResults)
        {
            if (capture.OCRResult != null)
            {
                var wordCount = capture.OCRResult.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                var lineCount = capture.OCRResult.Lines?.Count ?? 0;

                // Format OCR text based on selected format
                var formattedText = FormatOcrText(capture.OCRResult, options.OcrTextFormat);

                values.AddRange(new[]
                {
                    formattedText,
                    capture.OCRResult.Confidence.ToString("F4"),
                    capture.OCRResult.EngineName ?? "",
                    wordCount.ToString(),
                    lineCount.ToString()
                });
            }
            else
            {
                values.AddRange(new[] { "", "", "", "0", "0" });
            }
        }

        if (options.IncludeMetadata)
        {
            values.Add(capture.TemplateMetadata?.TemplateName ?? "");

            // Add values for each template field column
            foreach (var fieldName in templateFields)
            {
                var fieldValue = "";
                if (capture.TemplateMetadata?.Values != null &&
                    capture.TemplateMetadata.Values.TryGetValue(fieldName, out var value))
                {
                    fieldValue = value;
                }
                else if (capture.Metadata.TryGetValue(fieldName, out var legacyValue))
                {
                    fieldValue = legacyValue;
                }
                values.Add(fieldValue);
            }
        }

        csv.AppendLine(string.Join(",", values.Select(EscapeCsvValue)));
    }

    private string FormatOcrText(OCRResult ocrResult, OcrTextFormat format)
    {
        return format switch
        {
            OcrTextFormat.Continuous => ocrResult.Text.Replace("\n", " ").Replace("\r", ""),

            OcrTextFormat.Lines => ocrResult.Lines != null && ocrResult.Lines.Count > 0
                ? string.Join("\n", ocrResult.Lines.Select(l => l.Text))
                : ocrResult.Text,

            OcrTextFormat.Structured => ocrResult.Lines != null && ocrResult.Lines.Count > 0
                ? string.Join("\n", ocrResult.Lines.Select(l => $"[Line {l.LineNumber}] {l.Text}"))
                : ocrResult.Text,

            OcrTextFormat.Json => ocrResult.Lines != null && ocrResult.Lines.Count > 0
                ? Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    Text = ocrResult.Text,
                    Confidence = ocrResult.Confidence,
                    Lines = ocrResult.Lines.Select(l => new
                    {
                        l.LineNumber,
                        l.Text,
                        l.Confidence,
                        WordCount = l.Words.Count
                    })
                })
                : Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    Text = ocrResult.Text,
                    Confidence = ocrResult.Confidence
                }),

            _ => ocrResult.Text.Replace("\n", " ").Replace("\r", "")
        };
    }

    private string EscapeCsvValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "\"\"";

        // Escape quotes and wrap in quotes if contains comma, quote, or newline
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
