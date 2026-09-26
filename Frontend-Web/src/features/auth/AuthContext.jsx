import { createContext, useContext, useEffect, useMemo, useState } from 'react'

const AuthContext = createContext(null)
const tokenKey = 'hms_token'
const userKey = 'hms_user'

const clearStoredSession = () => {
  localStorage.removeItem(tokenKey)
  localStorage.removeItem(userKey)
}

const tokenExpiresAt = (token) => {
  try {
    const payload = token.split('.')[1]
    if (!payload) return null
    const base64 = payload.replace(/-/g, '+').replace(/_/g, '/')
    const paddedBase64 = base64.padEnd(Math.ceil(base64.length / 4) * 4, '=')
    const decoded = new TextDecoder().decode(
      Uint8Array.from(atob(paddedBase64), (character) => character.charCodeAt(0)),
    )
    const { exp } = JSON.parse(decoded)
    return typeof exp === 'number' ? exp * 1000 : null
  } catch {
    return null
  }
}

const readUser = () => {
  const token = localStorage.getItem(tokenKey)
  const expiresAt = tokenExpiresAt(token || '')
  if (!token || !expiresAt || expiresAt <= Date.now()) {
    clearStoredSession()
    return null
  }

  try {
    return JSON.parse(localStorage.getItem(userKey))
  } catch {
    clearStoredSession()
    return null
  }
}

export function AuthProvider({ children }) {
  const [user, setUser] = useState(readUser)
  const signIn = response => {
    const next = { userId: response.userId, doctorId: response.doctorId, fullName: response.fullName, email: response.email, role: response.role }
    localStorage.setItem(tokenKey, response.token)
    localStorage.setItem(userKey, JSON.stringify(next))
    setUser(next)
    return next
  }
  const updateUser = changes => setUser(current => {
    const next = { ...current, ...changes }
    localStorage.setItem(userKey, JSON.stringify(next))
    return next
  })
  const signOut = () => {
    clearStoredSession()
    setUser(null)
  }

  useEffect(() => {
    if (!user) return undefined

    const expiresAt = tokenExpiresAt(localStorage.getItem(tokenKey) || '')
    if (!expiresAt || expiresAt <= Date.now()) {
      clearStoredSession()
      setUser(null)
      return undefined
    }

    const timeoutId = window.setTimeout(() => {
      clearStoredSession()
      setUser(null)
    }, expiresAt - Date.now())

    return () => window.clearTimeout(timeoutId)
  }, [user])
  const value = useMemo(() => ({ user, signIn, signOut, updateUser }), [user])
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export const useAuth = () => useContext(AuthContext)
