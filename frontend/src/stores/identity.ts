import { defineStore } from 'pinia'

interface Whoami {
  uid: string | null
  email: string | null
  groups: string[]
  signOutUrl: string | null
}

interface IdentityState extends Whoami {
  status: 'idle' | 'loading' | 'loaded' | 'error'
}

export const useIdentityStore = defineStore('identity', {
  state: (): IdentityState => ({
    uid: null,
    email: null,
    groups: [],
    signOutUrl: null,
    status: 'idle',
  }),
  getters: {
    displayName(state): string {
      return state.email ?? state.uid ?? ''
    },
    initials(): string {
      const name = this.displayName.split('@')[0] ?? ''
      const parts = name.split(/[.\-_ ]+/).filter(Boolean)
      const letters = parts.length > 0 ? parts.slice(0, 2).map((part) => part[0]) : [name[0]]
      return letters.join('').toUpperCase() || '?'
    },
  },
  actions: {
    async load() {
      this.status = 'loading'
      try {
        const response = await fetch('/api/whoami', { headers: { Accept: 'application/json' } })
        if (!response.ok) {
          this.status = 'error'
          return
        }
        const data = (await response.json()) as Whoami
        this.uid = data.uid
        this.email = data.email
        this.groups = data.groups
        this.signOutUrl = data.signOutUrl
        this.status = 'loaded'
      } catch {
        this.status = 'error'
      }
    },
  },
})
