import { useState, useMemo } from 'react'
import { Plus, Search, LayoutGrid, List, RefreshCw, Users, Trash2, AlertTriangle } from 'lucide-react'
import { usePatients } from '../hooks/usePatients'
import PatientCard from '../components/PatientCard'
import PatientForm from '../components/PatientForm'
import Table from '../../../components/Table'
import Button from '../../../components/Button'
import Modal from '../../../components/Modal'
import { BloodGroupBadge } from '../../../components/Badge'
import { calculateAge, formatDate, getInitials, nameToGradient } from '../../../lib/utils'
import './PatientListPage.css'

export default function PatientListPage() {
  const { patients, loading, error, refetch, createPatient, updatePatient, deletePatient } = usePatients()

  const [search, setSearch] = useState('')
  const [view, setView] = useState('grid')
  const [filterGender, setFilterGender] = useState('all')
  const [filterBlood, setFilterBlood] = useState('all')

  const [formOpen, setFormOpen] = useState(false)
  const [editPatient, setEditPatient] = useState(null)
  const [deleteTarget, setDeleteTarget] = useState(null)
  const [saving, setSaving] = useState(false)
  const [deleting, setDeleting] = useState(false)

  const filtered = useMemo(() => {
    return patients.filter(p => {
      const term = search.toLowerCase()
      const matchSearch = !term || (
        `${p.firstName} ${p.lastName}`.toLowerCase().includes(term) ||
        p.nic?.toLowerCase().includes(term) ||
        p.email?.toLowerCase().includes(term) ||
        p.phoneNumber?.includes(term)
      )
      const matchGender = filterGender === 'all' || p.gender === filterGender
      const matchBlood = filterBlood === 'all' || p.bloodGroup === filterBlood
      return matchSearch && matchGender && matchBlood
    })
  }, [patients, search, filterGender, filterBlood])

  const handleOpenAdd = () => { setEditPatient(null); setFormOpen(true) }
  const handleOpenEdit = (p) => { setEditPatient(p); setFormOpen(true) }
  const handleCloseForm = () => { setFormOpen(false); setEditPatient(null) }

  const handleSubmit = async (data) => {
    setSaving(true)
    try {
      if (editPatient) {
        await updatePatient(editPatient.patientId, data)
      } else {
        await createPatient(data)
      }
      setFormOpen(false)
      setEditPatient(null)
    } finally {
      setSaving(false)
    }
  }

  const handleDelete = async () => {
    if (!deleteTarget) return
    setDeleting(true)
    try {
      await deletePatient(deleteTarget.patientId)
      setDeleteTarget(null)
    } finally {
      setDeleting(false)
    }
  }

  const columns = [
    {
      key: 'patient',
      label: 'Patient',
      render: (p) => {
        const [c1, c2] = nameToGradient(p.firstName)
        return (
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <div style={{
              width: '34px', height: '34px', borderRadius: '8px', flexShrink: 0,
              background: `linear-gradient(135deg, ${c1}, ${c2})`,
              display: 'flex', alignItems: 'center', justifyContent: 'center',
              fontWeight: 700, fontSize: '0.8rem', color: 'white',
            }}>
              {getInitials(p.firstName, p.lastName)}
            </div>
            <div>
              <div style={{ fontWeight: 600, color: 'var(--text-primary)', fontSize: '0.88rem' }}>
                {p.firstName} {p.lastName}
              </div>
              <div style={{ fontSize: '0.74rem', color: 'var(--text-muted)' }}>{p.nic}</div>
            </div>
          </div>
        )
      }
    },
    { key: 'gender', label: 'Gender', render: (p) => p.gender },
    { key: 'age', label: 'Age', render: (p) => `${calculateAge(p.dateOfBirth)} yrs` },
    { key: 'bloodGroup', label: 'Blood', render: (p) => <BloodGroupBadge group={p.bloodGroup} /> },
    { key: 'phoneNumber', label: 'Phone', render: (p) => p.phoneNumber },
    { key: 'email', label: 'Email', render: (p) => p.email },
    {
      key: 'createdAt',
      label: 'Registered',
      render: (p) => formatDate(p.createdAt),
    },
    {
      key: 'actions',
      label: '',
      align: 'right',
      render: (p) => (
        <div style={{ display: 'flex', gap: '8px', justifyContent: 'flex-end' }}>
          <Button variant="ghost" size="sm" onClick={() => handleOpenEdit(p)} id={`table-edit-${p.patientId}`}>Edit</Button>
          <Button variant="danger" size="sm" onClick={() => setDeleteTarget(p)} id={`table-delete-${p.patientId}`}>Delete</Button>
        </div>
      ),
    },
  ]

  return (
    <div className="page-wrapper">
      <div className="page-header">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="page-title">Patient Records</h1>
            <p className="page-subtitle">
              {patients.length} total patients · {filtered.length} shown
            </p>
          </div>
          <div style={{ display: 'flex', gap: '10px' }}>
            <Button variant="secondary" icon={RefreshCw} onClick={refetch} id="refresh-patients">
              Refresh
            </Button>
            <Button variant="primary" icon={Plus} onClick={handleOpenAdd} id="add-patient-btn">
              Add Patient
            </Button>
          </div>
        </div>
      </div>

      <div className="patients__filters">
        <div className="patients__search-wrap">
          <Search size={15} className="patients__search-icon" />
          <input
            id="patient-search"
            className="patients__search"
            type="search"
            placeholder="Search by name, NIC, email…"
            value={search}
            onChange={e => setSearch(e.target.value)}
          />
        </div>

        <select
          id="filter-gender"
          className="patients__select"
          value={filterGender}
          onChange={e => setFilterGender(e.target.value)}
        >
          <option value="all">All Genders</option>
          <option value="Male">Male</option>
          <option value="Female">Female</option>
          <option value="Other">Other</option>
        </select>

        <select
          id="filter-blood"
          className="patients__select"
          value={filterBlood}
          onChange={e => setFilterBlood(e.target.value)}
        >
          <option value="all">All Blood Groups</option>
          {['A+','A-','B+','B-','AB+','AB-','O+','O-'].map(b => (
            <option key={b} value={b}>{b}</option>
          ))}
        </select>

        <div className="patients__view-toggle">
          <button
            id="view-grid"
            className={`patients__view-btn ${view === 'grid' ? 'patients__view-btn--active' : ''}`}
            onClick={() => setView('grid')}
            aria-label="Grid view"
          >
            <LayoutGrid size={16} />
          </button>
          <button
            id="view-list"
            className={`patients__view-btn ${view === 'list' ? 'patients__view-btn--active' : ''}`}
            onClick={() => setView('list')}
            aria-label="List view"
          >
            <List size={16} />
          </button>
        </div>
      </div>

      {error && (
        <div className="patients__error">
          <AlertTriangle size={16} />
          <span>{error} – showing demo data</span>
        </div>
      )}

      {view === 'grid' && (
        <div className="patients__grid stagger-children animate-fade-in">
          {loading
            ? Array.from({ length: 6 }).map((_, i) => (
                <div key={i} className="skeleton" style={{ height: '220px', borderRadius: '16px' }} />
              ))
            : filtered.length === 0
              ? (
                <div className="patients__empty">
                  <Users size={40} strokeWidth={1.5} />
                  <p>No patients match your filters</p>
                </div>
              )
              : filtered.map(p => (
                  <PatientCard
                    key={p.patientId}
                    patient={p}
                    onEdit={handleOpenEdit}
                    onDelete={setDeleteTarget}
                  />
                ))
          }
        </div>
      )}

      {view === 'list' && (
        <div className="animate-fade-in">
          <Table
            columns={columns}
            data={filtered}
            loading={loading}
            emptyMessage="No patients match your search"
          />
        </div>
      )}

      <PatientForm
        open={formOpen}
        onClose={handleCloseForm}
        onSubmit={handleSubmit}
        patient={editPatient}
        loading={saving}
      />

      <Modal
        open={Boolean(deleteTarget)}
        onClose={() => setDeleteTarget(null)}
        title="Delete Patient"
        subtitle="This action cannot be undone"
        size="sm"
        id="delete-confirm-modal"
      >
        <div className="delete-confirm">
          <div className="delete-confirm__icon">
            <Trash2 size={28} />
          </div>
          <p className="delete-confirm__msg">
            Are you sure you want to delete{' '}
            <strong>{deleteTarget?.firstName} {deleteTarget?.lastName}</strong>?
            All their records will be permanently removed.
          </p>
          <div className="delete-confirm__actions">
            <Button variant="secondary" onClick={() => setDeleteTarget(null)} id="cancel-delete">
              Cancel
            </Button>
            <Button variant="danger" onClick={handleDelete} loading={deleting} id="confirm-delete">
              Delete Patient
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  )
}
