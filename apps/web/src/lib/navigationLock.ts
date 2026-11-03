import { createContext, useContext } from 'react'

export interface NavigationLock {
  isLocked: boolean
  setLocked: (locked: boolean) => void
}

export const NavigationLockContext = createContext<NavigationLock>({
  isLocked: false,
  setLocked: () => undefined,
})

export function useNavigationLock(): NavigationLock {
  return useContext(NavigationLockContext)
}
