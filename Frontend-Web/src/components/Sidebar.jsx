import { NavLink, useNavigate } from 'react-router-dom'
import {
  LayoutDashboard,
  Users,
  Stethoscope,
  Calendar,
  Settings,
  LogOut,
  Activity,
  ChevronLeft,
  ChevronRight,
  User,
} from 'lucide-react'
import { useAuth } from '../context/AuthContext'
import './Sidebar.css'

export default function Sidebar({ collapsed, onToggle }) {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const isDoctor = user?.role === 'Doctor'

  const navItems = isDoctor
    ? [
        { label: 'Dashboard', icon: LayoutDashboard, to: '/dashboard' },
        { label: 'My Profile', icon: User, to: '/profile' },
        { label: 'Appointments', icon: Calendar, to: '/appointments' },
        { label: 'Patients', icon: Users, to: '/patients' },
      ]
    : [
        { label: 'Dashboard', icon: LayoutDashboard, to: '/dashboard' },
        { label: 'Patients', icon: Users, to: '/patients' },
        { label: 'Doctors', icon: Stethoscope, to: '/doctors' },
        { label: 'Appointments', icon: Calendar, to: '/appointments' },
      ]

  const handleLogout = (e) => {
    e.preventDefault()
    logout()
    navigate('/login')
  }

  const doctorName = isDoctor
    ? `${user?.doctorProfile?.firstName || user?.fullName?.split(' ')[0] || ''} ${
        user?.doctorProfile?.lastName || user?.fullName?.split(' ').slice(1).join(' ') || ''
      }`.trim()
    : user?.fullName || 'Admin User'

  const roleText = isDoctor ? user?.doctorProfile?.specialization || 'Doctor' : 'System Admin'

  return (
    <aside className={`sidebar ${collapsed ? 'sidebar--collapsed' : ''}`}>
      <div className="sidebar__brand">
        <div className="sidebar__logo">
          <Activity size={22} strokeWidth={2.5} />
        </div>
        {!collapsed && (
          <span className="sidebar__brand-name">
            Medi<span>Core</span>
          </span>
        )}
      </div>

      <button
        id="sidebar-toggle"
        className="sidebar__toggle"
        onClick={onToggle}
        aria-label="Toggle sidebar"
      >
        {collapsed ? <ChevronRight size={16} /> : <ChevronLeft size={16} />}
      </button>

      <nav className="sidebar__nav">
        {!collapsed && <p className="sidebar__section-label">MAIN MENU</p>}
        {navItems.map(({ label, icon: Icon, to }) => (
          <NavLink
            key={to}
            to={to}
            id={`nav-${label.toLowerCase().replace(/\s+/g, '-')}`}
            className={({ isActive }) =>
              `sidebar__link ${isActive ? 'sidebar__link--active' : ''}`
            }
          >
            <span className="sidebar__icon">
              <Icon size={18} strokeWidth={2} />
            </span>
            {!collapsed && <span className="sidebar__label">{label}</span>}
          </NavLink>
        ))}
      </nav>

      <div className="sidebar__bottom">
        {!collapsed && <p className="sidebar__section-label">ACCOUNT</p>}
        
        {!isDoctor && (
          <NavLink
            to="/settings"
            id="nav-settings"
            className={({ isActive }) =>
              `sidebar__link ${isActive ? 'sidebar__link--active' : ''}`
            }
          >
            <span className="sidebar__icon">
              <Settings size={18} strokeWidth={2} />
            </span>
            {!collapsed && <span className="sidebar__label">Settings</span>}
          </NavLink>
        )}

        <button
          type="button"
          onClick={handleLogout}
          id="nav-logout"
          className="sidebar__link sidebar__link--danger"
          style={{ width: '100%', border: 'none', background: 'none', textAlign: 'left', cursor: 'pointer' }}
        >
          <span className="sidebar__icon">
            <LogOut size={18} strokeWidth={2} />
          </span>
          {!collapsed && <span className="sidebar__label">Logout</span>}
        </button>

        {!collapsed && (
          <div className="sidebar__user">
            <div
              className="sidebar__avatar"
              style={
                isDoctor
                  ? { background: 'linear-gradient(135deg, #0ea5e9, #6366f1)', color: '#fff' }
                  : {}
              }
            >
              {isDoctor ? 'Dr' : 'A'}
            </div>
            <div style={{ overflow: 'hidden' }}>
              <div className="sidebar__user-name" style={{ whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                {isDoctor ? `Dr. ${doctorName}` : doctorName}
              </div>
              <div className="sidebar__user-role">{roleText}</div>
            </div>
          </div>
        )}
      </div>
    </aside>
  )
}
