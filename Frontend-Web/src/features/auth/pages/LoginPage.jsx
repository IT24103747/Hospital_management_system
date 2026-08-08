import { Activity, Eye, EyeOff, Lock, Mail } from 'lucide-react'
import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import Button from '../../../components/Button'
import './LoginPage.css'

export default function LoginPage() {
  const navigate = useNavigate()
  const [showPass, setShowPass] = useState(false)
  const [loading, setLoading] = useState(false)
  const [form, setForm] = useState({ email: '', password: '' })

  const handleChange = e => setForm(p => ({ ...p, [e.target.name]: e.target.value }))

  const handleSubmit = async e => {
    e.preventDefault()
    setLoading(true)
    await new Promise(r => setTimeout(r, 1200))
    setLoading(false)
    navigate('/dashboard')
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

        <div className="login-card__header">
          <h2 className="login-card__title">Welcome back</h2>
          <p className="login-card__sub">Sign in to Hospital Management System</p>
        </div>

        <form onSubmit={handleSubmit} className="login-form" id="login-form">
          <div className="login-field">
            <label className="login-field__label" htmlFor="login-email">Email Address</label>
            <div className="login-field__wrap">
              <Mail size={16} className="login-field__icon" />
              <input
                id="login-email"
                type="email"
                name="email"
                className="login-field__input"
                placeholder="admin@medicore.lk"
                value={form.email}
                onChange={handleChange}
                required
              />
            </div>
          </div>

          <div className="login-field">
            <label className="login-field__label" htmlFor="login-password">Password</label>
            <div className="login-field__wrap">
              <Lock size={16} className="login-field__icon" />
              <input
                id="login-password"
                type={showPass ? 'text' : 'password'}
                name="password"
                className="login-field__input"
                placeholder="Enter your password"
                value={form.password}
                onChange={handleChange}
                required
              />
              <button
                type="button"
                className="login-field__toggle"
                onClick={() => setShowPass(s => !s)}
                aria-label="Toggle password visibility"
                id="toggle-password"
              >
                {showPass ? <EyeOff size={15} /> : <Eye size={15} />}
              </button>
            </div>
          </div>

          <div className="login-form__meta">
            <label className="login-form__remember" htmlFor="remember">
              <input type="checkbox" id="remember" />
              Remember me
            </label>
            <a href="#" className="login-form__forgot">Forgot password?</a>
          </div>

          <Button
            variant="primary"
            type="submit"
            fullWidth
            loading={loading}
            id="login-submit-btn"
          >
            Sign In
          </Button>

          <p style={{ textAlign: 'center', fontSize: '0.86rem', color: 'var(--text-secondary)', marginTop: '8px' }}>
            Are you a Doctor?{' '}
            <Link to="/register" style={{ color: 'var(--clr-primary)', fontWeight: 600, textDecoration: 'none' }}>
              Register Here
            </Link>
          </p>
        </form>

        <p className="login-card__footer">
          MediCore HMS · SE3090 Assignment · SLIIT
        </p>
      </div>
    </div>
  )
}
