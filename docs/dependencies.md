# Dependencies

## System runtimes and services

| Dependency | Used for | Notes |
|---|---|---|
| Node.js and npm | Vite, Vue, Electron development and packaging | PDF.js requires Node.js `>=22.13.0` or `>=24`; `npm ci` installs the locked package versions. |
| .NET 9 SDK | Build and run the ASP.NET Core API from source | A framework-dependent published API requires the ASP.NET Core 9 runtime on the target PC. |
| Ollama for Windows | Local model server | Required only for Local mode; listens at `http://127.0.0.1:11434` by default. |
| `qwen3:1.7b` | Default local language model | Required only for Local mode; download separately with `ollama pull qwen3:1.7b`. |
| Ollama Cloud account and API key | Hosted model service | Required only for Cloud mode; usage is billed by Ollama and prompts leave the PC. Keep the key in backend User Secrets or a protected environment variable. |

Local mode does not require a hosted API key. Cloud mode sends prompts to Ollama Cloud through the local .NET API; the key is not sent to the renderer.

## Frontend packages

Declared in `frontend/package.json`:

| Package | Purpose |
|---|---|
| `vue` | Renderer UI framework |
| `@vitejs/plugin-vue` | Vue support in Vite |
| `vite` | Development server and frontend build |
| `electron` | Windows desktop runtime |
| `electron-builder` | Windows NSIS installer packaging |
| `concurrently` | Runs Vite and Electron together in development |
| `wait-on` | Waits for Vite before starting Electron |
| `pdfjs-dist` | Extracts selectable text from PDF attachments in the renderer |
| `mammoth` | Extracts plain text from DOCX attachments in the renderer |
| `axios`, `pinia`, `vue-router` | Declared runtime dependencies; not currently imported by the chat UI |

The Electron postinstall script is explicitly allowed for the locked Electron version in `package.json`.

Image questions use Ollama vision models in addition to the regular text models: `qwen3-vl:2b` locally (Ollama 0.12.7+) and `qwen3-vl:235b-cloud` through Ollama Cloud. The local vision model is downloaded separately with `ollama pull qwen3-vl:2b`.

## Backend packages

The API targets `net9.0`. Its only explicit NuGet package is `Microsoft.AspNetCore.OpenApi` (`9.0.15`); `HttpClient`, JSON serialization, CORS, and Problem Details use the ASP.NET Core/.NET shared framework.
