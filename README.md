# Muhimbi OCR Sample (C# / .NET 9)

A console application demonstrating how to run OCR on PDF files using the Muhimbi PDF Online API. Targets .NET 9 (C# 13).

## Features
- **Custom HttpClient wrapper** (`MuhimbiHttpClient.cs`) with modern TLS 1.2/1.3 support
- **Async polling pattern** for large files - avoids connection timeouts by submitting jobs and polling for results
- Configurable timeout and SSL certificate validation options

## Requirements
- .NET 9 SDK
- Visual Studio 2022 (or any editor that supports .NET 9)
- Download prepackaged dependencies from https://github.com/Muhimbi/PDF-Converter-Services-Online/raw/master/clients/v1/dotnetcore/muhimbi-pdf-online-client-dotnetcore.zip
- Extract the contents and add a reference to the DLLs in your project
- A valid Muhimbi API key (subscription)

## Files
- `MuhimbiHttpClient.cs` - Custom HttpClient-based wrapper with async polling support (recommended for large files)
- `OCRPdfSampleHttpClient.cs` - Sample using the custom HttpClient wrapper
- `OCRPdfSample.cs` - Original sample using the Muhimbi SDK (works for small files)

## Setup

1. Obtain a Muhimbi API key from your Muhimbi subscription portal.
2. Do NOT commit your API key to source control.
3. Edit `OCRPdfSampleHttpClient.cs` and set the `API_KEY` constant.

## Usage

The recommended approach for large files is to use `MuhimbiHttpClient` with async polling:

```csharp
using var client = new MuhimbiHttpClient(
    apiKey: "your-api-key",
    timeoutMinutes: 15,
    skipCertificateValidation: false
);

var result = await client.OcrPdfWithPollingAsync(
    filePath: "input.pdf",
    language: "English",
    performance: "Slow but accurate",
    pollIntervalSeconds: 10
);

await client.SaveResultAsync(result, "output.pdf");
```

This avoids connection timeout issues that occur with the synchronous API when processing large PDF files.