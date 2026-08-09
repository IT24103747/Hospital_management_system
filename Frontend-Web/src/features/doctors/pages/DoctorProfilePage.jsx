import { useState, useEffect } from 'react'
import { AlertCircle, CheckCircle, FileText, Mail, Phone, ShieldCheck, Stethoscope, User } from 'lucide-react'
import Button from '../../../components/Button'
import { useAuth } from '../../../context/AuthContext'
import apiClient from '../../../lib/apiClient'

export default function DoctorProfilePage() {
  const { user, updateDoctorProfile } = useAuth()
  const doc = user?.doctorProfile || {}

  const [form, setForm] = useState({
    firstName: doc.firstName || user?.fullName?.split(' ')[0] || '',
    lastName: doc.lastName || user?.fullName?.split(' ').slice(1).join(' ') || '',
    phoneNumber: doc.phoneNumber || '',
  })

  const [loading, setLoading] = useState(false)
  const [success, setSuccess] = useState('')
  const [error, setError] = useState('')

  useEffect(() => {
    if (user?.doctorProfile) {
      setForm({
        firstName: user.doctorProfile.firstName || '',
        lastName: user.doctorProfile.lastName || '',
        phoneNumber: user.doctorProfile.phoneNumber || '',
      })
    }
  }, [user])

  const handleChange = (e) => {
    setError('')
    setSuccess('')
    setForm((prev) => ({ ...prev, [e.target.name]: e.target.value }))
  }

  const handleSubmit = async (e) => {
    e.preventDefault()
    setError('')
    setSuccess('')

    if (!form.firstName.trim() || !form.lastName.trim()) {
      setError('First name and second name are required.')
      return
    }

    const cleanedPhone = form.phoneNumber.trim().replace(/[\s-]/g, '')
    const phoneRegex = /^(?:\+94|94|0)?[0-9]{9}$/
    if (!phoneRegex.test(cleanedPhone)) {
      setError('Invalid Sri Lankan phone number format. (e.g. 0771234567)')
      return
    }

    setLoading(true)
    try {
      if (doc.doctorId) {
        await apiClient.put(`/doctors/${doc.doctorId}/profile`, {
          firstName: form.firstName.trim(),
          lastName: form.lastName.trim(),
          phoneNumber: cleanedPhone,
        })
      }

      updateDoctorProfile({
        firstName: form.firstName.trim(),
        lastName: form.lastName.trim(),
        phoneNumber: cleanedPhone,
      })

      setLoading(false)
      setSuccess('Profile updated successfully!')
    } catch (err) {
      setLoading(false)
      const msg = err.response?.data?.message || err.message || 'Failed to update profile.'
      setError(msg)
    }
  }

  return (
    <div className="page-wrapper" style={{ maxWidth: '850px', margin: '0 auto' }}>
      <div className="page-header" style={{ marginBottom: '24px' }}>
        <h1 className="page-title">My Doctor Profile</h1>
        <p className="page-subtitle">View and update your personal and professional profile details</p>
      </div>

      <div className="glass-card" style={{ padding: '28px', borderRadius: '16px' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '20px', marginBottom: '28px', paddingBottom: '20px', borderBottom: '1px solid var(--border-color, #e2e8f0)' }}>
          <div
            style={{
              width: '72px',
              height: '72px',
              borderRadius: '50%',
              background: 'linear-gradient(135deg, #0ea5e9, #6366f1)',
              color: '#fff',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              fontSize: '1.8rem',
              fontWeight: '700',
              boxShadow: '0 4px 14px rgba(14, 165, 233, 0.3)',
            }}
          >
            Dr
          </div>
          <div>
            <h2 style={{ fontSize: '1.4rem', fontWeight: '700', margin: 0, color: 'var(--text-main)' }}>
              Dr. {form.firstName} {form.lastName}
            </h2>
            <p style={{ margin: '4px 0 0', color: 'var(--clr-primary)', fontWeight: '600', fontSize: '0.92rem' }}>
              {doc.specialization || 'Medical Specialist'}
            </p>
            <div style={{ display: 'inline-flex', alignItems: 'center', gap: '6px', marginTop: '6px', background: 'rgba(16,185,129,0.12)', color: '#10b981', padding: '2px 10px', borderRadius: '20px', fontSize: '0.78rem', fontWeight: 600 }}>
              <ShieldCheck size={14} />
              SLMC Verified Doctor (Status: {doc.status || 'Approved'})
            </div>
          </div>
        </div>

        {error && (
          <div style={{ background: 'rgba(239,68,68,0.1)', border: '1px solid rgba(239,68,68,0.3)', color: '#ef4444', borderRadius: '8px', padding: '10px 14px', fontSize: '0.86rem', display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '20px' }}>
            <AlertCircle size={18} style={{ flexShrink: 0 }} />
            <span>{error}</span>
          </div>
        )}

        {success && (
          <div style={{ background: 'rgba(16,185,129,0.1)', border: '1px solid rgba(16,185,129,0.3)', color: '#10b981', borderRadius: '8px', padding: '10px 14px', fontSize: '0.86rem', display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '20px' }}>
            <CheckCircle size={18} style={{ flexShrink: 0 }} />
            <span>{success}</span>
          </div>
        )}

        <form onSubmit={handleSubmit}>
          <h3 style={{ fontSize: '1.05rem', fontWeight: 600, marginBottom: '16px', color: 'var(--text-main)' }}>
            Editable Information
          </h3>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px', marginBottom: '20px' }}>
            <div className="login-field">
              <label className="login-field__label">First Name</label>
              <div className="login-field__wrap">
                <User size={16} className="login-field__icon" />
                <input
                  type="text"
                  name="firstName"
                  className="login-field__input"
                  value={form.firstName}
                  onChange={handleChange}
                  required
                />
              </div>
            </div>

            <div className="login-field">
              <label className="login-field__label">Second Name (Last Name)</label>
              <div className="login-field__wrap">
                <User size={16} className="login-field__icon" />
                <input
                  type="text"
                  name="lastName"
                  className="login-field__input"
                  value={form.lastName}
                  onChange={handleChange}
                  required
                />
              </div>
            </div>
          </div>

          <div className="login-field" style={{ marginBottom: '28px' }}>
            <label className="login-field__label">Phone Number (Sri Lankan Format)</label>
            <div className="login-field__wrap">
              <Phone size={16} className="login-field__icon" />
              <input
                type="tel"
                name="phoneNumber"
                className="login-field__input"
                value={form.phoneNumber}
                onChange={handleChange}
                placeholder="0771234567"
                required
              />
            </div>
          </div>

          <h3 style={{ fontSize: '1.05rem', fontWeight: 600, marginBottom: '16px', color: 'var(--text-muted)' }}>
            Read-Only / Non-Editable Details (Verified Credentials)
          </h3>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px', marginBottom: '24px' }}>
            <div className="login-field">
              <label className="login-field__label" style={{ opacity: 0.7 }}>
                NIC Number (Sri Lanka)
              </label>
              <div className="login-field__wrap" style={{ opacity: 0.7, cursor: 'not-allowed' }}>
                <FileText size={16} className="login-field__icon" />
                <input
                  type="text"
                  className="login-field__input"
                  value={doc.nic || 'N/A'}
                  disabled
                  style={{ cursor: 'not-allowed', background: 'var(--bg-hover, #f8fafc)' }}
                />
              </div>
            </div>

            <div className="login-field">
              <label className="login-field__label" style={{ opacity: 0.7 }}>
                Official Email Address
              </label>
              <div className="login-field__wrap" style={{ opacity: 0.7, cursor: 'not-allowed' }}>
                <Mail size={16} className="login-field__icon" />
                <input
                  type="email"
                  className="login-field__input"
                  value={doc.email || user?.email || 'N/A'}
                  disabled
                  style={{ cursor: 'not-allowed', background: 'var(--bg-hover, #f8fafc)' }}
                />
              </div>
            </div>

            <div className="login-field">
              <label className="login-field__label" style={{ opacity: 0.7 }}>
                SLMC License Number
              </label>
              <div className="login-field__wrap" style={{ opacity: 0.7, cursor: 'not-allowed' }}>
                <FileText size={16} className="login-field__icon" />
                <input
                  type="text"
                  className="login-field__input"
                  value={doc.slmcLicenseNumber || 'N/A'}
                  disabled
                  style={{ cursor: 'not-allowed', background: 'var(--bg-hover, #f8fafc)' }}
                />
              </div>
            </div>

            <div className="login-field">
              <label className="login-field__label" style={{ opacity: 0.7 }}>
                Specialization
              </label>
              <div className="login-field__wrap" style={{ opacity: 0.7, cursor: 'not-allowed' }}>
                <Stethoscope size={16} className="login-field__icon" />
                <input
                  type="text"
                  className="login-field__input"
                  value={doc.specialization || 'N/A'}
                  disabled
                  style={{ cursor: 'not-allowed', background: 'var(--bg-hover, #f8fafc)' }}
                />
              </div>
            </div>
          </div>

          <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
            <Button variant="primary" type="submit" loading={loading} id="save-profile-btn">
              Save Profile Changes
            </Button>
          </div>
        </form>
      </div>
    </div>
  )
}
