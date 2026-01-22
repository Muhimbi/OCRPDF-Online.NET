using System;
using System.Threading.Tasks;

namespace OCR
{
    class OCRPdfSampleHttpClient
    {
        // !!!! ENTER YOUR API KEY HERE !!!!
        static string API_KEY = "";

        static async Task Main(string[] args)
        {
            try
            {
                // ** Make sure an api key has been entered
                if (string.IsNullOrEmpty(API_KEY))
                {
                    Console.WriteLine("[ERROR] Please update the sample code and enter the API Key that came with your subscription.");
                    return;
                }

                // ** Define input and output files
                string testFileName = @"C:\Converter\Scan_50 Pages.pdf";
                string outputFileName = @"C:\Converter\OCRResult2.pdf";

                // ** Create the HTTP client with 15 minute timeout
                // ** Set skipCertificateValidation to true if you encounter SSL issues
                using var client = new MuhimbiHttpClient(
                    apiKey: API_KEY,
                    timeoutMinutes: 15,
                    skipCertificateValidation: false
                );

                // ** Carry out the OCR operation using async polling (recommended for large files)
                // ** This submits the job and polls for results, avoiding connection timeouts
                Console.WriteLine("[INFO] Running OCR...");
                var result = await client.OcrPdfWithPollingAsync(
                    filePath: testFileName,
                    language: "English",
                    performance: "Slow but accurate",
                    pollIntervalSeconds: 10
                );

                // ** Write the results back to the file system
                await client.SaveResultAsync(result, outputFileName);

                Console.WriteLine("[INFO] 'OCRResult2.pdf' written to output folder.");
            }
            catch (MuhimbiApiException ex)
            {
                Console.WriteLine($"[ERROR] API Error: {ex.Message}");
                PrintFullExceptionChain(ex.InnerException);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {ex.GetType().Name}: {ex.Message}");
                PrintFullExceptionChain(ex.InnerException);
            }
        }

        static void PrintFullExceptionChain(Exception? ex)
        {
            int depth = 1;
            while (ex != null)
            {
                Console.WriteLine($"[ERROR] Inner({depth}): [{ex.GetType().Name}] {ex.Message}");
                ex = ex.InnerException;
                depth++;
            }
        }
    }
}
