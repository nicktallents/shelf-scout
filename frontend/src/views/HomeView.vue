<script setup lang="ts">
import { useIdentityStore } from '@/stores/identity'
import HouseholdlessView from './HouseholdlessView.vue'

const identity = useIdentityStore()
</script>

<template>
  <main class="home">
    <p v-if="identity.status === 'loading' || identity.status === 'idle'">Loading…</p>
    <p v-else-if="identity.status === 'error'">Couldn't load your account. Try reloading.</p>
    <HouseholdlessView v-else-if="!identity.hasHousehold" />
    <p v-else class="home__ready">
      Shelf Scout is set up. Inventory features land in a later scope.
    </p>
  </main>
</template>

<style scoped>
.home {
  padding: var(--st-space-5);
  color: var(--st-color-text-muted);
}
</style>
