import { Activity, Eye, EyeOff, FileText, Lock, Mail, Phone, Shield, Stethoscope, User, AlertCircle } from 'lucide-react'
import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import Button from '../../../components/Button'
import apiClient from '../../../lib/apiClient'
import './LoginPage.css'

export default function RegisterPage() {
  const navigate = useNavigate()
  const [showPass, setShowPass] = useState(false)
  const [loading, setLoading] = useState(false)
  const [submitted, setSubmitted] = useState(false)
  const [error, setError] = useState('')

  const [form, setForm] = useState({
    firstName: '',
    lastName: '',
    email: '',
    nic: '',
    specialization: '',
    slmcLicenseNumber: '',
    phoneNumber: '',
    password: '',
    confirmPassword: '',
  })

  const handleChange = (e) => {
    setError('')
    setForm((p) => ({ ...p, [e.target.name]: e.target.value }))
  }

  const validateForm = () => {
    // 1. First & Second Name
    if (!form.firstName.trim() || !form.lastName.trim()) {
      return 'First name and second name are required.'
    }

    // 2. Email
    if (!form.email.trim() || !/\S+@\S+\.\S+/.test(form.email)) {
      return 'Please enter a valid email address.'
    }

    // 3. Sri Lanka NIC format (Old: 9 digits + V/X, New: 12 digits)
    const cleanedNic = form.nic.trim()
    const nicRegex = /^([0-9]{9}[vVxX]|[0-9]{12})$/
    if (!nicRegex.test(cleanedNic)) {
      return 'Invalid Sri Lankan NIC number. (e.g. 941234567V or 199412345678)'
    }

    // 4. Specialization & SLMC
    if (!form.specialization.trim()) {
      return 'Specialization is required.'
    }
    if (!form.slmcLicenseNumber.trim()) {
      return 'SLMC License Number is required.'
    }

    // 5. Sri Lanka Phone number format
    const cleanedPhone = form.phoneNumber.trim().replace(/[\s-]/g, '')
    const phoneRegex = /^(?:\+94|94|0)(?:7[01245678]\d{7}|[1-9]\d{8})$/
    if (!phoneRegex.test(cleanedPhone)) {
      return 'Invalid Sri Lankan phone number format. Must be a valid Sri Lankan mobile/landline number (e.g. 0771234567 or +94771234567).'
    }

    // 6. Password (8+ characters, letters and numbers)
    if (form.password.length < 8) {
      return 'Password must be at least 8 characters long.'
    }
    if (!/[a-zA-Z]/.test(form.password) || !/[0-9]/.test(form.password)) {
      return 'Password must contain both letters and numbers.'
    }

    // 7. Confirm password
    if (form.password !== form.confirmPassword) {
      return 'Password and confirm password do not match.'
    }

    return null
  }

  const handleSubmit = async (e) => {
    e.preventDefault()
    setError('')

    const valErr = validateForm()
    if (valErr) {
      setError(valErr)
      return
    }

    setLoading(true)
    try {
      await apiClient.post('/doctors/register', {
        firstName: form.firstName.trim(),
        lastName: form.lastName.trim(),
        email: form.email.trim(),
        nic: form.nic.trim(),
        specialization: form.specialization.trim(),
        slmcLicenseNumber: form.slmcLicenseNumber.trim(),
        phoneNumber: form.phoneNumber.trim(),
        password: form.password,
        confirmPassword: form.confirmPassword,
      })

      setLoading(false)
      setSubmitted(true)
    } catch (err) {
      setLoading(false)
      const msg = err.response?.data?.message || err.message || 'Registration failed.'
      setError(msg)
    }
  }

  return (
    <div className="login-page">
      <div className="login-page__blob login-page__blob--1" />
      <div className="login-page__blob login-page__blob--2" />
      <div className="login-page__blob login-page__blob--3" />

      <div className="login-card glass-card animate-fade-in" style={{ maxWidth: '520px', width: '100%' }}>
        <div className="login-card__brand">
          <div className="login-card__logo">
            <Activity size={26} strokeWidth={2.5} />
          </div>
          <h1 className="login-card__app-name">
            Medi<span>Core</span>
          </h1>
        </div>

        {submitted ? (
          <div style={{ textAlign: 'center', padding: '20px 0' }}>
            <div
              style={{
                width: '64px',
                height: '64px',
                borderRadius: '50%',
                background: 'rgba(16, 185, 129, 0.15)',
                color: 'var(--clr-success)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                margin: '0 auto 16px',
              }}
            >
              <Shield size={32} />
            </div>
            <h2 className="login-card__title">Registration Request Submitted!</h2>
            <p className="login-card__sub" style={{ marginTop: '12px', lineHeight: '1.6', fontSize: '0.95rem' }}>
              Your Doctor registration request has been submitted. An <strong>Administrator</strong> will review your details and SLMC License for approval before you can log in.
            </p>
            <div style={{ marginTop: '28px' }}>
              <Button variant="primary" fullWidth onClick={() => navigate('/login')} id="back-to-login-btn">
                Return to Sign In
              </Button>
            </div>
          </div>
        ) : (
          <>
            <div className="login-card__header">
              <h2 className="login-card__title">Doctor Registration</h2>
              <p className="login-card__sub">Apply for a Doctor account (Admin Approval Required)</p>
            </div>

            {error && (
              <div
                style={{
                  background: 'rgba(239, 68, 68, 0.1)',
                  border: '1px solid rgba(239, 68, 68, 0.3)',
                  color: '#ef4444',
                  borderRadius: '8px',
                  padding: '10px 14px',
                  fontSize: '0.85rem',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '8px',
                  marginBottom: '16px',
                }}
              >
                <AlertCircle size={16} style={{ flexShrink: 0 }} />
                <span>{error}</span>
              </div>
            )}

            <form onSubmit={handleSubmit} className="login-form" id="register-form">
              {/* First Name & Second Name */}
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div className="login-field">
                  <label className="login-field__label" htmlFor="register-first-name">
                    First Name
                  </label>
                  <div className="login-field__wrap">
                    <User size={16} className="login-field__icon" />
                    <input
                      id="register-first-name"
                      type="text"
                      name="firstName"
                      className="login-field__input"
                      placeholder="Priyantha"
                      value={form.firstName}
                      onChange={handleChange}
                      required
                    />
                  </div>
                </div>

                <div className="login-field">
                  <label className="login-field__label" htmlFor="register-last-name">
                    Second Name
                  </label>
                  <div className="login-field__wrap">
                    <User size={16} className="login-field__icon" />
                    <input
                      id="register-last-name"
                      type="text"
                      name="lastName"
                      className="login-field__input"
                      placeholder="Jayawardena"
                      value={form.lastName}
                      onChange={handleChange}
                      required
                    />
                  </div>
                </div>
              </div>

              {/* Email */}
              <div className="login-field">
                <label className="login-field__label" htmlFor="register-email">
                  Email Address
                </label>
                <div className="login-field__wrap">
                  <Mail size={16} className="login-field__icon" />
                  <input
                    id="register-email"
                    type="email"
                    name="email"
                    className="login-field__input"
                    placeholder="dr.priyantha@medicore.lk"
                    value={form.email}
                    onChange={handleChange}
                    required
                  />
                </div>
              </div>

              {/* NIC & Phone */}
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div className="login-field">
                  <label className="login-field__label" htmlFor="register-nic">
                    NIC Number (SL)
                  </label>
                  <div className="login-field__wrap">
                    <FileText size={16} className="login-field__icon" />
                    <input
                      id="register-nic"
                      type="text"
                      name="nic"
                      className="login-field__input"
                      placeholder="941234567V / 1994..."
                      value={form.nic}
                      onChange={handleChange}
                      required
                    />
                  </div>
                </div>

                <div className="login-field">
                  <label className="login-field__label" htmlFor="register-phone">
                    Phone Number (SL)
                  </label>
                  <div className="login-field__wrap">
                    <Phone size={16} className="login-field__icon" />
                    <input
                      id="register-phone"
                      type="tel"
                      name="phoneNumber"
                      className="login-field__input"
                      placeholder="0771234567"
                      value={form.phoneNumber}
                      onChange={handleChange}
                      required
                    />
                  </div>
                </div>
              </div>

              {/* Specialization & SLMC */}
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div className="login-field">
                  <label className="login-field__label" htmlFor="register-specialization">
                    Specialization
                  </label>
                  <div className="login-field__wrap">
                    <Stethoscope size={16} className="login-field__icon" />
                    <input
                      id="register-specialization"
                      type="text"
                      name="specialization"
                      className="login-field__input"
                      placeholder="Cardiologist"
                      value={form.specialization}
                      onChange={handleChange}
                      required
                    />
                  </div>
                </div>

                <div className="login-field">
                  <label className="login-field__label" htmlFor="register-slmc">
                    SLMC License
                  </label>
                  <div className="login-field__wrap">
                    <FileText size={16} className="login-field__icon" />
                    <input
                      id="register-slmc"
                      type="text"
                      name="slmcLicenseNumber"
                      className="login-field__input"
                      placeholder="SLMC-89412"
                      value={form.slmcLicenseNumber}
                      onChange={handleChange}
                      required
                    />
                  </div>
                </div>
              </div>

              {/* Password & Confirm Password */}
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div className="login-field">
                  <label className="login-field__label" htmlFor="register-password">
                    Password (8+ chars)
                  </label>
                  <div className="login-field__wrap">
                    <Lock size={16} className="login-field__icon" />
                    <input
                      id="register-password"
                      type={showPass ? 'text' : 'password'}
                      name="password"
                      className="login-field__input"
                      placeholder="Letters & numbers"
                      value={form.password}
                      onChange={handleChange}
                      required
                    />
                  </div>
                </div>

                <div className="login-field">
                  <label className="login-field__label" htmlFor="register-confirm-password">
                    Confirm Password
                  </label>
                  <div className="login-field__wrap">
                    <Lock size={16} className="login-field__icon" />
                    <input
                      id="register-confirm-password"
                      type={showPass ? 'text' : 'password'}
                      name="confirmPassword"
                      className="login-field__input"
                      placeholder="Repeat password"
                      value={form.confirmPassword}
                      onChange={handleChange}
                      required
                    />
                    <button
                      type="button"
                      className="login-field__toggle"
                      onClick={() => setShowPass((s) => !s)}
                      aria-label="Toggle password visibility"
                      id="toggle-register-password"
                    >
                      {showPass ? <EyeOff size={15} /> : <Eye size={15} />}
                    </button>
                  </div>
                </div>
              </div>

              <Button variant="primary" type="submit" fullWidth loading={loading} id="register-submit-btn">
                Submit Registration
              </Button>

              <p style={{ textAlign: 'center', fontSize: '0.86rem', color: 'var(--text-secondary)', marginTop: '12px' }}>
                Already registered?{' '}
                <Link to="/login" style={{ color: 'var(--clr-primary)', fontWeight: 600, textDecoration: 'none' }}>
                  Sign In
                </Link>
              </p>
            </form>
          </>
        )}
      </div>
    </div>
  )
}
