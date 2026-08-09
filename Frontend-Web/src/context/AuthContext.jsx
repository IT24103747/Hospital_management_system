import { createContext, useContext, useState, useEffect } from 'react'
import apiClient from '../lib/apiClient'

const AuthContext = createContext(null)

export function AuthProvider({ children }) {
  const [user, setUser] = useState(() => {
    const saved = localStorage.getItem('hms_user')
    return saved ? JSON.parse(saved) : null
  })
  const [token, setToken] = useState(() => localStorage.getItem('hms_token') || '')

  const login = async (email, password) => {
    try {
      const response = await apiClient.post('/auth/login', { email, password })
      const data = response.data

      const userData = {
        userId: data.userId,
        role: data.role,
        email: data.email,
        fullName: data.fullName,
        doctorProfile: data.doctorProfile,
      }

      setUser(userData)
      setToken(data.token)
      localStorage.setItem('hms_token', data.token)
      localStorage.setItem('hms_user', JSON.stringify(userData))

      return { success: true, user: userData }
    } catch (err) {
      const msg = err.response?.data?.message || err.message || 'Login failed'
      return { success: false, message: msg }
    }
  }

  const logout = () => {
    setUser(null)
    setToken('')
    localStorage.removeItem('hms_token')
    localStorage.removeItem('hms_user')
  }

  const updateDoctorProfile = (updatedProfile) => {
    setUser((prev) => {
      if (!prev) return prev
      const newName = `${updatedProfile.firstName} ${updatedProfile.lastName}`
      const updatedUser = {
        ...prev,
        fullName: newName,
        doctorProfile: {
          ...prev.doctorProfile,
          ...updatedProfile,
          fullName: newName,
        },
      }
      localStorage.setItem('hms_user', JSON.stringify(updatedUser))
      return updatedUser
    })
  }

  return (
    <AuthContext.Provider value={{ user, token, login, logout, updateDoctorProfile }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) {
    throw new Error('useAuth must be used within an AuthProvider')
  }
  return ctx
}
