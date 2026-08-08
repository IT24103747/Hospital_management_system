import { useState, useEffect, useCallback } from 'react'
import { patientApi, MOCK_PATIENTS } from '../services/patientApi'

const USE_MOCK = true

export function usePatients() {
  const [patients, setPatients] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      if (USE_MOCK) {
        await new Promise(r => setTimeout(r, 600))
        setPatients(MOCK_PATIENTS)
      } else {
        const data = await patientApi.getAll()
        setPatients(data)
      }
    } catch (err) {
      setError(err.message || 'Failed to load patients')
      setPatients(MOCK_PATIENTS)
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { load() }, [load])

  const createPatient = async (data) => {
    if (USE_MOCK) {
      const newP = { ...data, patientId: Date.now(), createdAt: new Date().toISOString() }
      setPatients(prev => [newP, ...prev])
      return newP
    }
    const created = await patientApi.create(data)
    await load()
    return created
  }

  const updatePatient = async (id, data) => {
    if (USE_MOCK) {
      setPatients(prev => prev.map(p => (p.patientId === id ? { ...p, ...data } : p)))
      return
    }
    await patientApi.update(id, data)
    await load()
  }

  const deletePatient = async (id) => {
    if (USE_MOCK) {
      setPatients(prev => prev.filter(p => p.patientId !== id))
      return
    }
    await patientApi.delete(id)
    await load()
  }

  return { patients, loading, error, refetch: load, createPatient, updatePatient, deletePatient }
}
