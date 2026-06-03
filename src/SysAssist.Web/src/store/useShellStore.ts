import { create } from 'zustand'
import type { CurrentUserDto } from '../api/client'

export type PageId =
  | 'dashboard'
  | 'events'
  | 'event-detail'
  | 'approvals'
  | 'modules'
  | 'modules-store'
  | 'actions'
  | 'audit'
  | 'logs'
  | 'notifications'
  | 'users'
  | 'support'
  | 'diagnostics'

export type ToastTone = 'success' | 'warning' | 'danger' | 'neutral'

export type ToastMessage = {
  id: string
  tone: ToastTone
  title: string
  message?: string
}

type PersistedSession = {
  token: string
  user: CurrentUserDto
}

type ShellState = {
  activePage: PageId
  selectedEventId?: string
  token?: string
  user?: CurrentUserDto
  search: string
  toasts: ToastMessage[]
  setActivePage: (page: PageId) => void
  openEvent: (id: string) => void
  setSearch: (search: string) => void
  setSession: (token: string, user: CurrentUserDto) => void
  clearSession: () => void
  pushToast: (toast: Omit<ToastMessage, 'id'>) => void
  dismissToast: (id: string) => void
}

const storageKey = 'sysassist.session'

function readSession(): PersistedSession | undefined {
  try {
    const raw = localStorage.getItem(storageKey)
    return raw ? (JSON.parse(raw) as PersistedSession) : undefined
  } catch {
    return undefined
  }
}

const persisted = readSession()

export const useShellStore = create<ShellState>((set) => ({
  activePage: 'dashboard',
  token: persisted?.token,
  user: persisted?.user,
  search: '',
  toasts: [],
  setActivePage: (page) => set({ activePage: page }),
  openEvent: (id) => set({ activePage: 'event-detail', selectedEventId: id }),
  setSearch: (search) => set({ search }),
  setSession: (token, user) => {
    localStorage.setItem(storageKey, JSON.stringify({ token, user }))
    set({ token, user, activePage: 'dashboard' })
  },
  clearSession: () => {
    localStorage.removeItem(storageKey)
    set({ token: undefined, user: undefined, activePage: 'dashboard' })
  },
  pushToast: (toast) => {
    const id = crypto.randomUUID()
    set((state) => ({ toasts: [...state.toasts, { ...toast, id }].slice(-5) }))
    window.setTimeout(() => {
      set((state) => ({ toasts: state.toasts.filter((item) => item.id !== id) }))
    }, 5200)
  },
  dismissToast: (id) => set((state) => ({ toasts: state.toasts.filter((item) => item.id !== id) })),
}))
