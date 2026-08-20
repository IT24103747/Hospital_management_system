import { useState, useEffect, useMemo } from 'react'
import {
  FileText,
  Search,
  Plus,
  Filter,
  Calendar,
  Eye,
  Pencil,
  Trash2,
  Paperclip,
  Activity,
  CheckCircle2,
  FileCheck,
  AlertCircle,
  RotateCcw,
  Stethoscope,
  FlaskConical,
  ClipboardList,
  X,
} from 'lucide-react'
import { useMedicalRecords } from '../hooks/useMedicalRecords'
import { patientApi } from '../../patients/services/patientApi'
import { useAuth } from '../../auth/AuthContext'
import Button from '../../../components/Button'
import Badge from '../../../components/Badge'
import Table from '../../../components/Table'
import Modal from '../../../components/Modal'
import MedicalRecordDetailModal from '../components/MedicalRecordDetailModal'
import MedicalRecordFormModal from '../components/MedicalRecordFormModal'
import { getInitials, nameToGradient } from '../../../lib/utils'
import './MedicalRecordsPage.css'

export default function MedicalRecordsPage() {
  const { user } = useAuth()
  const [search, setSearch] = useState('')
  const [recordType, setRecordType] = useState('')
  const [status, setStatus] = useState('')
  const [fromDate, setFromDate] = useState('')
  const [toDate, setToDate] = useState('')
  const [page, setPage] = useState(1)
  const pageSize = 10

  const [patients, setPatients] = useState([])
  const [selectedRecord, setSelectedRecord] = useState(null)
  const [editingRecord, setEditingRecord] = useState(null)
  const [isFormOpen, setIsFormOpen] = useState(false)
  const [deleteTarget, setDeleteTarget] = useState(null)

  const {
    records,
    summary,
    totalCount,
    totalPages,
    loading,
    saving,
    error,
    refetch,
    createRecord,
    updateRecord,
    deleteRecord,
    addAttachment,
    deleteAttachment,
  } = useMedicalRecords({
    search,
    recordType,
    status,
    fromDate,
    toDate,
    page,
    pageSize,
  })

  useEffect(() => {
    patientApi.getAll({ pageSize: 100 }).then((res) => {
      setPatients(res.data || [])
    }).catch(console.error)
  }, [])

  const handleCreateOrUpdate = async (formData) => {
    if (editingRecord) {
      const res = await updateRecord(editingRecord.medicalRecordId, formData)
      if (res.success) {
        // Upload any newly attached files
        const newAttachments = (formData.attachments || []).filter((a) => !a.attachmentId)
        for (const att of newAttachments) {
          await addAttachment(editingRecord.medicalRecordId, att)
        }

        // Delete any attachments that were removed during edit
        const originalIds = (editingRecord.attachments || []).map((a) => a.attachmentId)
        const remainingIds = new Set(
          (formData.attachments || []).map((a) => a.attachmentId).filter(Boolean)
        )
        for (const origId of originalIds) {
          if (!remainingIds.has(origId)) {
            await deleteAttachment(editingRecord.medicalRecordId, origId)
          }
        }

        setIsFormOpen(false)
        setEditingRecord(null)
      } else {
        alert(res.error)
      }
    } else {
      const res = await createRecord(formData)
      if (res.success) {
        setIsFormOpen(false)
      } else {
        alert(res.error)
      }
    }
  }

  const handleDeleteConfirm = async () => {
    if (!deleteTarget) return
    const res = await deleteRecord(deleteTarget.medicalRecordId)
    if (res.success) {
      setDeleteTarget(null)
      if (selectedRecord?.medicalRecordId === deleteTarget.medicalRecordId) {
        setSelectedRecord(null)
      }
    } else {
      alert(res.error)
    }
  }

  const handleOpenEdit = (record) => {
    setSelectedRecord(null)
    setEditingRecord(record)
    setIsFormOpen(true)
  }

  const handleResetFilters = () => {
    setSearch('')
    setRecordType('')
    setStatus('')
    setFromDate('')
    setToDate('')
    setPage(1)
  }

  const handleAddAttachment = async (recordId, data) => {
    const res = await addAttachment(recordId, data)
    if (res?.data) {
      setSelectedRecord((prev) =>
        prev && prev.medicalRecordId === recordId
          ? { ...prev, attachments: [...(prev.attachments || []), res.data] }
          : prev
      )
    }
    return res
  }

  const handleDeleteAttachment = async (recordId, attachmentId) => {
    const res = await deleteAttachment(recordId, attachmentId)
    if (res?.success) {
      setSelectedRecord((prev) =>
        prev && prev.medicalRecordId === recordId
          ? {
              ...prev,
              attachments: (prev.attachments || []).filter((a) => a.attachmentId !== attachmentId),
            }
          : prev
      )
    }
    return res
  }

  const hasActiveFilters = Boolean(search || recordType || status || fromDate || toDate)

  const getTypeVariant = (type) => {
    switch (type) {
      case 'Consultation': return 'primary'
      case 'LabReport': return 'info'
      case 'DischargeSummary': return 'warning'
      case 'Prescription': return 'accent'
      default: return 'default'
    }
  }

  const columns = [
    {
      key: 'id',
      label: 'Record',
      render: (row) => (
        <div>
          <div style={{ fontWeight: 700, color: 'var(--clr-primary)', fontSize: '0.88rem' }}>
            #{row.medicalRecordId}
          </div>
          <div style={{ fontSize: '0.74rem', color: 'var(--text-muted)', marginTop: '2px' }}>
            {new Date(row.recordDate).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })}
          </div>
        </div>
      ),
    },
    {
      key: 'patient',
      label: 'Patient',
      render: (row) => {
        const [c1, c2] = nameToGradient(row.patientName || 'Patient')
        return (
          <div className="mr-patient-cell">
            <div
              className="mr-patient-avatar"
              style={{ background: `linear-gradient(135deg, ${c1}, ${c2})` }}
            >
              {getInitials(row.patientName?.split(' ')[0] || 'P', row.patientName?.split(' ')[1] || 'U')}
            </div>
            <div>
              <div className="mr-patient-name">{row.patientName || 'Unknown Patient'}</div>
              <div className="mr-patient-email">{row.patientEmail || '—'}</div>
            </div>
          </div>
        )
      },
    },
    {
      key: 'doctor',
      label: 'Doctor',
      render: (row) => (
        <div>
          <div style={{ fontWeight: 600, color: 'var(--text-primary)', fontSize: '0.84rem' }}>
            {row.doctorName || 'Assigned Clinician'}
          </div>
          <div style={{ fontSize: '0.74rem', color: 'var(--text-muted)' }}>
            {row.doctorSpecialization || 'General Medicine'}
          </div>
        </div>
      ),
    },
    {
      key: 'type',
      label: 'Type',
      render: (row) => (
        <Badge variant={getTypeVariant(row.recordType)}>
          {row.recordType}
        </Badge>
      ),
    },
    {
      key: 'diagnosis',
      label: 'Diagnosis & Plan',
      render: (row) => (
        <div className="mr-diagnosis-cell">
          <div className="mr-diagnosis-title">{row.diagnosis}</div>
          <div className="mr-diagnosis-sub">{row.treatmentPlan}</div>
        </div>
      ),
    },
    {
      key: 'files',
      label: 'Files',
      render: (row) => (
        row.attachments?.length > 0 ? (
          <span className="mr-attachment-pill">
            <Paperclip size={12} /> {row.attachments.length} {row.attachments.length === 1 ? 'file' : 'files'}
          </span>
        ) : (
          <span style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>—</span>
        )
      ),
    },
    {
      key: 'status',
      label: 'Status',
      render: (row) => (
        <Badge variant={row.status === 'Finalized' ? 'success' : row.status === 'Draft' ? 'warning' : 'default'} dot>
          {row.status}
        </Badge>
      ),
    },
    {
      key: 'actions',
      label: 'Actions',
      render: (row) => (
        <div className="mr-table-actions">
          <button
            type="button"
            className="mr-action-btn"
            onClick={() => setSelectedRecord(row)}
            title="View Full Medical Record"
          >
            <Eye size={15} />
          </button>
          <button
            type="button"
            className="mr-action-btn"
            onClick={() => handleOpenEdit(row)}
            title="Edit Medical Record"
          >
            <Pencil size={15} />
          </button>
          {user?.role === 'Admin' && (
            <button
              type="button"
              className="mr-action-btn mr-action-btn--danger"
              onClick={() => setDeleteTarget(row)}
              title="Delete Record"
            >
              <Trash2 size={15} />
            </button>
          )}
        </div>
      ),
    },
  ]

  return (
    <div className="page-wrapper mr-page">
      {/* Header */}
      <div className="page-header mr-header">
        <div className="mr-header__left">
          <h1 className="page-title">Medical Records Management</h1>
          <p className="page-subtitle">
            {totalCount} recorded {totalCount === 1 ? 'entry' : 'entries'} · Search, filter, and audit clinical patient health histories
          </p>
        </div>
        <div className="mr-header__actions">
          <Button variant="secondary" icon={RotateCcw} onClick={() => refetch()} id="refresh-records">
            Refresh
          </Button>
          <Button
            variant="primary"
            icon={Plus}
            onClick={() => {
              setEditingRecord(null)
              setIsFormOpen(true)
            }}
            id="new-record-btn"
          >
            New Medical Record
          </Button>
        </div>
      </div>

      {/* Summary KPI Cards */}
      <section className="mr-overview" aria-label="Medical records overview">
        <div className="mr-summary-card mr-summary-card--primary">
          <div className="mr-summary-icon">
            <ClipboardList size={22} />
          </div>
          <div className="mr-summary-info">
            <span className="mr-summary-label">Total Records</span>
            <span className="mr-summary-value">{summary?.totalRecords ?? totalCount}</span>
            <span className="mr-summary-sub">+{summary?.addedThisMonth ?? 0} this month</span>
          </div>
        </div>

        <div className="mr-summary-card mr-summary-card--blue">
          <div className="mr-summary-icon">
            <Stethoscope size={22} />
          </div>
          <div className="mr-summary-info">
            <span className="mr-summary-label">Consultations</span>
            <span className="mr-summary-value">{summary?.consultationCount ?? 0}</span>
            <span className="mr-summary-sub">Clinical visits & notes</span>
          </div>
        </div>

        <div className="mr-summary-card mr-summary-card--cyan">
          <div className="mr-summary-icon">
            <FlaskConical size={22} />
          </div>
          <div className="mr-summary-info">
            <span className="mr-summary-label">Lab Reports</span>
            <span className="mr-summary-value">{summary?.labReportCount ?? 0}</span>
            <span className="mr-summary-sub">Diagnostic findings</span>
          </div>
        </div>

        <div className="mr-summary-card mr-summary-card--success">
          <div className="mr-summary-icon">
            <CheckCircle2 size={22} />
          </div>
          <div className="mr-summary-info">
            <span className="mr-summary-label">Finalized</span>
            <span className="mr-summary-value">{summary?.finalizedCount ?? 0}</span>
            <span className="mr-summary-sub">{summary?.draftCount ?? 0} draft(s) pending</span>
          </div>
        </div>
      </section>

      {/* Filters Bar */}
      <div className="mr-filters-bar">
        {/* Search */}
        <div className="mr-search-wrap">
          <Search size={16} className="mr-search-icon" />
          <input
            id="mr-search-input"
            type="search"
            className="mr-search-input"
            placeholder="Search by diagnosis, symptoms, patient or NIC..."
            value={search}
            onChange={(e) => {
              setSearch(e.target.value)
              setPage(1)
            }}
          />
        </div>

        {/* Record Type Dropdown */}
        <select
          id="mr-filter-type"
          className="mr-select"
          value={recordType}
          onChange={(e) => {
            setRecordType(e.target.value)
            setPage(1)
          }}
        >
          <option value="">All Record Types</option>
          <option value="Consultation">Consultation</option>
          <option value="LabReport">Lab Report</option>
          <option value="DischargeSummary">Discharge Summary</option>
          <option value="Prescription">Prescription</option>
          <option value="GeneralNote">General Note</option>
        </select>

        {/* Status Dropdown */}
        <select
          id="mr-filter-status"
          className="mr-select"
          value={status}
          onChange={(e) => {
            setStatus(e.target.value)
            setPage(1)
          }}
        >
          <option value="">All Statuses</option>
          <option value="Finalized">Finalized</option>
          <option value="Draft">Draft</option>
          <option value="Archived">Archived</option>
        </select>

        {/* Date from/to */}
        <div className="mr-date-box">
          <Calendar size={14} color="var(--text-muted)" />
          <input
            type="date"
            className="mr-date-input"
            title="From Date"
            value={fromDate}
            onChange={(e) => {
              setFromDate(e.target.value)
              setPage(1)
            }}
          />
          <span className="mr-date-separator">to</span>
          <input
            type="date"
            className="mr-date-input"
            title="To Date"
            value={toDate}
            onChange={(e) => {
              setToDate(e.target.value)
              setPage(1)
            }}
          />
        </div>

        {/* Reset Filter Button */}
        {hasActiveFilters && (
          <button type="button" className="mr-reset-btn" onClick={handleResetFilters}>
            <X size={14} /> Clear Filters
          </button>
        )}
      </div>

      {/* Main Table Card */}
      <div className="mr-table-card">
        {error && <div className="patient-form__error mb-4">{error}</div>}

        <Table
          columns={columns}
          data={records}
          loading={loading}
          emptyText="No medical records found matching your filters."
        />

        {/* Pagination Controls */}
        {totalPages > 1 && (
          <div className="mr-pagination">
            <span className="mr-pagination-text">
              Showing {records.length} of {totalCount} records (Page {page} of {totalPages})
            </span>
            <div className="mr-pagination-controls">
              <Button
                size="sm"
                variant="secondary"
                disabled={page <= 1}
                onClick={() => setPage(page - 1)}
              >
                Previous
              </Button>
              <Button
                size="sm"
                variant="secondary"
                disabled={page >= totalPages}
                onClick={() => setPage(page + 1)}
              >
                Next
              </Button>
            </div>
          </div>
        )}
      </div>

      {/* Detail Modal (Read-Only) */}
      {selectedRecord && (
        <MedicalRecordDetailModal
          record={selectedRecord}
          onClose={() => setSelectedRecord(null)}
          onEdit={handleOpenEdit}
          canEdit={true}
        />
      )}

      {/* Create / Edit Form Modal */}
      <MedicalRecordFormModal
        open={isFormOpen}
        isOpen={isFormOpen}
        onClose={() => {
          setIsFormOpen(false)
          setEditingRecord(null)
        }}
        onSubmit={handleCreateOrUpdate}
        initialData={editingRecord}
        patients={patients}
        loading={saving}
      />

      {/* Delete Confirmation Modal */}
      {deleteTarget && (
        <Modal
          open={!!deleteTarget}
          onClose={() => setDeleteTarget(null)}
          title="Delete Medical Record"
          size="sm"
        >
          <div style={{ padding: '8px 0' }}>
            <p style={{ fontSize: '0.9rem', color: 'var(--text-primary)', marginBottom: '16px' }}>
              Are you sure you want to permanently delete Medical Record <strong>#{deleteTarget.medicalRecordId}</strong> for{' '}
              <strong>{deleteTarget.patientName}</strong>? This action cannot be undone.
            </p>
            <div style={{ display: 'flex', gap: '8px', justifyContent: 'flex-end' }}>
              <Button variant="secondary" onClick={() => setDeleteTarget(null)}>
                Cancel
              </Button>
              <Button variant="danger" icon={Trash2} onClick={handleDeleteConfirm}>
                Delete Record
              </Button>
            </div>
          </div>
        </Modal>
      )}
    </div>
  )
}
