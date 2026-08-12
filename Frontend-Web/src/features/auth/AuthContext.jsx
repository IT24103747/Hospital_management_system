import { createContext, useContext, useMemo, useState } from 'react'

const AuthContext = createContext(null)

const readUser = () => {
  try { return JSON.parse(localStorage.getItem('hms_user')) } catch { return null }
}

export function AuthProvider({ children }) {
  const [user, setUser] = useState(readUser)
  const signIn = response => {
    const next = { userId: response.userId, doctorId: response.doctorId, fullName: response.fullName, email: response.email, role: response.role }
    localStorage.setItem('hms_token', response.token)
    localStorage.setItem('hms_user', JSON.stringify(next))
    setUser(next)
    return next
  }
  const updateUser = changes => setUser(current => {
    const next = { ...current, ...changes }
    localStorage.setItem('hms_user', JSON.stringify(next))
    return next
  })
  const signOut = () => {
    localStorage.removeItem('hms_token')
    localStorage.removeItem('hms_user')
    setUser(null)
  }
  const value = useMemo(() => ({ user, signIn, signOut, updateUser }), [user])
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export const useAuth = () => useContext(AuthContext)
