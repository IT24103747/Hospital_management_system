import { useState, useEffect } from 'react'
import { Check, Clock, Plus, Search, ShieldAlert, ShieldCheck, UserCheck, X } from 'lucide-react'
import Button from '../../../components/Button'
import apiClient from '../../../lib/apiClient'
import { useAuth } from '../../../context/AuthContext'
import './DoctorsPage.css'

export default function DoctorsPage() {
  const { user } = useAuth()
  const isAdmin = user?.role === 'Admin' || !user?.role

  const [activeTab, setActiveTab] = useState('approved') // 'approved' or 'pending'
  const [doctors, setDoctors] = useState([])
  const [pendingRequests, setPendingRequests] = useState([])
  const [loading, setLoading] = useState(false)
  const [searchQuery, setSearchQuery] = useState('')
  const [actionLoadingId, setActionLoadingId] = useState(null)

  const fetchDoctors = async () => {
    setLoading(true)
    try {
      const [approvedRes, pendingRes] = await Promise.all([
        apiClient.get('/doctors?status=Approved'),
        apiClient.get('/doctors/pending'),
      ])
      setDoctors(approvedRes.data || [])
      setPendingRequests(pendingRes.data || [])
    } catch (err) {
      console.error('Failed to fetch doctors:', err)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    fetchDoctors()
  }, [])

  const handleApprove = async (doctorId) => {
    setActionLoadingId(doctorId)
    try {
      await apiClient.post(`/doctors/${doctorId}/approve`)
      await fetchDoctors()
    } catch (err) {
      alert('Failed to approve doctor request: ' + (err.response?.data?.message || err.message))
    } finally {
      setActionLoadingId(null)
    }
  }

  const handleDecline = async (doctorId) => {
    if (!window.confirm('Are you sure you want to decline this doctor registration request?')) {
      return
    }
    setActionLoadingId(doctorId)
    try {
      await apiClient.post(`/doctors/${doctorId}/decline`)
      await fetchDoctors()
    } catch (err) {
      alert('Failed to decline doctor request: ' + (err.response?.data?.message || err.message))
    } finally {
      setActionLoadingId(null)
    }
  }

  const filteredApproved = doctors.filter(
    (d) =>
      `${d.firstName} ${d.lastName}`.toLowerCase().includes(searchQuery.toLowerCase()) ||
      d.specialization.toLowerCase().includes(searchQuery.toLowerCase()) ||
      d.slmcLicenseNumber.toLowerCase().includes(searchQuery.toLowerCase())
  )

  const filteredPending = pendingRequests.filter(
    (d) =>
      `${d.firstName} ${d.lastName}`.toLowerCase().includes(searchQuery.toLowerCase()) ||
      d.specialization.toLowerCase().includes(searchQuery.toLowerCase()) ||
      d.slmcLicenseNumber.toLowerCase().includes(searchQuery.toLowerCase())
  )

  return (
    <div className="page-wrapper">
      <div className="page-header flex items-center justify-between">
        <div>
          <h1 className="page-title">Doctors & Medical Staff</h1>
          <p className="page-subtitle">Manage hospital doctor directory and verify registration applications</p>
        </div>
        {isAdmin && (
          <Button variant="primary" icon={Plus} id="add-doctor-btn">
            Add Doctor
          </Button>
        )}
      </div>

      {/* Tabs */}
      <div style={{ display: 'flex', gap: '12px', marginBottom: '24px', borderBottom: '1px solid var(--border-color, #e2e8f0)', paddingBottom: '12px' }}>
        <button
          type="button"
          onClick={() => setActiveTab('approved')}
          style={{
            padding: '8px 16px',
            borderRadius: '20px',
            fontWeight: 600,
            fontSize: '0.9rem',
            border: 'none',
            cursor: 'pointer',
            background: activeTab === 'approved' ? 'var(--clr-primary, #0ea5e9)' : 'transparent',
            color: activeTab === 'approved' ? '#ffffff' : 'var(--text-secondary, #64748b)',
          }}
          id="tab-approved-doctors"
        >
          Approved Doctors ({doctors.length})
        </button>

        {isAdmin && (
          <button
            type="button"
            onClick={() => setActiveTab('pending')}
            style={{
              padding: '8px 16px',
              borderRadius: '20px',
              fontWeight: 600,
              fontSize: '0.9rem',
              border: 'none',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: '6px',
              background: activeTab === 'pending' ? '#f59e0b' : 'transparent',
              color: activeTab === 'pending' ? '#ffffff' : 'var(--text-secondary, #64748b)',
            }}
            id="tab-pending-doctors"
          >
            Pending Approvals
            {pendingRequests.length > 0 && (
              <span
                style={{
                  background: activeTab === 'pending' ? '#ffffff' : '#f59e0b',
                  color: activeTab === 'pending' ? '#f59e0b' : '#ffffff',
                  borderRadius: '10px',
                  padding: '2px 8px',
                  fontSize: '0.75rem',
                  fontWeight: 700,
                }}
              >
                {pendingRequests.length}
              </span>
            )}
          </button>
        )}
      </div>

      {/* Search Input */}
      <div className="doctors__search-wrap" style={{ marginBottom: '28px' }}>
        <Search size={15} style={{ color: 'var(--text-muted)' }} />
        <input
          className="patients__search"
          placeholder="Search by name, specialization, or SLMC License..."
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
          id="doctor-search"
        />
      </div>

      {/* Pending Approvals Tab View */}
      {activeTab === 'pending' && isAdmin && (
        <div>
          {filteredPending.length === 0 ? (
            <div className="glass-card" style={{ padding: '40px', textAlign: 'center', color: 'var(--text-muted)' }}>
              <Clock size={40} style={{ margin: '0 auto 12px', opacity: 0.5 }} />
              <h3>No Pending Doctor Requests</h3>
              <p style={{ fontSize: '0.9rem', marginTop: '4px' }}>All doctor registration applications have been processed.</p>
            </div>
          ) : (
            <div style={{ display: 'grid', gap: '16px' }}>
              {filteredPending.map((doc) => (
                <div
                  key={doc.doctorId}
                  className="glass-card animate-fade-in"
                  style={{
                    padding: '20px 24px',
                    borderRadius: '14px',
                    display: 'flex',
                    flexWrap: 'wrap',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    gap: '16px',
                    borderLeft: '4px solid #f59e0b',
                  }}
                  id={`pending-doctor-${doc.doctorId}`}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: '16px', minWidth: '260px' }}>
                    <div
                      style={{
                        width: '52px',
                        height: '52px',
                        borderRadius: '50%',
                        background: 'linear-gradient(135deg, #f59e0b, #ec4899)',
                        color: '#fff',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        fontWeight: '700',
                        fontSize: '1.1rem',
                      }}
                    >
                      Dr
                    </div>
                    <div>
                      <h3 style={{ fontSize: '1.1rem', fontWeight: '700', margin: 0 }}>
                        Dr. {doc.firstName} {doc.lastName}
                      </h3>
                      <p style={{ color: 'var(--clr-primary)', fontWeight: '600', fontSize: '0.88rem', margin: '2px 0 0' }}>
                        {doc.specialization}
                      </p>
                    </div>
                  </div>

                  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(140px, 1fr))', gap: '12px', flex: 1 }}>
                    <div>
                      <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)', display: 'block' }}>Email</span>
                      <strong style={{ fontSize: '0.86rem' }}>{doc.email}</strong>
                    </div>
                    <div>
                      <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)', display: 'block' }}>NIC (SL)</span>
                      <strong style={{ fontSize: '0.86rem' }}>{doc.nic}</strong>
                    </div>
                    <div>
                      <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)', display: 'block' }}>SLMC License</span>
                      <strong style={{ fontSize: '0.86rem', color: '#6366f1' }}>{doc.slmcLicenseNumber}</strong>
                    </div>
                    <div>
                      <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)', display: 'block' }}>Phone</span>
                      <strong style={{ fontSize: '0.86rem' }}>{doc.phoneNumber}</strong>
                    </div>
                  </div>

                  <div style={{ display: 'flex', gap: '10px' }}>
                    <Button
                      variant="success"
                      size="sm"
                      onClick={() => handleApprove(doc.doctorId)}
                      loading={actionLoadingId === doc.doctorId}
                      id={`approve-btn-${doc.doctorId}`}
                    >
                      <Check size={16} style={{ marginRight: '4px' }} /> Approve
                    </Button>
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => handleDecline(doc.doctorId)}
                      loading={actionLoadingId === doc.doctorId}
                      style={{ color: '#ef4444', borderColor: 'rgba(239, 68, 68, 0.4)' }}
                      id={`decline-btn-${doc.doctorId}`}
                    >
                      <X size={16} style={{ marginRight: '4px' }} /> Decline
                    </Button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* Approved Doctors View */}
      {activeTab === 'approved' && (
        <div className="doctors__grid stagger-children animate-fade-in">
          {filteredApproved.length === 0 ? (
            <div className="glass-card" style={{ gridColumn: '1 / -1', padding: '40px', textAlign: 'center', color: 'var(--text-muted)' }}>
              <UserCheck size={40} style={{ margin: '0 auto 12px', opacity: 0.5 }} />
              <h3>No Approved Doctors Found</h3>
              <p style={{ fontSize: '0.9rem', marginTop: '4px' }}>Approve pending doctor requests to list them in the directory.</p>
            </div>
          ) : (
            filteredApproved.map((doc) => (
              <div key={doc.doctorId} className="doctor-card glass-card" id={`doctor-${doc.doctorId}`}>
                <div className="doctor-card__top">
                  <div
                    className="doctor-card__avatar"
                    style={{ background: 'linear-gradient(135deg, #0ea5e9, #6366f1)' }}
                  >
                    {doc.firstName[0]}
                    {doc.lastName[0]}
                  </div>
                  <span className="doctor-card__status doctor-card__status--available">
                    <span className="doctor-card__dot" />
                    Approved
                  </span>
                </div>

                <h4 className="doctor-card__name">
                  Dr. {doc.firstName} {doc.lastName}
                </h4>
                <p className="doctor-card__specialty">{doc.specialization}</p>

                <div style={{ fontSize: '0.82rem', color: 'var(--text-secondary)', marginBottom: '14px', lineHeight: '1.6' }}>
                  <div><strong>SLMC:</strong> {doc.slmcLicenseNumber}</div>
                  <div><strong>NIC:</strong> {doc.nic}</div>
                  <div><strong>Phone:</strong> {doc.phoneNumber}</div>
                </div>

                <Button variant="outline" size="sm" fullWidth id={`book-${doc.doctorId}`}>
                  Book Appointment
                </Button>
              </div>
            ))
          )}
        </div>
      )}
    </div>
  )
}
