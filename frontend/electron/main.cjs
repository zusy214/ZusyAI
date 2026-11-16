const { app, BrowserWindow, clipboard, Menu, ipcMain } = require('electron')
const path = require('node:path')

app.setName('ZusyAI')
Menu.setApplicationMenu(null)

ipcMain.handle('zusyai:chat', async (_event, request) => {
  if (
    !request ||
    !Array.isArray(request.messages) ||
    (request.provider !== 'Local' && request.provider !== 'Cloud')
  ) {
    throw new Error('The chat request is invalid.')
  }

  let response
  try {
    response = await fetch('http://127.0.0.1:5030/api/chat', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        messages: request.messages,
        provider: request.provider,
        think: request.think === true,
        model: request.model,
        localContext: request.localContext,
      }),
    })
  } catch {
    throw new Error('Cannot reach the local API. Start the .NET API and try again.')
  }

  let result
  try {
    result = await response.json()
  } catch {
    throw new Error(`The local API returned an unreadable response (${response.status}).`)
  }

  if (!response.ok) {
    throw new Error(result.detail || result.title || `The local API returned ${response.status}.`)
  }

  return result
})

ipcMain.handle('zusyai:local-models', async () => {
  let response
  try {
    response = await fetch('http://127.0.0.1:5030/api/models/local')
  } catch {
    throw new Error('Cannot reach the local API. Start the .NET API and try again.')
  }

  let result
  try {
    result = await response.json()
  } catch {
    throw new Error(`The local API returned an unreadable response (${response.status}).`)
  }

  if (!response.ok) {
    throw new Error(result.detail || result.title || `The local API returned ${response.status}.`)
  }

  return result
})

ipcMain.handle('zusyai:copy-text', (_event, text) => {
  if (typeof text !== 'string') {
    throw new Error('Clipboard text must be a string.')
  }
  clipboard.writeText(text)
})

function createWindow() {
  const window = new BrowserWindow({
    title: 'ZusyAI',
    width: 1200,
    height: 800,
    minWidth: 800,
    minHeight: 600,
    icon: path.join(__dirname, 'assets', 'zusyai.ico'),
    webPreferences: {
      preload: path.join(__dirname, 'preload.cjs'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
    },
  })

  window.webContents.on('context-menu', (_event, params) => {
    const template = params.isEditable
      ? [
          { role: 'cut' },
          { role: 'copy' },
          { role: 'paste' },
          { type: 'separator' },
          { role: 'selectAll' },
        ]
      : [
            ...(typeof params.selectionText === 'string' && params.selectionText.trim()
              ? [{ role: 'copy' }]
              : []),
          { role: 'selectAll' },
        ]

    Menu.buildFromTemplate(template).popup({ window })
  })

  if (app.isPackaged) {
    window.loadFile(path.join(__dirname, '..', 'dist', 'index.html'))
  } else {
    window.loadURL('http://127.0.0.1:5173')
  }
}

app.whenReady().then(() => {
  createWindow()

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) createWindow()
  })
})

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit()
})
