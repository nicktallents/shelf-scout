<script setup lang="ts">
/**
 * Round account affordance button showing initials/email. Purely presentational —
 * the caller decides what "opening the account screen" means (extraction-ready,
 * ADR 0020).
 */
defineProps<{
  label: string
}>()

const emit = defineEmits<{
  activate: []
}>()
</script>

<template>
  <button
    type="button"
    class="account-affordance"
    :aria-label="`Account: ${label}`"
    @click="emit('activate')"
  >
    {{ label }}
  </button>
</template>

<style scoped>
.account-affordance {
  display: flex;
  align-items: center;
  justify-content: center;
  width: var(--st-size-avatar-sm);
  height: var(--st-size-avatar-sm);
  border-radius: var(--st-radius-full);
  border: 1px solid var(--st-color-border);
  background: var(--st-color-brand);
  color: var(--st-color-brand-contrast);
  font-size: var(--st-font-size-sm);
  font-weight: 600;
  cursor: pointer;
}

.account-affordance:hover {
  filter: brightness(1.08);
}

.account-affordance:focus-visible {
  outline: var(--st-focus-outline-width) solid var(--st-color-brand);
  outline-offset: var(--st-focus-outline-offset);
}
</style>
