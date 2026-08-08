import { Users, Stethoscope, Calendar, TrendingUp, ArrowUp, ArrowDown } from 'lucide-react'
import {
  AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, BarChart, Bar
} from 'recharts'
import { MOCK_PATIENTS } from '../../patients/services/patientApi'
import { BloodGroupBadge } from '../../../components/Badge'
import { formatDate, calculateAge, getInitials, nameToGradient } from '../../../lib/utils'
import './DashboardPage.css'

const MONTHLY_DATA = [
  { month: 'Jan', patients: 24, appointments: 40 },
  { month: 'Feb', patients: 18, appointments: 35 },
  { month: 'Mar', patients: 31, appointments: 55 },
  { month: 'Apr', patients: 28, appointments: 48 },
  { month: 'May', patients: 42, appointments: 62 },
  { month: 'Jun', patients: 35, appointments: 58 },
  { month: 'Jul', patients: 50, appointments: 75 },
  { month: 'Aug', patients: 45, appointments: 70 },
]

const STATS = [
  {
    id: 'stat-patients',
    label: 'Total Patients',
    value: '1,284',
    change: '+12%',
    up: true,
    icon: Users,
    color: 'var(--clr-primary)',
    bg: 'rgba(14,165,233,0.1)',
  },
  {
    id: 'stat-doctors',
    label: 'Active Doctors',
    value: '48',
    change: '+3',
    up: true,
    icon: Stethoscope,
    color: 'var(--clr-accent)',
    bg: 'rgba(99,102,241,0.1)',
  },
  {
    id: 'stat-appointments',
    label: 'Today\'s Appointments',
    value: '37',
    change: '-5%',
    up: false,
    icon: Calendar,
    color: 'var(--clr-warning)',
    bg: 'rgba(245,158,11,0.1)',
  },
  {
    id: 'stat-revenue',
    label: 'Monthly Revenue',
    value: 'Rs 4.2M',
    change: '+18%',
    up: true,
    icon: TrendingUp,
    color: 'var(--clr-success)',
    bg: 'rgba(16,185,129,0.1)',
  },
]

const CustomTooltip = ({ active, payload, label }) => {
  if (!active || !payload?.length) return null
  return (
    <div className="chart-tooltip">
      <p className="chart-tooltip__label">{label}</p>
      {payload.map(entry => (
        <p key={entry.name} style={{ color: entry.color, fontSize: '0.82rem' }}>
          {entry.name}: <strong>{entry.value}</strong>
        </p>
      ))}
    </div>
  )
}

export default function DashboardPage() {
  const recent = MOCK_PATIENTS.slice(0, 5)

  return (
    <div className="page-wrapper">
      <div className="grid-cols-4 stagger-children animate-fade-in" style={{ marginBottom: '28px' }}>
        {STATS.map(stat => {
          const Icon = stat.icon
          return (
            <div key={stat.id} id={stat.id} className="stat-card glass-card">
              <div className="stat-card__top">
                <div className="stat-card__icon" style={{ background: stat.bg, color: stat.color }}>
                  <Icon size={20} />
                </div>
                <span className={`stat-card__change ${stat.up ? 'stat-card__change--up' : 'stat-card__change--down'}`}>
                  {stat.up ? <ArrowUp size={11} /> : <ArrowDown size={11} />}
                  {stat.change}
                </span>
              </div>
              <div className="stat-card__value">{stat.value}</div>
              <div className="stat-card__label">{stat.label}</div>
            </div>
          )
        })}
      </div>

      <div className="dashboard__charts animate-fade-in" style={{ animationDelay: '0.1s' }}>
        <div className="glass-card chart-card">
          <div className="chart-card__header">
            <div>
              <h3 className="chart-card__title">Patient Admissions</h3>
              <p className="chart-card__sub">Monthly trend – 2024</p>
            </div>
            <div className="chart-legend">
              <span className="chart-legend__dot" style={{ background: 'var(--clr-primary)' }} />
              <span>Patients</span>
              <span className="chart-legend__dot" style={{ background: 'var(--clr-accent)' }} />
              <span>Appointments</span>
            </div>
          </div>
          <ResponsiveContainer width="100%" height={220}>
            <AreaChart data={MONTHLY_DATA} margin={{ top: 5, right: 10, left: -20, bottom: 0 }}>
              <defs>
                <linearGradient id="colPrimary" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%" stopColor="#0ea5e9" stopOpacity={0.3} />
                  <stop offset="95%" stopColor="#0ea5e9" stopOpacity={0} />
                </linearGradient>
                <linearGradient id="colAccent" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%" stopColor="#6366f1" stopOpacity={0.3} />
                  <stop offset="95%" stopColor="#6366f1" stopOpacity={0} />
                </linearGradient>
              </defs>
              <CartesianGrid strokeDasharray="3 3" stroke="rgba(148,163,184,0.08)" />
              <XAxis dataKey="month" tick={{ fill: 'var(--text-muted)', fontSize: 11 }} axisLine={false} tickLine={false} />
              <YAxis tick={{ fill: 'var(--text-muted)', fontSize: 11 }} axisLine={false} tickLine={false} />
              <Tooltip content={<CustomTooltip />} />
              <Area type="monotone" dataKey="patients" name="Patients" stroke="#0ea5e9" strokeWidth={2} fill="url(#colPrimary)" />
              <Area type="monotone" dataKey="appointments" name="Appointments" stroke="#6366f1" strokeWidth={2} fill="url(#colAccent)" />
            </AreaChart>
          </ResponsiveContainer>
        </div>

        <div className="glass-card chart-card">
          <div className="chart-card__header">
            <div>
              <h3 className="chart-card__title">Weekly Admissions</h3>
              <p className="chart-card__sub">This week vs last week</p>
            </div>
          </div>
          <ResponsiveContainer width="100%" height={220}>
            <BarChart data={MONTHLY_DATA.slice(-4)} margin={{ top: 5, right: 10, left: -20, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="rgba(148,163,184,0.08)" />
              <XAxis dataKey="month" tick={{ fill: 'var(--text-muted)', fontSize: 11 }} axisLine={false} tickLine={false} />
              <YAxis tick={{ fill: 'var(--text-muted)', fontSize: 11 }} axisLine={false} tickLine={false} />
              <Tooltip content={<CustomTooltip />} />
              <Bar dataKey="patients" name="Patients" fill="#0ea5e9" radius={[4,4,0,0]} opacity={0.85} />
              <Bar dataKey="appointments" name="Appointments" fill="#6366f1" radius={[4,4,0,0]} opacity={0.85} />
            </BarChart>
          </ResponsiveContainer>
        </div>
      </div>

      <div className="glass-card animate-fade-in" style={{ animationDelay: '0.2s', marginTop: '28px' }}>
        <div className="chart-card__header" style={{ padding: '20px 24px', borderBottom: '1px solid var(--border-default)' }}>
          <div>
            <h3 className="chart-card__title">Recent Patients</h3>
            <p className="chart-card__sub">Latest registrations</p>
          </div>
          <a href="/patients" className="dashboard__link" id="view-all-patients">View all →</a>
        </div>
        <div className="recent-patients">
          {recent.map((p, i) => {
            const [c1, c2] = nameToGradient(p.firstName)
            return (
              <div key={p.patientId} className="recent-patient" style={{ animationDelay: `${i * 0.06}s` }}>
                <div
                  className="recent-patient__avatar"
                  style={{ background: `linear-gradient(135deg, ${c1}, ${c2})` }}
                >
                  {getInitials(p.firstName, p.lastName)}
                </div>
                <div className="recent-patient__info">
                  <div className="recent-patient__name">{p.firstName} {p.lastName}</div>
                  <div className="recent-patient__meta">{p.gender} · {calculateAge(p.dateOfBirth)} yrs</div>
                </div>
                <BloodGroupBadge group={p.bloodGroup} />
                <div className="recent-patient__date">{formatDate(p.createdAt)}</div>
              </div>
            )
          })}
        </div>
      </div>
    </div>
  )
}
