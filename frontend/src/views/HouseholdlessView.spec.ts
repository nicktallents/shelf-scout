import { describe, expect, it, beforeEach } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { mount } from '@vue/test-utils'

import { useIdentityStore } from '@/stores/identity'
import HouseholdlessView from './HouseholdlessView.vue'

describe('HouseholdlessView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('renders the create branch when allowlisted', () => {
    const identity = useIdentityStore()
    identity.canCreateHousehold = true

    const wrapper = mount(HouseholdlessView)

    expect(wrapper.find('[data-testid="landing-create"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="landing-enter-invite"]').exists()).toBe(false)
  })

  // Not-allowlisted-no-household and no-household-no-invite are the same AC-named states
  // (see the design-rationale comment atop HouseholdlessView.vue): there is no data signal
  // that distinguishes "holds an invite link" from "doesn't" until a link is actually
  // submitted, so both render the same enter-invite screen — which doubles as the dead end
  // for a visitor with no link, since the form has nowhere to go without one.
  it('renders the enter-invite branch (also the dead end) when not allowlisted', () => {
    const identity = useIdentityStore()
    identity.canCreateHousehold = false

    const wrapper = mount(HouseholdlessView)

    expect(wrapper.find('[data-testid="landing-enter-invite"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="landing-create"]').exists()).toBe(false)
  })
})
