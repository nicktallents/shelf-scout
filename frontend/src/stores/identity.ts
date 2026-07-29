import { defineStore } from 'pinia'

export interface Membership {
  householdId: number
  householdName: string
  role: 'Owner' | 'Member'
}

interface ContextUser {
  id: number
  displayName: string | null
  email: string | null
}

interface Context {
  user: ContextUser
  memberships: Membership[]
  canCreateHousehold: boolean
  signOutUrl: string | null
}

interface IdentityState {
  status: 'idle' | 'loading' | 'loaded' | 'error'
  user: ContextUser | null
  memberships: Membership[]
  canCreateHousehold: boolean
  signOutUrl: string | null
}

export const useIdentityStore = defineStore('identity', {
  state: (): IdentityState => ({
    status: 'idle',
    user: null,
    memberships: [],
    canCreateHousehold: false,
    signOutUrl: null,
  }),
  getters: {
    displayName(state): string {
      return state.user?.displayName ?? state.user?.email ?? ''
    },
    initials(): string {
      const name = this.displayName.split('@')[0] ?? ''
      const parts = name.split(/[.\-_ ]+/).filter(Boolean)
      const letters = parts.length > 0 ? parts.slice(0, 2).map((part) => part[0]) : [name[0]]
      return letters.join('').toUpperCase() || '?'
    },
    // Household-less is exactly "zero Membership rows" (ADR 0013) — no other signal needed.
    hasHousehold(state): boolean {
      return state.memberships.length > 0
    },
  },
  actions: {
    async load() {
      this.status = 'loading'
      try {
        const response = await fetch('/api/context', { headers: { Accept: 'application/json' } })
        if (!response.ok) {
          this.status = 'error'
          return
        }
        const data = (await response.json()) as Context
        this.user = data.user
        this.memberships = data.memberships
        this.canCreateHousehold = data.canCreateHousehold
        this.signOutUrl = data.signOutUrl
        this.status = 'loaded'
      } catch {
        this.status = 'error'
      }
    },
  },
})
