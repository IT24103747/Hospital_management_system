import { useState, useEffect, useRef } from 'react'
import { Outlet, useLocation, useNavigate } from 'react-router-dom'
import Sidebar from '../components/Sidebar'
import { Bell, Search, Sun, Moon, LogOut, User as UserIcon, Settings, CheckCheck } from 'lucide-react'
import './DashboardLayout.css'
import { useAuth } from '../features/auth/AuthContext'
import apiClient from '../lib/apiClient'

const PAGE_TITLES = {
  '/dashboard':       { title: 'Dashboard',       subtitle: 'Welcome back, Admin' },
  '/patients':        { title: 'Patients',        subtitle: 'Manage patient records' },
  '/doctors':         { title: 'Doctors',         subtitle: 'Medical staff directory' },
  '/appointments':    { title: 'Appointments',    subtitle: 'Schedule & manage visits' },
  '/medical-records': { title: 'Medical Records', subtitle: 'Manage patient EHR & clinical diagnostics' },
  '/settings':        { title: 'Settings',        subtitle: 'System configuration' },
  '/admin':           { title: 'Available Rooms', subtitle: 'Manage hospital room availability' },
}

export default function DashboardLayout() {
  const { user, signOut } = useAuth()
  const [collapsed, setCollapsed] = useState(false)
  const location = useLocation()
  const navigate = useNavigate()

  const [showNotifications, setShowNotifications] = useState(false)
  const [showProfileMenu, setShowProfileMenu] = useState(false)
  const [notifications, setNotifications] = useState([])
  const [notificationsLoading, setNotificationsLoading] = useState(false)

  const notifRef = useRef(null)
  const profileRef = useRef(null)

  const unreadCount = notifications.filter((n) => !n.read).length

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

  // Close dropdowns on outside click
  useEffect(() => {
    const handleClickOutside = (e) => {
      if (notifRef.current && !notifRef.current.contains(e.target)) {
        setShowNotifications(false)
      }
      if (profileRef.current && !profileRef.current.contains(e.target)) {
        setShowProfileMenu(false)
      }
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [])

  const handleMarkAllAsRead = () => {
    setNotifications((prev) => prev.map((n) => ({ ...n, read: true })))
  }

  const loadNotifications = async () => {
    if (user?.role?.toLowerCase() !== 'patient') return
    setNotificationsLoading(true)
    try {
      const { data } = await apiClient.get('/triage-workflows/notifications')
      setNotifications(data.map((item) => ({
        id: item.triageWorkflowId,
        title: 'Clinical Review Available',
        message: item.message || 'A clinician has reviewed your assessment.',
        time: item.createdAt ? new Date(item.createdAt).toLocaleString() : 'Recently',
        read: false,
      })))
    } catch {
      setNotifications([])
    } finally {
      setNotificationsLoading(false)
    }
  }

  useEffect(() => {
    if (user?.role?.toLowerCase() === 'patient') loadNotifications()
  // Refresh the badge whenever the signed-in account changes.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [user?.userId, user?.role])

  const handleSignOut = () => {
    signOut()
    navigate('/login')
  }

  const pathKey = '/' + location.pathname.split('/')[1]
  const doctorMeta =
    location.pathname === '/doctor/profile'
      ? { title: 'Profile', subtitle: 'Manage your professional profile' }
      : location.pathname === '/doctor/triage-review'
      ? { title: 'SafeTriage Review', subtitle: 'Clinical decision-support oversight' }
      : location.pathname === '/doctor/schedules'
      ? { title: 'Appointment Schedules', subtitle: 'Reserve rooms and manage your schedules' }
      : location.pathname === '/doctor/medical-records'
      ? { title: 'Medical Records', subtitle: 'Clinical consultations and diagnoses' }
      : { title: 'Doctor Dashboard', subtitle: `Welcome Dr. ${user?.fullName || ''}` }

  const pageMeta =
    user?.role === 'Doctor'
      ? doctorMeta
      : PAGE_TITLES[pathKey] || { title: 'MediCore', subtitle: '' }

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
            {user?.role !== 'Doctor' && (
              <div className="topbar__search" id="topbar-search">
                <Search size={15} className="topbar__search-icon" />
                <input
                  type="search"
                  placeholder="Search patients, doctors…"
                  className="topbar__search-input"
                  id="global-search"
                />
              </div>
            )}

            {/* Theme Toggle */}
            <button
              className="topbar__icon-btn"
              onClick={toggleTheme}
              aria-label="Toggle theme"
              id="theme-toggle-btn"
              title="Toggle theme"
            >
              {theme === 'light' ? <Moon size={18} /> : <Sun size={18} />}
            </button>

            {/* Notifications Popover */}
            <div className="topbar__dropdown-wrap" ref={notifRef}>
              <button
                className="topbar__icon-btn"
                id="notifications-btn"
                aria-label="Notifications"
                onClick={() => {
                  setShowNotifications((prev) => {
                    if (!prev) loadNotifications()
                    return !prev
                  })
                  setShowProfileMenu(false)
                }}
                title="Notifications"
              >
                <Bell size={18} />
                {unreadCount > 0 && (
                  <span className="topbar__badge">{unreadCount}</span>
                )}
              </button>

              {showNotifications && (
                <div className="topbar__popover notifications-popover">
                  <div className="notifications-header">
                    <span className="notifications-title">Notifications</span>
                    {unreadCount > 0 && (
                      <button
                        className="notifications-clear"
                        onClick={handleMarkAllAsRead}
                      >
                        Mark all as read
                      </button>
                    )}
                  </div>
                  <div className="notifications-list">
                    {notificationsLoading && <div className="notification-item"><div>Loading notifications…</div></div>}
                    {!notificationsLoading && notifications.length === 0 && <div className="notification-item"><div><div className="notification-title">No new notifications</div><div className="notification-time">Clinical review responses will appear here.</div></div></div>}
                    {notifications.map((notif) => (
                      <div
                        key={notif.id}
                        className="notification-item"
                        onClick={() => {
                          setNotifications((prev) =>
                            prev.map((n) =>
                              n.id === notif.id ? { ...n, read: true } : n
                            )
                          )
                        }}
                      >
                        <div
                          className={`notification-dot ${
                            notif.read ? 'notification-dot--read' : ''
                          }`}
                        />
                        <div>
                          <div className="notification-title">{notif.title}</div>
                          {notif.message && <div className="notification-time">{notif.message}</div>}
                          <div className="notification-time">{notif.time}</div>
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </div>

            {/* User Profile Popover */}
            <div className="topbar__dropdown-wrap" ref={profileRef}>
              <button
                className="topbar__avatar"
                id="profile-btn"
                aria-label="Profile"
                onClick={() => {
                  setShowProfileMenu((prev) => !prev)
                  setShowNotifications(false)
                }}
                title="Account Menu"
              >
                {user?.fullName?.[0] || 'U'}
              </button>

              {showProfileMenu && (
                <div className="topbar__popover profile-popover">
                  <div className="profile-header">
                    <div className="profile-name">{user?.fullName || 'MediCore User'}</div>
                    <div className="profile-email">{user?.email || 'user@medicore.com'}</div>
                    <span className="profile-role-badge">{user?.role || 'Staff'}</span>
                  </div>

                  {user?.role === 'Doctor' && (
                    <button
                      type="button"
                      className="profile-menu-item"
                      onClick={() => {
                        setShowProfileMenu(false)
                        navigate('/doctor/profile')
                      }}
                    >
                      <UserIcon size={16} /> My Doctor Profile
                    </button>
                  )}

                  <button
                    type="button"
                    className="profile-menu-item"
                    onClick={() => {
                      toggleTheme()
                    }}
                  >
                    {theme === 'light' ? <Moon size={16} /> : <Sun size={16} />} Switch to {theme === 'light' ? 'Dark' : 'Light'} Mode
                  </button>

                  <button
                    type="button"
                    className="profile-menu-item profile-menu-item--danger"
                    onClick={handleSignOut}
                  >
                    <LogOut size={16} /> Sign Out
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
