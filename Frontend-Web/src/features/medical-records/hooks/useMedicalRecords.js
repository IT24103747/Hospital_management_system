import { useState, useEffect, useCallback } from 'react'
import { medicalRecordApi } from '../services/medicalRecordApi'

export function useMedicalRecords({
  search = '',
  recordType = '',
  status = '',
  fromDate = '',
  toDate = '',
  page = 1,
  pageSize = 10,
} = {}) {
  const [records, setRecords] = useState([])
  const [summary, setSummary] = useState(null)
  const [totalCount, setTotalCount] = useState(0)
  const [totalPages, setTotalPages] = useState(1)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState(null)

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const [recordsRes, summaryRes] = await Promise.all([
        medicalRecordApi.getAll({
          search: search || undefined,
          recordType: recordType || undefined,
          status: status || undefined,
          fromDate: fromDate || undefined,
          toDate: toDate || undefined,
          page,
          pageSize,
        }),
        medicalRecordApi.getSummary().catch(() => null),
      ])

      setRecords(recordsRes.data || [])
      setTotalCount(recordsRes.totalCount || 0)
      setTotalPages(recordsRes.totalPages || 1)
      if (summaryRes) setSummary(summaryRes)
    } catch (err) {
      console.error('Failed to fetch medical records:', err)
      setError(err.response?.data?.message || 'Failed to load medical records')
    } finally {
      setLoading(false)
    }
  }, [search, recordType, status, fromDate, toDate, page, pageSize])

  useEffect(() => {
    load()
  }, [load])

  const createRecord = async (data) => {
    setSaving(true)
    try {
      const res = await medicalRecordApi.create(data)
      await load()
      return { success: true, data: res }
    } catch (err) {
      return { success: false, error: err.response?.data?.message || 'Failed to create record' }
    } finally {
      setSaving(false)
    }
  }

  const updateRecord = async (id, data) => {
    setSaving(true)
    try {
      const res = await medicalRecordApi.update(id, data)
      setRecords((prev) =>
        prev.map((r) => (r.medicalRecordId === id ? { ...r, ...data } : r))
      )
      await load()
      return { success: true, data: res }
    } catch (err) {
      return { success: false, error: err.response?.data?.message || 'Failed to update record' }
    } finally {
      setSaving(false)
    }
  }

  const deleteRecord = async (id) => {
    setSaving(true)
    try {
      await medicalRecordApi.delete(id)
      setRecords((prev) => prev.filter((r) => r.medicalRecordId !== id))
      setTotalCount((prev) => Math.max(0, prev - 1))
      await load()
      return { success: true }
    } catch (err) {
      return { success: false, error: err.response?.data?.message || 'Failed to delete record' }
    } finally {
      setSaving(false)
    }
  }

  const addAttachment = async (recordId, attachmentData) => {
    try {
      const res = await medicalRecordApi.addAttachment(recordId, attachmentData)
      await load()
      return { success: true, data: res }
    } catch (err) {
      return { success: false, error: err.response?.data?.message || 'Failed to add attachment' }
    }
  }

  const deleteAttachment = async (recordId, attachmentId) => {
    try {
      await medicalRecordApi.deleteAttachment(recordId, attachmentId)
      await load()
      return { success: true }
    } catch (err) {
      return { success: false, error: err.response?.data?.message || 'Failed to delete attachment' }
    }
  }

  return {
    records,
    summary,
    totalCount,
    totalPages,
    loading,
    saving,
    error,
    refetch: load,
    createRecord,
    updateRecord,
    deleteRecord,
    addAttachment,
    deleteAttachment,
  }
}
