import { useState, useEffect, useMemo } from 'react'
import { Search, RefreshCw, Stethoscope, AlertTriangle } from 'lucide-react'
import Button from '../../../components/Button'
import DoctorCard from '../components/DoctorCard'
import { approveDoctor, declineDoctor, getDoctorRegistrations } from '../services/doctorApi'
import './DoctorsPage.css'

export default function DoctorsPage() {
  const [doctors, setDoctors] = useState([])
  const [status, setStatus] = useState('Pending')
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const load = async () => {
    setLoading(true)
    setError('')
    try {
      setDoctors(await getDoctorRegistrations(status))
    } catch (requestError) {
      setError(requestError.response?.data?.message || 'Unable to load doctor requests.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    load()
  }, [status])

  const visible = useMemo(() => {
    const term = search.toLowerCase().trim()
    if (!term) return doctors
    return doctors.filter(d =>
      `${d.fullName} ${d.email} ${d.specialization} ${d.slmcLicenseNumber} ${d.nic}`
        .toLowerCase()
        .includes(term)
    )
  }, [doctors, search])

  const review = async (doctor, approve) => {
    const reason = approve ? '' : window.prompt('Optional reason for declining this registration:', '')
    if (!approve && reason === null) return
    try {
      if (approve) {
        await approveDoctor(doctor.doctorId)
      } else {
        await declineDoctor(doctor.doctorId, reason)
      }
      await load()
    } catch (requestError) {
      setError(requestError.response?.data?.message || 'Unable to review request.')
    }
  }

  return (
    <div className="page-wrapper">
      <div className="page-header">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="page-title">Doctor Management</h1>
            <p className="page-subtitle">
              Review doctor registrations and manage approved medical staff
            </p>
          </div>
          <Button variant="secondary" icon={RefreshCw} onClick={load} id="refresh-doctors">
            Refresh
          </Button>
        </div>
      </div>

      <div className="doctor-toolbar">
        <div className="doctors__search-wrap">
          <Search size={15} className="doctors__search-icon" />
          <input
            id="doctor-search"
            className="patients__search"
            value={search}
            onChange={e => setSearch(e.target.value)}
            placeholder="Search by doctor name, specialization, SLMC..."
          />
        </div>

        <div className="doctor-tabs">
          {['Pending', 'Approved', 'Declined'].map(value => (
            <button
              key={value}
              id={`tab-${value.toLowerCase()}`}
              onClick={() => setStatus(value)}
              className={status === value ? 'active' : ''}
            >
              {value}
            </button>
          ))}
        </div>
      </div>

      {error && (
        <div className="patients__error" style={{ marginBottom: '20px' }}>
          <AlertTriangle size={16} />
          <span>{error}</span>
        </div>
      )}

      {loading ? (
        <div className="doctor-request-grid">
          {Array.from({ length: 6 }).map((_, i) => (
            <div key={i} className="skeleton" style={{ height: '280px', borderRadius: '18px' }} />
          ))}
        </div>
      ) : visible.length === 0 ? (
        <div className="doctor-empty">
          <Stethoscope size={40} strokeWidth={1.5} style={{ margin: '0 auto 12px auto', color: 'var(--text-muted)' }} />
          <p>No {status.toLowerCase()} doctor registrations found.</p>
        </div>
      ) : (
        <div className="doctor-request-grid stagger-children animate-fade-in">
          {visible.map(doctor => (
            <DoctorCard
              key={doctor.doctorId}
              doctor={doctor}
              onReview={review}
            />
          ))}
        </div>
      )}
    </div>
  )
}
