import { useCallback, useEffect, useState } from 'react'
import { appointmentApi } from '../services/appointmentApi'

export function useAppointments(filters) {
  const [appointments, setAppointments] = useState([])
  const [slots, setSlots] = useState([])
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState(null)

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const [appointmentResult, slotResult] = await Promise.all([
        appointmentApi.getAll({ ...filters, pageSize: 50 }),
        appointmentApi.getSlots({ onlyAvailable: false }),
      ])
      setAppointments(appointmentResult.data || appointmentResult)
      setSlots(slotResult)
    } catch (err) {
      setError(err.response?.data?.message || err.message || 'Failed to load appointments')
      setAppointments([])
      setSlots([])
    } finally {
      setLoading(false)
    }
  }, [filters])

  useEffect(() => { load() }, [load])

  const runMutation = async (action) => {
    setSaving(true)
    try {
      const result = await action()
      await load()
      return result
    } finally {
      setSaving(false)
    }
  }

  return {
    appointments,
    slots,
    loading,
    saving,
    error,
    refetch: load,
    createAppointment: (data) => runMutation(() => appointmentApi.create(data)),
    createSlot: (data) => runMutation(() => appointmentApi.createSlot(data)),
    updateStatus: (id, status) => runMutation(() => appointmentApi.updateStatus(id, status)),
    cancelAppointment: (id, reason) => runMutation(() => appointmentApi.cancel(id, reason)),
    rescheduleAppointment: (id, slotId) => runMutation(() => appointmentApi.reschedule(id, slotId)),
  }
}
