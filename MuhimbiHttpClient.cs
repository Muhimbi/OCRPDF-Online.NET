using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace OCR
{
    /// <summary>
    /// Custom HttpClient-based wrapper for Muhimbi PDF OCR API.
    /// Uses modern .NET HttpClient with configurable TLS settings.
    /// </summary>
    public class MuhimbiHttpClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private bool _disposed;

        /// <summary>
        /// Creates a new MuhimbiHttpClient instance.
        /// </summary>
        /// <param name="apiKey">Your Muhimbi API key</param>
        /// <param name="timeoutMinutes">HTTP timeout in minutes (default: 15)</param>
        /// <param name="skipCertificateValidation">Skip SSL certificate validation (default: false)</param>
        public MuhimbiHttpClient(string apiKey, int timeoutMinutes = 15, bool skipCertificateValidation = false)
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));

            var handler = new HttpClientHandler
            {
                SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            };

            if (skipCertificateValidation)
            {
                handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
            }

            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://api.muhimbi.com/api/"),
                Timeout = TimeSpan.FromMinutes(timeoutMinutes)
            };

            _httpClient.DefaultRequestHeaders.Add("api_key", _apiKey);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// Performs OCR on a PDF file.
        /// </summary>
        /// <param name="filePath">Path to the PDF file</param>
        /// <param name="language">OCR language (default: English)</param>
        /// <param name="performance">Performance setting (default: Slow but accurate)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>OCR result containing the processed PDF bytes</returns>
        public async Task<OcrResult> OcrPdfAsync(
            string filePath,
            string language = "English",
            string performance = "Slow but accurate",
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Source file not found", filePath);

            byte[] fileContent = await File.ReadAllBytesAsync(filePath, cancellationToken);
            string fileName = Path.GetFileName(filePath);

            Console.WriteLine($"[INFO] File: {fileName}");
            Console.WriteLine($"[INFO] File size: {fileContent.Length / 1024.0 / 1024.0:F2} MB");

            return await OcrPdfAsync(fileContent, fileName, language, performance, cancellationToken);
        }

        /// <summary>
        /// Performs OCR on PDF content using synchronous mode.
        /// </summary>
        public async Task<OcrResult> OcrPdfAsync(
            byte[] fileContent,
            string fileName,
            string language = "English",
            string performance = "Slow but accurate",
            CancellationToken cancellationToken = default)
        {
            return await OcrPdfInternalAsync(fileContent, fileName, language, performance, useAsyncPattern: false, cancellationToken);
        }

        /// <summary>
        /// Performs OCR on PDF content using async pattern (recommended for large files).
        /// Submits the job and polls for completion.
        /// </summary>
        /// <param name="fileContent">PDF file bytes</param>
        /// <param name="fileName">File name with extension</param>
        /// <param name="language">OCR language (default: English)</param>
        /// <param name="performance">Performance setting (default: Slow but accurate)</param>
        /// <param name="pollIntervalSeconds">Seconds between status checks (default: 5)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>OCR result containing the processed PDF bytes</returns>
        public async Task<OcrResult> OcrPdfWithPollingAsync(
            byte[] fileContent,
            string fileName,
            string language = "English",
            string performance = "Slow but accurate",
            int pollIntervalSeconds = 5,
            CancellationToken cancellationToken = default)
        {
            return await OcrPdfInternalAsync(fileContent, fileName, language, performance, useAsyncPattern: true, cancellationToken, pollIntervalSeconds);
        }

        /// <summary>
        /// Performs OCR on a PDF file using async pattern (recommended for large files).
        /// </summary>
        public async Task<OcrResult> OcrPdfWithPollingAsync(
            string filePath,
            string language = "English",
            string performance = "Slow but accurate",
            int pollIntervalSeconds = 5,
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Source file not found", filePath);

            byte[] fileContent = await File.ReadAllBytesAsync(filePath, cancellationToken);
            string fileName = Path.GetFileName(filePath);

            Console.WriteLine($"[INFO] File: {fileName}");
            Console.WriteLine($"[INFO] File size: {fileContent.Length / 1024.0 / 1024.0:F2} MB");

            return await OcrPdfWithPollingAsync(fileContent, fileName, language, performance, pollIntervalSeconds, cancellationToken);
        }

        private async Task<OcrResult> OcrPdfInternalAsync(
            byte[] fileContent,
            string fileName,
            string language,
            string performance,
            bool useAsyncPattern,
            CancellationToken cancellationToken,
            int pollIntervalSeconds = 5)
        {
            var request = new OcrPdfRequest
            {
                UseAsyncPattern = useAsyncPattern,
                SourceFileName = fileName,
                SourceFileContent = Convert.ToBase64String(fileContent),
                Language = language,
                Performance = performance,
                CharactersOption = "None",
                Characters = null,
                Paginate = false,
                Regions = null,
                FailOnError = true
            };

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            string jsonContent = JsonSerializer.Serialize(request, jsonOptions);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            Console.WriteLine($"[INFO] Sending OCR request to Muhimbi API (async={useAsyncPattern})...");

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsync("v1/operations/ocr_pdf", httpContent, cancellationToken);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                throw new MuhimbiApiException("Request timed out. Consider increasing the timeout value.", ex);
            }
            catch (HttpRequestException ex)
            {
                var fullMessage = GetFullExceptionMessage(ex);
                throw new MuhimbiApiException($"HTTP request failed: {fullMessage}", ex);
            }

            string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new MuhimbiApiException(
                    $"API returned {(int)response.StatusCode} {response.ReasonPhrase}: {responseContent}");
            }

            // Parse response - same structure for both sync and async
            var result = JsonSerializer.Deserialize<OcrPdfResponse>(responseContent, jsonOptions);

            if (result == null)
                throw new MuhimbiApiException("Failed to parse API response");

            // If using async pattern, result_code will be "Accepted" and task_id is in result_details
            if (useAsyncPattern && result.ResultCode == "Accepted")
            {
                Console.WriteLine($"[DEBUG] Async response: {responseContent}");

                // Extract task ID from result_details (format: "task_id=<uuid>")
                string? taskId = result.ResultDetails?.Replace("task_id=", "");
                if (string.IsNullOrEmpty(taskId))
                    throw new MuhimbiApiException($"Failed to get task ID from async response. Response: {responseContent}");

                Console.WriteLine($"[INFO] Task submitted. Task ID: {taskId}");
                return await PollForResultAsync(taskId, pollIntervalSeconds, jsonOptions, cancellationToken);
            }

            // Synchronous response (or async that completed immediately)
            if (result.ResultCode != "Success")
                throw new MuhimbiApiException($"OCR failed: {result.ResultCode} - {result.ResultDetails}");

            if (string.IsNullOrEmpty(result.ProcessedFileContent))
                throw new MuhimbiApiException("API returned empty file content");

            Console.WriteLine($"[INFO] OCR completed successfully. Result: {result.ResultCode}");

            return new OcrResult
            {
                ProcessedFileContent = Convert.FromBase64String(result.ProcessedFileContent),
                BaseFileName = result.BaseFileName,
                ResultCode = result.ResultCode,
                ResultDetails = result.ResultDetails
            };
        }

        private async Task<OcrResult> PollForResultAsync(
            string taskId,
            int pollIntervalSeconds,
            JsonSerializerOptions jsonOptions,
            CancellationToken cancellationToken)
        {
            int pollCount = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                pollCount++;
                await Task.Delay(TimeSpan.FromSeconds(pollIntervalSeconds), cancellationToken);

                Console.WriteLine($"[INFO] Polling for result (attempt {pollCount})...");

                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.GetAsync($"v1/operations/action_task?task_id={taskId}", cancellationToken);
                }
                catch (HttpRequestException ex)
                {
                    var fullMessage = GetFullExceptionMessage(ex);
                    throw new MuhimbiApiException($"HTTP request failed while polling: {fullMessage}", ex);
                }

                string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    throw new MuhimbiApiException(
                        $"API returned {(int)response.StatusCode} {response.ReasonPhrase}: {responseContent}");
                }

                var result = JsonSerializer.Deserialize<OcrPdfResponse>(responseContent, jsonOptions);

                if (result == null)
                    continue; // Keep polling

                // Check if task is still processing (Accepted = still in progress)
                if (result.ResultCode == "Accepted" || result.ResultCode == "Pending" || result.ResultCode == "Processing")
                {
                    Console.WriteLine($"[INFO] Task status: {result.ResultCode}");
                    continue;
                }

                if (result.ResultCode != "Success")
                    throw new MuhimbiApiException($"OCR failed: {result.ResultCode} - {result.ResultDetails}");

                if (string.IsNullOrEmpty(result.ProcessedFileContent))
                    throw new MuhimbiApiException("API returned empty file content");

                Console.WriteLine($"[INFO] OCR completed successfully. Result: {result.ResultCode}");

                return new OcrResult
                {
                    ProcessedFileContent = Convert.FromBase64String(result.ProcessedFileContent),
                    BaseFileName = result.BaseFileName,
                    ResultCode = result.ResultCode,
                    ResultDetails = result.ResultDetails
                };
            }

            throw new OperationCanceledException("Operation was cancelled while waiting for OCR result");
        }

        /// <summary>
        /// Saves the OCR result to a file.
        /// </summary>
        public async Task SaveResultAsync(OcrResult result, string outputPath, CancellationToken cancellationToken = default)
        {
            await File.WriteAllBytesAsync(outputPath, result.ProcessedFileContent, cancellationToken);
            Console.WriteLine($"[INFO] Result saved to: {outputPath}");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _httpClient?.Dispose();
                _disposed = true;
            }
        }

        private static string GetFullExceptionMessage(Exception ex)
        {
            var sb = new StringBuilder();
            var current = ex;
            int depth = 0;
            while (current != null && depth < 10)
            {
                if (depth > 0) sb.Append(" -> ");
                sb.Append($"[{current.GetType().Name}] {current.Message}");
                current = current.InnerException;
                depth++;
            }
            return sb.ToString();
        }
    }

    #region Request/Response Models

    internal class OcrPdfRequest
    {
        public bool UseAsyncPattern { get; set; }
        public required string SourceFileName { get; set; }
        public required string SourceFileContent { get; set; }
        public required string Language { get; set; }
        public required string Performance { get; set; }
        public required string CharactersOption { get; set; }
        public string? Characters { get; set; }
        public bool Paginate { get; set; }
        public string? Regions { get; set; }
        public bool FailOnError { get; set; }
    }

    internal class OcrPdfResponse
    {
        public string? ProcessedFileContent { get; set; }
        public string? BaseFileName { get; set; }
        public string? ResultCode { get; set; }
        public string? ResultDetails { get; set; }
    }

    #endregion

    #region Public Models

    /// <summary>
    /// Result of an OCR operation.
    /// </summary>
    public class OcrResult
    {
        /// <summary>
        /// The OCR-processed PDF file content.
        /// </summary>
        public required byte[] ProcessedFileContent { get; set; }

        /// <summary>
        /// Base file name without extension.
        /// </summary>
        public string? BaseFileName { get; set; }

        /// <summary>
        /// Result code (e.g., "Success").
        /// </summary>
        public string? ResultCode { get; set; }

        /// <summary>
        /// Additional result details.
        /// </summary>
        public string? ResultDetails { get; set; }
    }

    /// <summary>
    /// Exception thrown when Muhimbi API operations fail.
    /// </summary>
    public class MuhimbiApiException : Exception
    {
        public MuhimbiApiException(string message) : base(message) { }
        public MuhimbiApiException(string message, Exception innerException) : base(message, innerException) { }
    }

    #endregion
}
