<script setup>
import { ref } from 'vue'

defineProps({
  modelValue: { type: String, required: true },
  thinkMode: { type: Boolean, default: false },
  sending: { type: Boolean, default: false },
  attachments: { type: Array, default: () => [] },
  attachmentNotice: { type: String, default: '' },
  processingAttachments: { type: Boolean, default: false },
  voiceNotice: { type: Boolean, default: false },
})

const emit = defineEmits([
  'update:modelValue',
  'send',
  'keydown',
  'toggle-think',
  'voice',
  'files-selected',
  'remove-attachment',
])
const fileInput = ref(null)

function openFilePicker() {
  fileInput.value?.click()
}

function handleFileSelection(event) {
  const files = Array.from(event.target.files ?? [])
  if (files.length) emit('files-selected', files)
  event.target.value = ''
}

function handlePaste(event) {
  const files = Array.from(event.clipboardData?.files ?? [])
  if (!files.length) return

  event.preventDefault()
  emit('files-selected', files)
}
</script>

<template>
  <div class="composer-wrap">
    <div v-if="attachments.length" class="attachment-previews" aria-label="Attached files">
      <div v-for="attachment in attachments" :key="attachment.id" class="attachment-preview">
        <img
          v-if="attachment.kind === 'image'"
          class="attachment-preview-image"
          :src="attachment.previewUrl"
          :alt="attachment.name"
        />
        <span v-else class="attachment-preview-icon" aria-hidden="true">DOC</span>
        <span class="attachment-preview-name" :title="attachment.name">{{ attachment.name }}</span>
        <button
          class="attachment-remove-button"
          type="button"
          :aria-label="`Remove ${attachment.name}`"
          @click="$emit('remove-attachment', attachment.id)"
        >×</button>
      </div>
    </div>
    <p v-if="attachmentNotice" class="composer-notice attachment-error" role="alert">{{ attachmentNotice }}</p>
    <p v-else-if="processingAttachments" class="composer-notice" aria-live="polite">Reading attachments...</p>
    <p v-else-if="voiceNotice" class="composer-notice">Voice input isn’t available in this preview.</p>
    <form class="composer" @submit.prevent="$emit('send')">
      <button
        class="composer-plus"
        type="button"
        aria-label="Attach files"
        title="Attach files"
        :disabled="sending || processingAttachments"
        @click="openFilePicker"
      >
        <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
          <path d="M12 5v14M5 12h14" />
        </svg>
      </button>
      <input
        ref="fileInput"
        class="hidden-file-input"
        type="file"
        multiple
        accept="image/png,image/jpeg,image/webp,.png,.jpg,.jpeg,.webp,.txt,.md,.csv,.json,application/pdf,.pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document,.docx"
        aria-label="Choose files to attach"
        @change="handleFileSelection"
      />
      <textarea
        :value="modelValue"
        rows="1"
        maxlength="12000"
        placeholder="Message ZusyAI"
        aria-label="Message ZusyAI"
        @input="$emit('update:modelValue', $event.target.value)"
        @keydown="$emit('keydown', $event)"
        @paste="handlePaste"
      ></textarea>
      <div class="composer-tools">
        <button
          class="think-button"
          :class="{ active: thinkMode }"
          type="button"
          :aria-pressed="thinkMode"
          @click="$emit('toggle-think')"
        >
          <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
            <path d="M9 4.5a3.5 3.5 0 0 0-3.1 5.1A3.5 3.5 0 0 0 7 16a3 3 0 0 0 5 2.2V5.8a3 3 0 0 0-3-1.3Zm6 0a3.5 3.5 0 0 1 3.1 5.1A3.5 3.5 0 0 1 17 16a3 3 0 0 1-5 2.2V5.8a3 3 0 0 1 3-1.3Z" />
          </svg>
          <span>Think</span>
        </button>
        <button class="microphone-button" type="button" aria-label="Voice input" @click="$emit('voice')">
          <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
            <rect x="9" y="3" width="6" height="12" rx="3" />
            <path d="M5.5 11.5a6.5 6.5 0 0 0 13 0M12 18v3m-3 0h6" />
          </svg>
        </button>
        <button
          v-if="(modelValue.trim() || attachments.length) && !sending"
          class="send-button"
          type="submit"
          aria-label="Send message"
          :disabled="processingAttachments"
          :title="processingAttachments ? 'Wait for attachments to finish reading' : 'Send message'"
        >
          <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
            <path d="M12 19V5m-6 6 6-6 6 6" />
          </svg>
        </button>
        <button
          v-else-if="sending"
          class="send-button sending-button"
          type="button"
          aria-label="Waiting for response"
          disabled
        >
          <span class="send-spinner"></span>
        </button>
        <button
          v-else
          class="voice-mode-button"
          type="button"
          aria-label="Voice mode (preview unavailable)"
          title="Voice mode isn’t available in this preview"
          @click="$emit('voice')"
        >
          <span></span><span></span><span></span><span></span><span></span>
        </button>
      </div>
    </form>
  </div>
</template>
