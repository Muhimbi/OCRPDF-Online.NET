using Muhimbi.PDF.Online.Client.Api;
using Muhimbi.PDF.Online.Client.Client;
using Muhimbi.PDF.Online.Client.Model;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;

namespace OCR
{
    class OCRPdfSample
    {
        // !!!! ENTER YOUR API KEY HERE !!!!
        static string API_KEY = "438c4cb3-1929-4e24-8c0d-e21064714743";

        static void Main2(string[] args)
        {
            string testFileName = null;

            try
            {
                // ** Make sure an api key has been entered
                if (API_KEY == string.Empty)
                {
                    Console.WriteLine("[ERROR] Please update the sample code and enter the API Key that came with your subscription.");
                    return;
                }

                // ** Was a 'file to OCR' specified on the command line?
                /*if (args.Count() == 0)
                {
                    Console.WriteLine("[INFO] No file to OCR specified, using default file.");
                    testFile = Directory.GetFiles(".", "*.tif")[0];
                }
                else
                    testFile = args[0];*/

                testFileName = "C:\\Converter\\Scan_50 Pages.pdf";

                // ** Specify the API key associated with your subscription.
                Configuration config = new Configuration();
                config.ApiKey.Add("api_key", API_KEY);
                config.BasePath = "https://api.muhimbi.com/api";
                config.Timeout = 600000; // ** 10 minutes timeout for large files


                // ** Accept all SSL Certificates, this makes life under mono a lot easier. This line is not needed on Windows
                ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;


                // ** We are dealing with OCR, so instantiate the relevant class
                OCRApi ocrApi = new OCRApi(config);

                // ** Read the file we wish to OCR
                byte[] sourceFile = File.ReadAllBytes(testFileName);

                // ** Fill out the data for the OCR operation.
                OcrPdfData inputData = new OcrPdfData(
                    sourceFileName: testFileName,                                   // ** The name of the file to OCR. Always include the correct extension
                    sourceFileContent: sourceFile,                              // ** The content of the file to OCR
                    language: OcrPdfData.LanguageEnum.English,                  // ** The document's primary language
                    performance: OcrPdfData.PerformanceEnum.SlowButAccurate,    // ** Unless you have a good reason not to, always use the 'Slow' option.
                    charactersOption: OcrPdfData.CharactersOptionEnum.None,     // ** Any characters to black list or white list (e.g. 1234567890 to deal with numerical data)
                    characters: null,                                           // ** The characters to black or white list.
                    paginate: false,                                            // ** Only 'paginate' when your documents have images that span multiple pages.
                    regions: null                                               // ** We want to OCR the entire document, not just specific areas.
                    );

                // ** Carry out the OCR operation
                Console.WriteLine("[INFO] Running OCR...");
                var response = ocrApi.OcrPdf(inputData);

                // ** Write the results back to the file system
                File.WriteAllBytes("C:\\Converter\\OCRResult2.pdf", response.ProcessedFileContent);

                Console.WriteLine("[INFO] 'OCRResult2.pdf' written to output folder.");

                // ** On Windows open the generated file in the system PDF viewer
                //  Process.Start(@"result.pdf");
            }

            catch (ApiException ex)
            {
                Console.WriteLine($"[ERROR] API Error: {ex.Message}");
                if (ex.InnerException != null)
                    Console.WriteLine($"[ERROR] Inner: {ex.InnerException.Message}");
            }

            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}