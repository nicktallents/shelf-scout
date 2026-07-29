<script setup lang="ts">
/**
 * Landing states for a household-less user (ADR 0013): "household-less" is exactly zero
 * Membership rows, and there is no half-state in the data. `canCreateHousehold` (from the
 * Authentik `family` group) is the only other signal, so there are two branches here:
 *
 * - allowlisted -> "create" (the actual form is built by issue #25; this is its landing spot).
 * - not allowlisted -> "enter-invite": paste a link to join (issue #28 owns the invite route
 *   this navigates to). A visitor with no link in hand reads the same copy as the "dead-end"
 *   screen from ADR 0013 — there's no separate technical state to distinguish "holds a token"
 *   from "doesn't", so the copy itself carries that distinction.
 */
import { ref } from 'vue'
import { useIdentityStore } from '@/stores/identity'

const identity = useIdentityStore()
const inviteLink = ref('')

function joinWithInviteLink() {
  const trimmed = inviteLink.value.trim()
  if (trimmed) {
    window.location.href = trimmed
  }
}
</script>

<template>
  <section v-if="identity.canCreateHousehold" class="landing" data-testid="landing-create">
    <h1>Set up your household</h1>
    <p>You're on the allowlist — you can create a household and start tracking your inventory.</p>
  </section>

  <section v-else class="landing" data-testid="landing-enter-invite">
    <h1>You need an invite</h1>
    <p>Ask an existing household member for an invite link, then paste it below to join.</p>

    <form class="landing__invite-form" @submit.prevent="joinWithInviteLink">
      <label for="invite-link">Invite link</label>
      <input id="invite-link" v-model="inviteLink" type="url" placeholder="https://…" />
      <button type="submit" :disabled="!inviteLink.trim()">Join household</button>
    </form>
  </section>
</template>

<style scoped>
.landing {
  padding: var(--st-space-5);
  display: flex;
  flex-direction: column;
  gap: var(--st-space-3);
  max-width: 480px;
  color: var(--st-color-text);
}

.landing p {
  color: var(--st-color-text-muted);
}

.landing__invite-form {
  display: flex;
  flex-direction: column;
  gap: var(--st-space-2);
  margin-top: var(--st-space-3);
}

.landing__invite-form input {
  padding: var(--st-space-2) var(--st-space-3);
  border: 1px solid var(--st-color-border);
  border-radius: var(--st-radius-sm);
  background: var(--st-color-background);
  color: var(--st-color-text);
  font-size: var(--st-font-size-md);
}

.landing__invite-form button {
  align-self: flex-start;
  padding: var(--st-space-2) var(--st-space-4);
  border: none;
  border-radius: var(--st-radius-sm);
  background: var(--st-color-brand);
  color: var(--st-color-brand-contrast);
  font-weight: 600;
  cursor: pointer;
}

.landing__invite-form button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
</style>
