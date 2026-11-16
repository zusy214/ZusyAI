# Architecture

ZusyAI is a Windows desktop chat client built with Vue and Electron. The .NET API routes each chat request to either local Ollama or Ollama Cloud.

## Request flow

```text
Vue chat UI
  -> Local model list: Electron preload/main -> API GET /api/models/local -> Local Ollama GET /api/tags
  -> Chat: Electron preload/main -> API POST /api/chat -> IChatService / OllamaChatService
     -> Local: Ollama POST /api/chat at 127.0.0.1:11434
     -> Cloud: Ollama Cloud POST /api/chat at ollama.com
```

In a regular Vite browser tab, the UI calls the .NET endpoint directly. The API's CORS policy permits the Vite origins `http://localhost:5173` and `http://127.0.0.1:5173`. The packaged Electron app uses a narrow preload bridge and main-process IPC instead of exposing Node.js to the renderer.

## Components

- `frontend/src/components/ChatWindow.vue` manages the persistent navigation rail, toggleable history panel, IndexedDB-backed conversations and local memory, scrolling sent questions into view and positioning new replies at their start, attachment parsing and drag/drop, errors, Markdown rendering, the 12-24px chat text-size control, and a persisted dark/light theme preference.
- `frontend/src/localMemory.js` stores conversations, user notes, and extracted document text in IndexedDB and provides keyword-based document matching without downloading an embedding model.
- `frontend/src/components/ChatComposer.vue` renders the message composer, file picker, pasted-file handling, and attachment previews.
- `frontend/electron/preload.cjs` exposes chat, local model listing, and clipboard methods to the renderer.
- `frontend/electron/main.cjs` forwards chat and local-model requests to the API and provides native clipboard context-menu actions.
- `backend/ZusyAI.Api/Endpoints/ChatEndpoints.cs` validates `POST /api/chat` and calls the chat service.
- `backend/ZusyAI.Api/Endpoints/LocalModelEndpoints.cs` exposes the configured local Ollama model list.
- `backend/ZusyAI.Api/Services/OllamaChatService.cs` forwards the recent conversation to Ollama using `HttpClient`.
- `backend/ZusyAI.Api/appsettings.json` configures local and cloud Ollama URLs and models.

## API contract and limits

`GET /api/models/local` returns installed model names and advertised context lengths from the configured local Ollama `/api/tags` endpoint, along with configured text and vision defaults. `POST /api/chat` accepts messages with `User` or `Assistant` roles, a `provider` (`Local` or `Cloud`), a `think` flag, an optional local text `model`, and optional `localContext` (up to 8,000 characters). A selected local text model is used for text-only Local requests; image requests continue to use `Ollama:LocalVisionModel`. Local model selection is rejected for Cloud requests. The API accepts 1-40 messages, up to 12,000 characters per message. User messages may also include base64 images or extracted document text. Images are limited to 5 per request and 3 MB each; documents are limited to 4 per message, 10 per request, 10,000 characters each, and 100,000 extracted characters total. Attachments are accepted only on user messages. The UI estimates prompt size using text, local context, document text, and a conservative image allowance; it favors the newest complete turns and uses up to 75% of the model's advertised context window (or a 32,768-token fallback), reserving space for generated output. Estimates are approximate because actual tokenization is model-specific. Older messages omitted from a request remain in saved history and the UI reports that they were omitted. The API supplies the selected runtime model identifier as system metadata; direct questions about the active model or runtime host are answered from application metadata rather than relying on model self-report. `localContext` is appended to the system message for Local requests, and requests using the Cloud provider are rejected if `localContext` is non-empty.

Conversation history, notes, and extracted document text persist in local IndexedDB. Notes and up to three keyword-matched document excerpts are included in Local mode only. Cloud requests omit saved memory; directly attached files still follow the normal provider routing and are sent to Cloud when Cloud mode is selected. Memory does not train or modify the model. The Memory panel supports note editing, document removal, and clearing saved conversations independently.

Successful requests return `{ "message": "...", "model": "..." }`; the UI displays the model used next to each reply. Invalid requests return HTTP 400 Problem Details. If Ollama is unavailable or the model is missing, the API returns HTTP 503 Problem Details; unexpected Ollama responses return HTTP 502.

The API uses the Ollama non-streaming endpoint, so the UI displays a waiting indicator until the complete answer is ready. Think mode is sent as Ollama's `think` option and may take longer.

Local text requests use the model selected from the installed-model dropdown (default `Ollama:LocalModel`, `qwen3:1.7b`); image requests use `Ollama:LocalVisionModel` (default `qwen3-vl:2b`) and require Ollama 0.12.7 or later. Cloud text requests use `Ollama:CloudModel` (default `gpt-oss:20b`); image requests use `Ollama:CloudVisionModel` (default `qwen3-vl:235b-cloud`) and require `OLLAMA_API_KEY` to be configured for the .NET API. Cloud prompts, images, and extracted document text leave the PC and are subject to Ollama's usage charges and account/model availability. PDF and DOCX extraction happens in the renderer before sending; scanned PDFs are not OCR-processed.

## Local-only security model

The Electron renderer has context isolation enabled and Node integration disabled. Its preload exposes specific chat and clipboard functions, not arbitrary IPC or filesystem access. The local API is expected to run on loopback for a single-user desktop setup. The API has no authentication; do not expose it or local Ollama to a LAN or the public internet without adding authentication and revisiting CORS and network binding. Keep the cloud API key in the backend environment or local user secrets; never put it in frontend code or source control.
