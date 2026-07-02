<script setup lang="ts">
import { useIdentityStore } from '@/stores/identity'

const identity = useIdentityStore()
</script>

<template>
  <main class="account">
    <h1>Account</h1>

    <p v-if="identity.status === 'loading' || identity.status === 'idle'">Loading…</p>
    <p v-else-if="identity.status === 'error'">Couldn't load your account. Try reloading.</p>
    <template v-else>
      <p class="account__signed-in-as">
        Signed in as <strong>{{ identity.displayName }}</strong> / {{ identity.uid }}
      </p>

      <a
        v-if="identity.signOutUrl"
        class="account__sign-out"
        :href="identity.signOutUrl"
        rel="noopener"
      >
        Sign out
      </a>
      <p v-else class="account__sign-out-unavailable">
        Sign-out isn't configured in this environment.
      </p>
    </template>
  </main>
</template>

<style scoped>
.account {
  padding: var(--st-space-5);
  display: flex;
  flex-direction: column;
  gap: var(--st-space-4);
  max-width: 480px;
}

.account__signed-in-as {
  color: var(--st-color-text);
}

.account__sign-out {
  align-self: flex-start;
  padding: var(--st-space-2) var(--st-space-4);
  border-radius: var(--st-radius-sm);
  background: var(--st-color-danger);
  color: var(--st-color-brand-contrast);
  text-decoration: none;
  font-weight: 600;
}

.account__sign-out-unavailable {
  color: var(--st-color-text-muted);
  font-size: var(--st-font-size-sm);
}
</style>
