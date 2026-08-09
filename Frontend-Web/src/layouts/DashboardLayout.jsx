import { useState, useRef, useEffect } from 'react'
import { Outlet, useLocation, useNavigate } from 'react-router-dom'
import Sidebar from '../components/Sidebar'
import { Bell, Search, Sun, Moon, ChevronDown, User, LogOut } from 'lucide-react'
import { useAuth } from '../context/AuthContext'
import './DashboardLayout.css'

const PAGE_TITLES = {
  '/dashboard':    { title: 'Dashboard',    subtitle: 'Overview & statistics' },
  '/patients':     { title: 'Patients',     subtitle: 'Manage patient records' },
  '/doctors':      { title: 'Doctors',      subtitle: 'Medical staff & registration requests' },
  '/appointments': { title: 'Appointments', subtitle: 'Schedule & manage visits' },
  '/profile':      { title: 'Doctor Profile', subtitle: 'View and edit profile details' },
  '/settings':     { title: 'Settings',     subtitle: 'System configuration' },
}

export default function DashboardLayout() {
  const [collapsed, setCollapsed] = useState(false)
  const [dropdownOpen, setDropdownOpen] = useState(false)
  const dropdownRef = useRef(null)
  const location = useLocation()
  const navigate = useNavigate()
  const { user, logout } = useAuth()

  const [theme, setTheme] = useState(() => {
    const saved = localStorage.getItem('hms_theme') || 'light'
    if (saved === 'dark') {
      document.body.classList.add('dark')
    } else {
      document.body.classList.remove('dark')
    }
    return saved
  })

  const toggleTheme = () => {
    setTheme((prev) => {
      const next = prev === 'light' ? 'dark' : 'light'
      localStorage.setItem('hms_theme', next)
      if (next === 'dark') {
        document.body.classList.add('dark')
      } else {
        document.body.classList.remove('dark')
      }
      return next
    })
  }

  // Close dropdown on outside click
  useEffect(() => {
    const handleClickOutside = (e) => {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target)) {
        setDropdownOpen(false)
      }
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [])

  const pathKey = '/' + location.pathname.split('/')[1]
  const pageMeta = PAGE_TITLES[pathKey] || { title: 'MediCore', subtitle: '' }

  const isDoctor = user?.role === 'Doctor'
  const doctorName = isDoctor
    ? `${user?.doctorProfile?.firstName || user?.fullName?.split(' ')[0] || ''} ${
        user?.doctorProfile?.lastName || user?.fullName?.split(' ').slice(1).join(' ') || ''
      }`.trim()
    : user?.fullName || 'User'

  const welcomeMessage = isDoctor ? `Welcome Dr, ${doctorName}` : `Welcome ${user?.fullName || 'Admin'}`

  const handleLogout = () => {
    logout()
    navigate('/login')
  }

  return (
    <div className={`layout ${collapsed ? 'layout--collapsed' : ''}`}>
      <Sidebar collapsed={collapsed} onToggle={() => setCollapsed((c) => !c)} />

      <div className="layout__content">
        <header className="topbar">
          <div className="topbar__left">
            <div className="topbar__page-info">
              <h1 className="topbar__title">{pageMeta.title}</h1>
              <p className="topbar__subtitle">{pageMeta.subtitle}</p>
            </div>
          </div>

          <div className="topbar__right">
            <div className="topbar__search" id="topbar-search">
              <Search size={15} className="topbar__search-icon" />
              <input
                type="search"
                placeholder="Search patients, doctors…"
                className="topbar__search-input"
                id="global-search"
              />
            </div>

            <button
              className="topbar__icon-btn"
              onClick={toggleTheme}
              aria-label="Toggle theme"
              id="theme-toggle-btn"
            >
              {theme === 'light' ? <Moon size={18} /> : <Sun size={18} />}
            </button>

            <button className="topbar__icon-btn" id="notifications-btn" aria-label="Notifications">
              <Bell size={18} />
              <span className="topbar__badge">3</span>
            </button>

            {/* Profile Dropdown Menu */}
            <div style={{ position: 'relative' }} ref={dropdownRef}>
              <button
                type="button"
                onClick={() => setDropdownOpen((s) => !s)}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: '8px',
                  background: 'var(--bg-card, #ffffff)',
                  border: '1px solid var(--border-color, #e2e8f0)',
                  borderRadius: '30px',
                  padding: '4px 12px 4px 6px',
                  cursor: 'pointer',
                  fontSize: '0.88rem',
                  fontWeight: 600,
                  color: 'var(--text-main, #1e293b)',
                  boxShadow: '0 2px 4px rgba(0,0,0,0.04)',
                }}
                id="profile-dropdown-trigger"
              >
                <div
                  className="topbar__avatar"
                  style={{
                    width: '32px',
                    height: '32px',
                    borderRadius: '50%',
                    background: 'linear-gradient(135deg, var(--clr-primary, #0ea5e9), #6366f1)',
                    color: '#fff',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    fontSize: '0.85rem',
                    fontWeight: 700,
                  }}
                >
                  {isDoctor ? 'Dr' : doctorName[0]}
                </div>
                <span>{isDoctor ? `Welcome Dr, ${doctorName}` : doctorName}</span>
                <ChevronDown size={14} style={{ transition: 'transform 0.2s', transform: dropdownOpen ? 'rotate(180deg)' : 'none' }} />
              </button>

              {dropdownOpen && (
                <div
                  style={{
                    position: 'absolute',
                    top: 'calc(100% + 8px)',
                    right: 0,
                    width: '210px',
                    background: 'var(--bg-card, #ffffff)',
                    border: '1px solid var(--border-color, #e2e8f0)',
                    borderRadius: '12px',
                    boxShadow: '0 10px 25px -5px rgba(0,0,0,0.15)',
                    padding: '6px',
                    zIndex: 1000,
                  }}
                  id="profile-dropdown-menu"
                >
                  <button
                    type="button"
                    onClick={() => {
                      setDropdownOpen(false)
                      navigate('/profile')
                    }}
                    style={{
                      width: '100%',
                      display: 'flex',
                      alignItems: 'center',
                      gap: '10px',
                      padding: '10px 12px',
                      border: 'none',
                      background: 'none',
                      borderRadius: '8px',
                      cursor: 'pointer',
                      fontSize: '0.88rem',
                      color: 'var(--text-main, #1e293b)',
                      textAlign: 'left',
                      fontWeight: 500,
                    }}
                    onMouseEnter={(e) => (e.currentTarget.style.background = 'var(--bg-hover, #f1f5f9)')}
                    onMouseLeave={(e) => (e.currentTarget.style.background = 'none')}
                    id="view-profile-btn"
                  >
                    <User size={16} style={{ color: 'var(--clr-primary, #0ea5e9)' }} />
                    View Profile
                  </button>

                  <div style={{ height: '1px', background: 'var(--border-color, #e2e8f0)', margin: '4px 0' }} />

                  <button
                    type="button"
                    onClick={() => {
                      setDropdownOpen(false)
                      handleLogout()
                    }}
                    style={{
                      width: '100%',
                      display: 'flex',
                      alignItems: 'center',
                      gap: '10px',
                      padding: '10px 12px',
                      border: 'none',
                      background: 'none',
                      borderRadius: '8px',
                      cursor: 'pointer',
                      fontSize: '0.88rem',
                      color: '#ef4444',
                      textAlign: 'left',
                      fontWeight: 500,
                    }}
                    onMouseEnter={(e) => (e.currentTarget.style.background = 'rgba(239,68,68,0.08)')}
                    onMouseLeave={(e) => (e.currentTarget.style.background = 'none')}
                    id="logout-btn"
                  >
                    <LogOut size={16} />
                    Log Out
                  </button>
                </div>
              )}
            </div>
          </div>
        </header>

        <main className="layout__main">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
