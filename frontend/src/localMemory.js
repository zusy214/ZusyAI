const databaseName = 'zusyai-local-data'
const databaseVersion = 1
const storeNames = {
  conversations: 'conversations',
  memories: 'memories',
  documents: 'documents',
}

let databasePromise

function openDatabase() {
  if (!('indexedDB' in window)) {
    return Promise.reject(new Error('This environment does not support local IndexedDB storage.'))
  }
  if (databasePromise) return databasePromise

  const request = window.indexedDB.open(databaseName, databaseVersion)
  databasePromise = new Promise((resolve, reject) => {
    request.onupgradeneeded = () => {
      const database = request.result
      for (const storeName of Object.values(storeNames)) {
        if (!database.objectStoreNames.contains(storeName)) {
          database.createObjectStore(storeName, { keyPath: 'id' })
        }
      }
    }
    request.onsuccess = () => {
      request.result.onversionchange = () => {
        request.result.close()
        databasePromise = null
      }
      resolve(request.result)
    }
    request.onerror = () => reject(request.error ?? new Error('Could not open local storage.'))
    request.onblocked = () => reject(new Error('Local storage is blocked by another open ZusyAI window.'))
  }).catch((error) => {
    databasePromise = null
    throw error
  })

  return databasePromise
}

function transact(storeName, mode, operation) {
  return openDatabase().then((database) => new Promise((resolve, reject) => {
    const transaction = database.transaction(storeName, mode)
    let result
    let request
    const fail = () => reject(
      transaction.error ?? request?.error ?? new Error('The local storage operation failed.'),
    )

    transaction.oncomplete = () => resolve(result)
    transaction.onerror = fail
    transaction.onabort = fail

    try {
      request = operation(transaction.objectStore(storeName))
      request.onsuccess = () => {
        result = request.result
      }
      request.onerror = fail
    } catch (error) {
      transaction.abort()
      reject(error)
    }
  }))
}

export function listConversations() {
  return transact(storeNames.conversations, 'readonly', (store) => store.getAll())
    .then((conversations) => conversations.sort((left, right) => (
      right.updatedAt.localeCompare(left.updatedAt)
    )))
}

export function saveConversation(conversation) {
  return transact(storeNames.conversations, 'readwrite', (store) => store.put({
    ...conversation,
    updatedAt: new Date().toISOString(),
  }))
}

export function clearConversations() {
  return transact(storeNames.conversations, 'readwrite', (store) => store.clear())
}

export function listMemories() {
  return transact(storeNames.memories, 'readonly', (store) => store.getAll())
    .then((memories) => memories.sort((left, right) => (
      right.updatedAt.localeCompare(left.updatedAt)
    )))
}

export function saveMemory(memory) {
  return transact(storeNames.memories, 'readwrite', (store) => store.put(memory))
}

export function deleteMemory(id) {
  return transact(storeNames.memories, 'readwrite', (store) => store.delete(id))
}

export function listDocuments() {
  return transact(storeNames.documents, 'readonly', (store) => store.getAll())
    .then((documents) => documents.sort((left, right) => (
      right.updatedAt.localeCompare(left.updatedAt)
    )))
}

export function saveDocument(document) {
  return transact(storeNames.documents, 'readwrite', (store) => store.put(document))
}

export function deleteDocument(id) {
  return transact(storeNames.documents, 'readwrite', (store) => store.delete(id))
}

const stopWords = new Set([
  'about', 'after', 'again', 'also', 'and', 'are', 'because', 'been', 'before', 'being',
  'between', 'but', 'can', 'could', 'did', 'does', 'for', 'from', 'have', 'here', 'how',
  'into', 'its', 'just', 'more', 'most', 'not', 'our', 'please', 'should', 'that', 'the',
  'their', 'them', 'then', 'there', 'these', 'they', 'this', 'those', 'through', 'use',
  'was', 'were', 'what', 'when', 'where', 'which', 'who', 'with', 'would', 'you', 'your',
])

export function findRelevantDocuments(query, documents, maximumResults = 3) {
  const terms = [...new Set((query.toLocaleLowerCase().match(/[\p{L}\p{N}]{2,}/gu) ?? [])
    .filter((term) => !stopWords.has(term)))]
    .slice(0, 12)
  if (terms.length === 0) return []

  return documents
    .map((document) => {
      const searchableText = `${document.name}\n${document.text}`.toLocaleLowerCase()
      let score = 0
      let firstMatch = -1
      for (const term of terms) {
        let position = searchableText.indexOf(term)
        let matches = 0
        while (position !== -1 && matches < 5) {
          if (firstMatch === -1) firstMatch = position
          matches += 1
          position = searchableText.indexOf(term, position + term.length)
        }
        score += matches * (document.name.toLocaleLowerCase().includes(term) ? 4 : 1)
      }
      return { document, score, firstMatch }
    })
    .filter((result) => result.score > 0)
    .sort((left, right) => right.score - left.score)
    .slice(0, maximumResults)
    .map(({ document, firstMatch }) => {
      const textStart = firstMatch < document.name.length + 1
        ? 0
        : firstMatch - document.name.length - 1
      const start = Math.max(0, textStart - 250)
      return {
        document,
        excerpt: document.text.slice(start, start + 1500),
      }
    })
}
