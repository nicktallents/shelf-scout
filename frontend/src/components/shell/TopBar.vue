<script setup lang="ts">
/**
 * Suite reference top bar: app name on the left, an account affordance on the
 * right via the `account` slot. Extraction-ready (ADR 0020) — no dependency on
 * app stores or domain types, only props/slots.
 */
defineProps<{
  appName: string
}>()
</script>

<template>
  <header class="top-bar">
    <span class="top-bar__app-name">{{ appName }}</span>
    <div class="top-bar__account">
      <slot name="account" />
    </div>
  </header>
</template>

<style scoped>
.top-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  height: var(--st-top-bar-height);
  padding: 0 var(--st-space-4);
  background: var(--st-color-surface);
  border-bottom: 1px solid var(--st-color-border);
}

.top-bar__app-name {
  font-weight: 600;
  font-size: var(--st-font-size-md);
  color: var(--st-color-text);
}
</style>
