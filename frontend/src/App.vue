<script setup lang="ts">
import { onMounted } from 'vue'
import { RouterView, useRouter } from 'vue-router'
import TopBar from '@/components/shell/TopBar.vue'
import AccountAffordance from '@/components/shell/AccountAffordance.vue'
import { useIdentityStore } from '@/stores/identity'

const identity = useIdentityStore()
const router = useRouter()

onMounted(() => {
  void identity.load()
})
</script>

<template>
  <TopBar app-name="Shelf Scout">
    <template #account>
      <AccountAffordance :label="identity.initials" @activate="router.push({ name: 'account' })" />
    </template>
  </TopBar>

  <RouterView />
</template>
