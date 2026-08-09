import { useCallback, useEffect, useState } from 'react'
import { appointmentApi, MOCK_DOCTORS } from '../services/appointmentApi'
import { patientApi, MOCK_PATIENTS } from '../../patients/services/patientApi'

export function useAppointments(filters) {
  const [appointments, setAppointments] = useState([])
  const [slots, setSlots] = useState([])
  const [doctors, setDoctors] = useState([])
  const [patients, setPatients] = useState([])
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
      const normalizedSlots = Array.isArray(slotResult) ? slotResult : []
      setAppointments(appointmentResult.data || appointmentResult)
      setSlots(normalizedSlots)

      try {
        const doctorResult = await appointmentApi.getDoctors()
        setDoctors(doctorResult?.length ? doctorResult : getDoctorsFromSlots(normalizedSlots))
      } catch {
        setDoctors(getDoctorsFromSlots(normalizedSlots))
      }

      try {
        const patientResult = await patientApi.getAll({ pageSize: 50 })
        setPatients(patientResult.data || patientResult || [])
      } catch {
        setPatients(MOCK_PATIENTS)
      }
    } catch (err) {
      setError(err.response?.data?.message || err.message || 'Failed to load appointments')
      setAppointments([])
      setSlots([])
      setDoctors(MOCK_DOCTORS)
      setPatients(MOCK_PATIENTS)
    } finally {
      setLoading(false)
    }
  }, [filters])

  useEffect(() => { load() }, [load])

  const runMutation = async (action) => {
    setSaving(true)
    setError(null)
    try {
      const result = await action()
      await load()
      return result
    } catch (err) {
      setError(err.response?.data?.message || err.message || 'Failed to save appointment changes')
      throw err
    } finally {
      setSaving(false)
    }
  }

  return {
    appointments,
    slots,
    doctors,
    patients,
    loading,
    saving,
    error,
    refetch: load,
    createAppointment: (data) => runMutation(() => appointmentApi.create(data)),
    updateAppointment: (id, data) => runMutation(() => appointmentApi.update(id, data)),
    createSlot: (data) => runMutation(() => appointmentApi.createSlot(data)),
    updateStatus: (id, status) => runMutation(() => appointmentApi.updateStatus(id, status)),
    cancelAppointment: (id, reason) => runMutation(() => appointmentApi.cancel(id, reason)),
    rescheduleAppointment: (id, slotId) => runMutation(() => appointmentApi.reschedule(id, slotId)),
  }
}

function getDoctorsFromSlots(slots) {
  const byName = new Map()
  slots.forEach(slot => {
    if (slot?.doctorName && !byName.has(slot.doctorName)) {
      byName.set(slot.doctorName, slot.specialty || '')
    }
  })

  const doctors = Array.from(byName, ([doctorName, specialty]) => ({ doctorName, specialty }))
  return doctors.length ? doctors : MOCK_DOCTORS
}
