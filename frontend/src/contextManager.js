const tokenPattern = /[\p{L}\p{N}]+|\s+|[^\p{L}\p{N}\s]/gu
const imageTokenReserve = 4096
const messageTokenOverhead = 8
const systemTokenReserve = 128
const promptBudgetRatio = 0.75
export const defaultContextWindowTokens = 32768

export function estimateTextTokens(value) {
  const segments = String(value ?? '').match(tokenPattern) ?? []
  let estimate = 0

  for (const segment of segments) {
    if (/^\s+$/u.test(segment)) {
      estimate += 0.25
    } else if (/^[\p{L}\p{N}]+$/u.test(segment)) {
      estimate += /^[\x00-\x7F]+$/u.test(segment)
        ? Math.max(1, Math.ceil(segment.length / 4))
        : [...segment].length
    } else {
      estimate += [...segment].length
    }
  }

  return Math.ceil(estimate)
}

export function estimateMessageTokens(message) {
  const documentText = (message.documents ?? [])
    .map((document) => `${document.name ?? ''}\n${document.text ?? ''}`)
    .join('\n')
  const imageCount = (message.images ?? []).length

  return messageTokenOverhead +
    estimateTextTokens(`${message.content ?? ''}\n${documentText}`) +
    imageCount * imageTokenReserve
}

export function fitMessagesToContext(messages, localContext, contextWindowTokens) {
  const contextWindow = Number.isFinite(contextWindowTokens) && contextWindowTokens > 0
    ? contextWindowTokens
    : defaultContextWindowTokens
  const promptBudget = Math.floor(contextWindow * promptBudgetRatio)
  let estimatedPromptTokens = systemTokenReserve + estimateTextTokens(localContext)
  let firstIncludedIndex = messages.length

  while (firstIncludedIndex > 0) {
    const message = messages[firstIncludedIndex - 1]
    const messageTokens = estimateMessageTokens(message)

    if (estimatedPromptTokens + messageTokens > promptBudget) {
      if (firstIncludedIndex === messages.length) {
        throw new Error(
          `The newest message and its attachments exceed this model's estimated prompt budget (${promptBudget.toLocaleString()} tokens). Remove some attachments or text, or select a model with a larger context window.`,
        )
      }
      break
    }

    estimatedPromptTokens += messageTokens
    firstIncludedIndex -= 1
  }

  if (messages[firstIncludedIndex]?.role === 'assistant') {
    estimatedPromptTokens -= estimateMessageTokens(messages[firstIncludedIndex])
    firstIncludedIndex += 1
  }

  return {
    messages: messages.slice(firstIncludedIndex),
    omittedMessages: firstIncludedIndex,
    estimatedPromptTokens,
    promptBudget,
  }
}
