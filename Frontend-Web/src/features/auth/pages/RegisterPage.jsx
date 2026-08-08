import { Activity, Eye, EyeOff, FileText, Lock, Mail, Shield, Stethoscope, User } from 'lucide-react'
import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import Button from '../../../components/Button'
import './LoginPage.css'

export default function RegisterPage() {
  const navigate = useNavigate()
  const [showPass, setShowPass] = useState(false)
  const [loading, setLoading] = useState(false)
  const [submitted, setSubmitted] = useState(false)
  const [form, setForm] = useState({
    name: '',
    email: '',
    specialty: '',
    licenseNumber: '',
    password: '',
  })

  const handleChange = e => setForm(p => ({ ...p, [e.target.name]: e.target.value }))

  const handleSubmit = async e => {
    e.preventDefault()
    setLoading(true)
    await new Promise(r => setTimeout(r, 1200))
    setLoading(false)
    setSubmitted(true)
  }

  return (
    <div className="login-page">
      <div className="login-page__blob login-page__blob--1" />
      <div className="login-page__blob login-page__blob--2" />
      <div className="login-page__blob login-page__blob--3" />

      <div className="login-card glass-card animate-fade-in">
        <div className="login-card__brand">
          <div className="login-card__logo">
            <Activity size={26} strokeWidth={2.5} />
          </div>
          <h1 className="login-card__app-name">Medi<span>Core</span></h1>
        </div>

        {submitted ? (
          <div style={{ textAlign: 'center', padding: '10px 0' }}>
            <div style={{
              width: '56px',
              height: '56px',
              borderRadius: '50%',
              background: 'rgba(16, 185, 129, 0.15)',
              color: 'var(--clr-success)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              margin: '0 auto 16px',
            }}>
              <Shield size={28} />
            </div>
            <h2 className="login-card__title">Registration Submitted!</h2>
            <p className="login-card__sub" style={{ marginTop: '8px', lineHeight: '1.5' }}>
              Your Doctor account application has been submitted successfully. An <strong>Administrator</strong> must verify your SLMC License and approve your account before you can log in.
            </p>
            <div style={{ marginTop: '24px' }}>
              <Button variant="primary" fullWidth onClick={() => navigate('/login')} id="back-to-login-btn">
                Return to Login
              </Button>
            </div>
          </div>
        ) : (
          <>
            <div className="login-card__header">
              <h2 className="login-card__title">Doctor Registration</h2>
              <p className="login-card__sub">Apply for a Doctor account (Admin Approval Required)</p>
            </div>

            <form onSubmit={handleSubmit} className="login-form" id="register-form">
              <div className="login-field">
                <label className="login-field__label" htmlFor="register-name">Full Name (with Title)</label>
                <div className="login-field__wrap">
                  <User size={16} className="login-field__icon" />
                  <input
                    id="register-name"
                    type="text"
                    name="name"
                    className="login-field__input"
                    placeholder="Dr. Priyantha Jayawardena"
                    value={form.name}
                    onChange={handleChange}
                    required
                  />
                </div>
              </div>

              <div className="login-field">
                <label className="login-field__label" htmlFor="register-email">Official Email</label>
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

              <div className="login-field">
                <label className="login-field__label" htmlFor="register-specialty">Specialty / Department</label>
                <div className="login-field__wrap">
                  <Stethoscope size={16} className="login-field__icon" />
                  <input
                    id="register-specialty"
                    type="text"
                    name="specialty"
                    className="login-field__input"
                    placeholder="Cardiologist, Neurologist, etc."
                    value={form.specialty}
                    onChange={handleChange}
                    required
                  />
                </div>
              </div>

              <div className="login-field">
                <label className="login-field__label" htmlFor="register-license">SLMC License Number</label>
                <div className="login-field__wrap">
                  <FileText size={16} className="login-field__icon" />
                  <input
                    id="register-license"
                    type="text"
                    name="licenseNumber"
                    className="login-field__input"
                    placeholder="e.g. SLMC-89412"
                    value={form.licenseNumber}
                    onChange={handleChange}
                    required
                  />
                </div>
              </div>

              <div className="login-field">
                <label className="login-field__label" htmlFor="register-password">Password</label>
                <div className="login-field__wrap">
                  <Lock size={16} className="login-field__icon" />
                  <input
                    id="register-password"
                    type={showPass ? 'text' : 'password'}
                    name="password"
                    className="login-field__input"
                    placeholder="Create a strong password"
                    value={form.password}
                    onChange={handleChange}
                    required
                  />
                  <button
                    type="button"
                    className="login-field__toggle"
                    onClick={() => setShowPass(s => !s)}
                    aria-label="Toggle password visibility"
                    id="toggle-register-password"
                  >
                    {showPass ? <EyeOff size={15} /> : <Eye size={15} />}
                  </button>
                </div>
              </div>

              <Button
                variant="primary"
                type="submit"
                fullWidth
                loading={loading}
                id="register-submit-btn"
              >
                Submit Doctor Registration
              </Button>

              <p style={{ textAlign: 'center', fontSize: '0.86rem', color: 'var(--text-secondary)', marginTop: '8px' }}>
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
