import { NavLink } from 'react-router-dom'
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
} from 'lucide-react'
import './Sidebar.css'

const NAV_ITEMS = [
  { label: 'Dashboard',    icon: LayoutDashboard, to: '/dashboard' },
  { label: 'Patients',     icon: Users,           to: '/patients' },
  { label: 'Doctors',      icon: Stethoscope,     to: '/doctors' },
  { label: 'Appointments', icon: Calendar,        to: '/appointments' },
]

const BOTTOM_ITEMS = [
  { label: 'Settings',    icon: Settings,  to: '/settings' },
  { label: 'Logout',      icon: LogOut,    to: '/login',   danger: true },
]

export default function Sidebar({ collapsed, onToggle }) {
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
        {NAV_ITEMS.map(({ label, icon: Icon, to }) => (
          <NavLink
            key={to}
            to={to}
            id={`nav-${label.toLowerCase()}`}
            className={({ isActive }) =>
              `sidebar__link ${isActive ? 'sidebar__link--active' : ''}`
            }
          >
            <span className="sidebar__icon"><Icon size={18} strokeWidth={2} /></span>
            {!collapsed && <span className="sidebar__label">{label}</span>}
          </NavLink>
        ))}
      </nav>

      <div className="sidebar__bottom">
        {!collapsed && <p className="sidebar__section-label">ACCOUNT</p>}
        {BOTTOM_ITEMS.map(({ label, icon: Icon, to, danger }) => (
          <NavLink
            key={to}
            to={to}
            id={`nav-${label.toLowerCase()}`}
            className={`sidebar__link ${danger ? 'sidebar__link--danger' : ''}`}
          >
            <span className="sidebar__icon"><Icon size={18} strokeWidth={2} /></span>
            {!collapsed && <span className="sidebar__label">{label}</span>}
          </NavLink>
        ))}

        {!collapsed && (
          <div className="sidebar__user">
            <div className="sidebar__avatar">A</div>
            <div>
              <div className="sidebar__user-name">Admin User</div>
              <div className="sidebar__user-role">System Admin</div>
            </div>
          </div>
        )}
      </div>
    </aside>
  )
}
