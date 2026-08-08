import { useState, useEffect } from 'react'
import { Outlet, useLocation } from 'react-router-dom'
import Sidebar from '../components/Sidebar'
import { Bell, Search, Sun, Moon } from 'lucide-react'
import './DashboardLayout.css'

const PAGE_TITLES = {
  '/dashboard':    { title: 'Dashboard',    subtitle: 'Welcome back, Admin' },
  '/patients':     { title: 'Patients',     subtitle: 'Manage patient records' },
  '/doctors':      { title: 'Doctors',      subtitle: 'Medical staff directory' },
  '/appointments': { title: 'Appointments', subtitle: 'Schedule & manage visits' },
  '/settings':     { title: 'Settings',     subtitle: 'System configuration' },
}

export default function DashboardLayout() {
  const [collapsed, setCollapsed] = useState(false)
  const location = useLocation()

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
    setTheme(prev => {
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

  const pathKey = '/' + location.pathname.split('/')[1]
  const pageMeta = PAGE_TITLES[pathKey] || { title: 'MediCore', subtitle: '' }

  return (
    <div className={`layout ${collapsed ? 'layout--collapsed' : ''}`}>
      <Sidebar collapsed={collapsed} onToggle={() => setCollapsed(c => !c)} />

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

            <button className="topbar__avatar" id="profile-btn" aria-label="Profile">
              A
            </button>
          </div>
        </header>

        <main className="layout__main">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
