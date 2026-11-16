# Local setup

## Prerequisites

- Windows 10/11.
- Node.js 22.13+ and npm for the Vue/Electron app.
- .NET 9 SDK for the API.
- Ollama for Windows and enough disk space for the model if using the local provider.

## Install Ollama and its model

Install Ollama from [ollama.com/download](https://ollama.com/download) or:

```powershell
winget install --id Ollama.Ollama -e
```

Open a new terminal and download the default model:

```powershell
ollama pull qwen3:1.7b
ollama list
```

The Ollama desktop app normally starts the local server automatically. If it is not running, start it with `ollama serve` in a separate terminal. Do not start a second server if the Ollama tray app is already serving requests.

For image questions in Local mode, Ollama 0.12.7 or later is required. Download the separate vision model when needed:

```powershell
ollama pull qwen3-vl:2b
```

## Start the application

Keep the API and desktop app running in separate terminals. Ollama normally runs in the background.

Terminal 1 - API:

```powershell
cd C:\projects\ZusyAI\backend\ZusyAI.Api
dotnet run --launch-profile http
```

The API listens at `http://localhost:5030`. Its development OpenAPI document is `http://localhost:5030/openapi/v1.json`.

Terminal 2 - frontend and Electron:

```powershell
cd C:\projects\ZusyAI\frontend
npm ci
npm run desktop:dev
```

The Electron window connects to the API at `http://127.0.0.1:5030`. Keep both terminals open while chatting.

## Configuration

The settings are in `backend/ZusyAI.Api/appsettings.json`:

```json
{
  "Ollama": {
    "LocalBaseUrl": "http://127.0.0.1:11434/api",
    "LocalModel": "qwen3:1.7b",
    "LocalVisionModel": "qwen3-vl:2b",
    "CloudBaseUrl": "https://ollama.com/api",
    "CloudModel": "gpt-oss:20b",
    "CloudVisionModel": "qwen3-vl:235b-cloud"
  }
}
```

The Local/Cloud switch and local text-model dropdown are in the top-right Ollama menu. The dropdown lists models currently installed on the configured local Ollama service; use Refresh after pulling a model. The selected text model is saved on this PC. Image requests continue to use the configured local vision model. Cloud mode sends prompts to Ollama Cloud and requires an Ollama Cloud API key. Configure it for the backend, never in the frontend.

```powershell
dotnet user-secrets init --project .\backend\ZusyAI.Api
dotnet user-secrets set "OLLAMA_API_KEY" "<your Ollama Cloud API key>" --project .\backend\ZusyAI.Api
```

User Secrets are for local development and are not committed to the repository. For a deployed API, set the `OLLAMA_API_KEY` environment variable in the API process's protected environment. Cloud usage is billed by Ollama; prompts are transmitted to Ollama Cloud and leave the PC. Check the current Ollama Cloud pricing and data-handling terms before using it with sensitive information.

Override provider settings using ASP.NET Core configuration environment variables, then restart the API:

```powershell
$env:Ollama__LocalModel = "qwen3:1.7b"
$env:Ollama__LocalVisionModel = "qwen3-vl:2b"
$env:Ollama__CloudModel = "gpt-oss:20b"
$env:Ollama__CloudVisionModel = "qwen3-vl:235b-cloud"
```

Install additional local models with `ollama pull <model-name>`, then refresh the dropdown. `Ollama:LocalModel` remains the default selection when it is installed; otherwise, the app selects an installed model. Cloud models are hosted by Ollama and are not pulled onto the PC.

## Attachments

Attach files with the plus button, paste an image from the clipboard, or drag files anywhere into the chat window. Supported formats are PNG, JPEG, WebP, TXT, Markdown, CSV, JSON, PDF, and DOCX. PDF and DOCX text is extracted on the PC; scanned PDFs and images embedded in Word documents are not OCR-processed.

Images are limited to 3 MB each and use the configured vision model (Local: `qwen3-vl:2b`; Cloud: `qwen3-vl:235b-cloud`). Local image questions require the vision model to be pulled first. Documents are limited to 10 MB per file and 10,000 extracted characters each; PDFs are limited to 100 pages. The API limits each request to 5 images and 10 documents, with at most 100,000 extracted document characters.

In Local mode, prompt content, images, and extracted document text go only to the local Ollama service. In Cloud mode, prompt content, images, and extracted text are sent to Ollama Cloud; review its current pricing and data-handling terms before attaching sensitive files.

Use the sun/moon control in the top bar to switch between dark and light mode. The selection is saved on this PC.

## Local history and memory

Chat history, saved notes, and extracted text from attached documents are stored in the app's local IndexedDB database on this PC. Use the Memory button in the chat toolbar to add, edit, or delete notes; remove indexed documents; or clear saved conversations. Clearing chat history does not remove notes or indexed documents.

The app does not train or change the Ollama model. In Local mode, saved notes and up to three keyword-matched document excerpts may be included as context with a chat request. The app estimates prompt size and prioritizes the newest complete turns, reserving space for a reply; this estimate is approximate because tokenization varies by model. If older messages are omitted to fit the context window, they stay in saved history and the chat shows a notice. In Cloud mode, saved memory is not included. Files attached directly to a Cloud chat are still sent to Ollama Cloud, just as described in Attachments.

The local library is limited to 50 notes (up to 1,000 characters each) and 100 documents (up to 10,000 extracted characters each). Only extracted document text is indexed; the original file is not copied into the library. Conversation history may contain attached images and can therefore use more storage. Actual browser storage limits vary by system. Removing chats, notes, or indexed documents from the Memory panel frees the corresponding local records.

## Troubleshooting

- **"Local Ollama unavailable"**: make sure the Ollama tray app/server is running and listening on port 11434.
- **"Ollama model not found"**: run `ollama pull qwen3:1.7b`, or pull the model configured in `appsettings.json`.
- **Image requests fail in Local mode**: update Ollama to 0.12.7 or later and run `ollama pull qwen3-vl:2b`.
- **A PDF has no usable text**: scanned PDFs need OCR, which this version does not provide.
- **"Ollama Cloud API key missing"**: configure `OLLAMA_API_KEY` for the .NET API process and restart it.
- **"Ollama Cloud authentication failed"**: confirm the key is valid and the account has access to the configured cloud model.
- **Cannot reach the local API**: start the API with the `http` launch profile and check that port 5030 is available.
- **Electron fails to install**: confirm the npm install script for Electron is allowed, then check `npx electron --version` before starting the desktop script.
