import { useState, useEffect, useCallback } from 'react'
import { patientApi } from '../services/patientApi'

const USE_MOCK = false

export function usePatients({ search = '', gender = '', bloodGroup = '', sortBy = 'createdAt', sortDirection = 'desc', page = 1, pageSize = 12 } = {}) {
  const [patients, setPatients] = useState([])
  const [pagination, setPagination] = useState({ page, pageSize, totalCount: 0, totalPages: 0 })
  const [summary, setSummary] = useState(null)
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
        const [result, summaryResult] = await Promise.all([
          patientApi.getAll({ search, gender, bloodGroup, sortBy, sortDirection, page, pageSize }),
          patientApi.getSummary(),
        ])
        setPatients(result.data || [])
        setSummary(summaryResult)
        setPagination({
          page: result.page || page,
          pageSize: result.pageSize || pageSize,
          totalCount: result.totalCount || 0,
          totalPages: result.totalPages || 0,
        })
      }
    } catch (err) {
      setError(err.message || 'Failed to load patients')
      setPatients([])
    } finally {
      setLoading(false)
    }
  }, [bloodGroup, gender, page, pageSize, search, sortBy, sortDirection])

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

  return { patients, pagination, summary, loading, error, refetch: load, createPatient, updatePatient, deletePatient }
}
