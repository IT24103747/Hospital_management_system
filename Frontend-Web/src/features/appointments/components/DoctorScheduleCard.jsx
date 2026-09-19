import { DoorOpen, Calendar, Clock, Users, Banknote, Edit3, XCircle, Trash2, Lock } from 'lucide-react'
import Button from '../../../components/Button'
import { formatDate } from '../../../lib/utils'
import './DoctorScheduleCard.css'

export default function DoctorScheduleCard({ schedule, onEdit, onCancel, onDelete }) {
  const isPast = new Date(schedule.endAt).getTime() <= Date.now()
  const canEdit = schedule.isActive && !isPast
  const canModifyOrDelete = !schedule.hasPatientBookings

  const formatTimeRange = (startStr, endStr) => {
    const start = new Date(startStr).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
    const end = new Date(endStr).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
    return `${start} – ${end}`
  }

  const feeFormatted = `LKR ${Number(schedule.consultationFee).toLocaleString(undefined, {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}`

  return (
    <article className="schedule-card-item">
      <div
        className="schedule-card-item__accent"
        style={{
          background: schedule.isActive
            ? 'linear-gradient(180deg, #0ea5e9, #6366f1)'
            : 'linear-gradient(180deg, #94a3b8, #64748b)',
        }}
      />

      <header className="schedule-card-item__header">
        <div
          className="schedule-card-item__avatar"
          style={{
            background: schedule.isActive
              ? 'linear-gradient(135deg, #0ea5e9, #6366f1)'
              : 'linear-gradient(135deg, #64748b, #475569)',
          }}
        >
          <DoorOpen size={24} />
        </div>

        <div className="schedule-card-item__info">
          <h4 className="schedule-card-item__name">Room {schedule.roomNumber}</h4>
          <span className="schedule-card-item__meta">
            {schedule.roomName} • {schedule.floor}
          </span>
        </div>

        <div className="schedule-card-item__status-wrap">
          <span className={`schedule-status-badge ${schedule.isActive ? 'status-active' : 'status-cancelled'}`}>
            {schedule.isActive ? 'Active' : 'Cancelled'}
          </span>
        </div>
      </header>

      <div className="schedule-card-item__record">
        <Detail icon={Users} label="Booked Capacity" value={`${schedule.bookedCount} / ${schedule.capacity}`} />
        <Detail icon={Banknote} label="Consulting Fee" value={feeFormatted} />
      </div>

      <div className="schedule-card-item__details">
        <Detail icon={Calendar} label="Date" value={formatDate(schedule.startAt)} />
        <Detail icon={Clock} label="Time Slot" value={formatTimeRange(schedule.startAt, schedule.endAt)} />
      </div>

      {schedule.hasPatientBookings && (
        <div className="schedule-card-item__locked-box">
          <Lock size={15} />
          <span>Booked schedule locked (patients registered)</span>
        </div>
      )}

      <footer className="schedule-card-item__footer">
        <div className="schedule-card-item__actions">
          {canEdit && (
            <Button
              variant="secondary"
              size="sm"
              icon={Edit3}
              onClick={() => onEdit(schedule)}
              id={`edit-schedule-${schedule.doctorTimeSlotId}`}
            >
              Edit
            </Button>
          )}

          {canModifyOrDelete && schedule.isActive && (
            <Button
              variant="outline"
              size="sm"
              icon={XCircle}
              onClick={() => onCancel(schedule)}
              id={`cancel-schedule-${schedule.doctorTimeSlotId}`}
            >
              Cancel
            </Button>
          )}

          {canModifyOrDelete && (
            <Button
              variant="danger"
              size="sm"
              icon={Trash2}
              onClick={() => onDelete(schedule)}
              id={`delete-schedule-${schedule.doctorTimeSlotId}`}
            >
              Delete
            </Button>
          )}
        </div>
      </footer>
    </article>
  )
}

function Detail({ icon: Icon, label, value }) {
  return (
    <div className="schedule-card-item__detail" title={value || undefined}>
      <Icon size={15} aria-hidden="true" />
      <div className="schedule-card-item__detail-content">
        <span className="schedule-card-item__detail-label">{label}</span>
        <span className="schedule-card-item__detail-value">{value || '—'}</span>
      </div>
    </div>
  )
}
