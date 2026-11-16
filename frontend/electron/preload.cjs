const { contextBridge, ipcRenderer } = require('electron')

contextBridge.exposeInMainWorld('zusyAI', {
  chat: (request) => ipcRenderer.invoke('zusyai:chat', request),
  getLocalModels: () => ipcRenderer.invoke('zusyai:local-models'),
  copyText: (text) => ipcRenderer.invoke('zusyai:copy-text', text),
})
