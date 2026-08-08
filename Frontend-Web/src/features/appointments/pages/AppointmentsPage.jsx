import { Calendar, Plus, Clock, Stethoscope, CheckCircle, XCircle, AlertCircle } from 'lucide-react'
import Button from '../../../components/Button'
import './AppointmentsPage.css'

const MOCK_APPOINTMENTS = [
  { id: 1, patient: 'Amal Perera', doctor: 'Dr. Priyantha Jayawardena', date: '2024-08-08', time: '09:00 AM', type: 'Consultation', status: 'confirmed', avatar: ['#0ea5e9','#6366f1'] },
  { id: 2, patient: 'Nimesha Silva', doctor: 'Dr. Sumedha Bandara', date: '2024-08-08', time: '10:30 AM', type: 'Follow-up', status: 'pending', avatar: ['#10b981','#0ea5e9'] },
  { id: 3, patient: 'Chaminda Fernando', doctor: 'Dr. Menaka Weerasinghe', date: '2024-08-08', time: '11:00 AM', type: 'Check-up', status: 'completed', avatar: ['#f59e0b','#ef4444'] },
  { id: 4, patient: 'Priya Rajapaksa', doctor: 'Dr. Ruwan Herath', date: '2024-08-09', time: '02:00 PM', type: 'Surgery Review', status: 'confirmed', avatar: ['#8b5cf6','#ec4899'] },
  { id: 5, patient: 'Kasun Wickramasinghe', doctor: 'Dr. Chamari Gunaratne', date: '2024-08-09', time: '03:30 PM', type: 'Consultation', status: 'cancelled', avatar: ['#06b6d4','#10b981'] },
]

const STATUS_CONFIG = {
  confirmed:  { icon: CheckCircle, color: 'var(--clr-success)',  bg: 'rgba(16,185,129,0.12)',  label: 'Confirmed' },
  pending:    { icon: AlertCircle, color: 'var(--clr-warning)',  bg: 'rgba(245,158,11,0.12)',  label: 'Pending' },
  completed:  { icon: CheckCircle, color: 'var(--clr-primary)',  bg: 'rgba(14,165,233,0.12)',  label: 'Completed' },
  cancelled:  { icon: XCircle,     color: 'var(--clr-danger)',   bg: 'rgba(239,68,68,0.12)',   label: 'Cancelled' },
}

export default function AppointmentsPage() {
  const today = MOCK_APPOINTMENTS.filter(a => a.date === '2024-08-08')
  const upcoming = MOCK_APPOINTMENTS.filter(a => a.date !== '2024-08-08')

  return (
    <div className="page-wrapper">
      <div className="page-header">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="page-title">Appointments</h1>
            <p className="page-subtitle">Schedule & manage patient visits</p>
          </div>
          <Button variant="primary" icon={Plus} id="add-appointment-btn">New Appointment</Button>
        </div>
      </div>

      <div className="appt-stats animate-fade-in">
        {[
          { label: "Today's Appointments", value: today.length, color: 'var(--clr-primary)' },
          { label: 'Confirmed', value: MOCK_APPOINTMENTS.filter(a => a.status === 'confirmed').length, color: 'var(--clr-success)' },
          { label: 'Pending', value: MOCK_APPOINTMENTS.filter(a => a.status === 'pending').length, color: 'var(--clr-warning)' },
          { label: 'Completed Today', value: MOCK_APPOINTMENTS.filter(a => a.status === 'completed').length, color: 'var(--clr-accent)' },
        ].map(s => (
          <div key={s.label} className="appt-stat glass-card">
            <span className="appt-stat__val" style={{ color: s.color }}>{s.value}</span>
            <span className="appt-stat__label">{s.label}</span>
          </div>
        ))}
      </div>

      <Section title="Today's Schedule" icon={Clock} items={today} />
      <Section title="Upcoming" icon={Calendar} items={upcoming} />
    </div>
  )
}

function Section({ title, icon: Icon, items }) {
  return (
    <div className="appt-section animate-fade-in">
      <div className="appt-section__header">
        <Icon size={16} />
        <h3>{title}</h3>
        <span className="appt-section__count">{items.length}</span>
      </div>
      <div className="appt-list">
        {items.map(appt => {
          const { icon: StatusIcon, color, bg, label } = STATUS_CONFIG[appt.status]
          return (
            <div key={appt.id} className="appt-item glass-card" id={`appt-${appt.id}`}>
              <div
                className="appt-item__avatar"
                style={{ background: `linear-gradient(135deg, ${appt.avatar[0]}, ${appt.avatar[1]})` }}
              >
                {appt.patient.split(' ').map(w => w[0]).join('').slice(0,2)}
              </div>
              <div className="appt-item__info">
                <div className="appt-item__patient">{appt.patient}</div>
                <div className="appt-item__doctor">
                  <Stethoscope size={12} /> {appt.doctor}
                </div>
              </div>
              <div className="appt-item__type">{appt.type}</div>
              <div className="appt-item__time">
                <Clock size={12} /> {appt.time}
              </div>
              <div className="appt-item__status" style={{ background: bg, color }}>
                <StatusIcon size={12} />
                {label}
              </div>
            </div>
          )
        })}
      </div>
    </div>
  )
}
