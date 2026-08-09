import { useEffect, useMemo, useRef, useState } from 'react'
import { AlertTriangle, CalendarClock, CheckCircle, Clock, Plus, RefreshCw, Search, Stethoscope, XCircle } from 'lucide-react'
import Button from '../../../components/Button'
import Modal from '../../../components/Modal'
import Table from '../../../components/Table'
import { useAppointments } from '../hooks/useAppointments'
import './AppointmentsPage.css'

const STATUSES = ['Requested', 'Confirmed', 'Completed', 'Cancelled', 'No-show']
const TYPES = ['Consultation', 'Follow-up', 'Check-up', 'Procedure Review', 'Emergency']

const STATUS_CONFIG = {
  Requested: { icon: AlertTriangle, color: 'var(--clr-warning)', bg: 'rgba(245,158,11,0.12)' },
  Confirmed: { icon: CheckCircle, color: 'var(--clr-success)', bg: 'rgba(16,185,129,0.12)' },
  Completed: { icon: CheckCircle, color: 'var(--clr-primary)', bg: 'rgba(14,165,233,0.12)' },
  Cancelled: { icon: XCircle, color: 'var(--clr-danger)', bg: 'rgba(239,68,68,0.12)' },
  'No-show': { icon: XCircle, color: 'var(--text-muted)', bg: 'rgba(100,116,139,0.12)' },
}

const emptyAppointment = {
  doctorName: '',
  doctorTimeSlotId: '',
  appointmentNumber: '',
  patientName: '',
  patientPhone: '',
  patientEmail: '',
  appointmentType: 'Consultation',
  reason: '',
  notes: '',
}

const emptySlot = {
  doctorName: '',
  specialty: '',
  date: '',
  startTime: '',
  endTime: '',
  capacity: 1,
}

export default function AppointmentsPage() {
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('all')
  const [date, setDate] = useState('')
  const [appointmentOpen, setAppointmentOpen] = useState(false)
  const [appointmentNumberOpen, setAppointmentNumberOpen] = useState(false)
  const [slotOpen, setSlotOpen] = useState(false)
  const [cancelTarget, setCancelTarget] = useState(null)
  const [cancelReason, setCancelReason] = useState('')
  const [appointmentForm, setAppointmentForm] = useState(emptyAppointment)
  const [slotForm, setSlotForm] = useState(emptySlot)
  const appointmentNumberRef = useRef(null)

  const filters = useMemo(() => ({ search, status, date, sortBy: 'startAt', sortDirection: 'asc' }), [search, status, date])
  const {
    appointments,
    slots,
    loading,
    saving,
    error,
    refetch,
    createAppointment,
    createSlot,
    updateStatus,
    cancelAppointment,
  } = useAppointments(filters)

  const today = new Date().toISOString().slice(0, 10)
  const stats = [
    { label: "Today's Appointments", value: appointments.filter(a => a.startAt?.slice(0, 10) === today).length, color: 'var(--clr-primary)' },
    { label: 'Available Slots', value: slots.reduce((sum, s) => sum + Math.max(0, (s.availableCount ?? s.capacity - s.bookedCount)), 0), color: 'var(--clr-info)' },
    { label: 'Confirmed', value: appointments.filter(a => a.status === 'Confirmed').length, color: 'var(--clr-success)' },
    { label: 'Requested', value: appointments.filter(a => a.status === 'Requested').length, color: 'var(--clr-warning)' },
  ]
  const doctors = useMemo(() => {
    const byName = new Map()
    slots.forEach(slot => {
      if (!byName.has(slot.doctorName)) byName.set(slot.doctorName, slot.specialty)
    })
    return Array.from(byName, ([doctorName, specialty]) => ({ doctorName, specialty }))
  }, [slots])
  const selectedDoctorSlots = useMemo(() => {
    const term = appointmentForm.doctorName.trim().toLowerCase()
    if (!term) return []
    return slots.filter(slot =>
      slot.doctorName.toLowerCase() === term &&
      (slot.availableCount ?? slot.capacity - slot.bookedCount) > 0)
  }, [appointmentForm.doctorName, slots])
  const selectedSlot = slots.find(slot => String(slot.doctorTimeSlotId) === String(appointmentForm.doctorTimeSlotId))

  const getBookedNumbersForSlot = (slot) => {
    const bySlot = appointments
      .filter(a => String(a.doctorTimeSlotId) === String(slot.doctorTimeSlotId))
      .filter(a => ['Requested', 'Confirmed', 'Completed', 'No-show'].includes(a.status))
      .map(a => Number(a.appointmentNumber))

    const fromSlot = (slot.bookedAppointmentNumbers || []).map(n => Number(n))
    return new Set([...bySlot, ...fromSlot])
  }

  useEffect(() => {
    if (!appointmentNumberOpen) return

    const onPointerDown = (event) => {
      if (!appointmentNumberRef.current) return
      if (!appointmentNumberRef.current.contains(event.target)) {
        setAppointmentNumberOpen(false)
      }
    }

    document.addEventListener('mousedown', onPointerDown)
    return () => document.removeEventListener('mousedown', onPointerDown)
  }, [appointmentNumberOpen])

  const closeAppointmentModal = () => {
    setAppointmentOpen(false)
    setAppointmentNumberOpen(false)
  }

  const handleAppointmentSubmit = async (e) => {
    e.preventDefault()
    if (!appointmentForm.doctorTimeSlotId || !appointmentForm.appointmentNumber) return

    const payload = {
      patientName: appointmentForm.patientName,
      patientPhone: appointmentForm.patientPhone,
      patientEmail: appointmentForm.patientEmail,
      appointmentType: appointmentForm.appointmentType,
      reason: appointmentForm.reason,
      notes: appointmentForm.notes,
      doctorTimeSlotId: Number(appointmentForm.doctorTimeSlotId),
      appointmentNumber: Number(appointmentForm.appointmentNumber),
    }
    await createAppointment(payload)
    closeAppointmentModal()
    setAppointmentForm(emptyAppointment)
  }

  const handleSlotSubmit = async (e) => {
    e.preventDefault()
    await createSlot({
      doctorName: slotForm.doctorName,
      specialty: slotForm.specialty,
      startAt: new Date(`${slotForm.date}T${slotForm.startTime}`).toISOString(),
      endAt: new Date(`${slotForm.date}T${slotForm.endTime}`).toISOString(),
      capacity: Number(slotForm.capacity),
    })
    setSlotOpen(false)
    setSlotForm(emptySlot)
  }

  const handleCancel = async (e) => {
    e.preventDefault()
    if (!cancelTarget) return
    await cancelAppointment(cancelTarget.appointmentId, cancelReason)
    setCancelTarget(null)
    setCancelReason('')
  }

  const columns = [
    {
      key: 'patient',
      label: 'Patient',
      render: (a) => (
        <div className="appt-patient-cell">
          <span className="appt-avatar">{getInitials(a.patientName)}</span>
          <div>
            <strong>{a.patientName}</strong>
            <span>{a.patientPhone}</span>
          </div>
        </div>
      ),
    },
    {
      key: 'number',
      label: 'No. / Time',
      render: (a) => (
        <div className="appt-number-cell">
          <strong>#{a.appointmentNumber || '-'}</strong>
          <span>{formatTime(a.estimatedStartAt || a.startAt)}</span>
        </div>
      ),
    },
    {
      key: 'doctor',
      label: 'Doctor / Slot',
      render: (a) => (
        <div className="appt-doctor-cell">
          <strong><Stethoscope size={13} /> {a.doctorName}</strong>
          <span>{a.specialty} · {formatDateTime(a.startAt)}-{formatTime(a.endAt)}</span>
        </div>
      ),
    },
    { key: 'appointmentType', label: 'Type' },
    { key: 'reason', label: 'Reason' },
    { key: 'status', label: 'Status', render: (a) => <StatusPill status={a.status} /> },
    {
      key: 'actions',
      label: '',
      align: 'right',
      render: (a) => (
        <div className="appt-actions">
          <select
            className="appt-inline-select"
            value={a.status}
            onChange={(e) => updateStatus(a.appointmentId, e.target.value)}
            disabled={saving || a.status === 'Cancelled' || a.status === 'Completed'}
            aria-label={`Update status for ${a.patientName}`}
          >
            {STATUSES.map(s => <option key={s} value={s}>{s}</option>)}
          </select>
          <Button variant="danger" size="sm" onClick={() => setCancelTarget(a)} disabled={a.status === 'Cancelled' || a.status === 'Completed'}>
            Cancel
          </Button>
        </div>
      ),
    },
  ]

  return (
    <div className="page-wrapper">
      <div className="page-header">
        <div className="appt-header">
          <div>
            <h1 className="page-title">Appointments</h1>
            <p className="page-subtitle">Admin scheduling, doctor time slots, capacity, and patient visit workflow</p>
          </div>
          <div className="appt-header__actions">
            <Button variant="secondary" icon={RefreshCw} onClick={refetch}>Refresh</Button>
            <Button variant="secondary" icon={CalendarClock} onClick={() => setSlotOpen(true)}>Add Slot</Button>
            <Button variant="primary" icon={Plus} onClick={() => setAppointmentOpen(true)}>New Appointment</Button>
          </div>
        </div>
      </div>

      <div className="appt-stats animate-fade-in">
        {stats.map(s => (
          <div key={s.label} className="appt-stat">
            <span className="appt-stat__val" style={{ color: s.color }}>{s.value}</span>
            <span className="appt-stat__label">{s.label}</span>
          </div>
        ))}
      </div>

      <div className="appt-panel">
        <div className="appt-toolbar">
          <label className="appt-search">
            <Search size={15} />
            <input type="search" value={search} onChange={e => setSearch(e.target.value)} placeholder="Search patient, phone, doctor, reason" />
          </label>
          <select value={status} onChange={e => setStatus(e.target.value)} className="appt-filter">
            <option value="all">All Statuses</option>
            {STATUSES.map(s => <option key={s} value={s}>{s}</option>)}
          </select>
          <input type="date" value={date} onChange={e => setDate(e.target.value)} className="appt-filter" />
        </div>

        {error && (
          <div className="appt-error">
            <AlertTriangle size={16} />
            <span>{error} - showing demo appointment data</span>
          </div>
        )}

        <Table columns={columns} data={appointments} loading={loading} emptyMessage="No appointments match the current filters" />
      </div>

      <div className="appt-slots">
        <div className="appt-section__header">
          <Clock size={16} />
          <h3>Doctor Time Slots</h3>
          <span className="appt-section__count">{slots.length}</span>
        </div>
        <div className="appt-slot-grid">
          {slots.map(slot => (
            <div className="appt-slot" key={slot.doctorTimeSlotId}>
              <div>
                <strong>{slot.doctorName}</strong>
                <span>{slot.specialty}</span>
              </div>
              <p>{formatDateTime(slot.startAt)}-{formatTime(slot.endAt)}</p>
              <meter min="0" max={slot.capacity} value={slot.bookedCount} />
              {slot.nextAppointmentNumber > 0 && (
                <small>Next #{slot.nextAppointmentNumber} at {formatTime(slot.nextEstimatedStartAt)}</small>
              )}
              <small>{slot.bookedCount}/{slot.capacity} booked · {slot.availableCount ?? slot.capacity - slot.bookedCount} available</small>
            </div>
          ))}
        </div>
      </div>

      <Modal open={appointmentOpen} onClose={closeAppointmentModal} title="Create Appointment" subtitle="Book on behalf of a patient" size="lg" id="appointment-form-modal">
        <form className="appt-form" onSubmit={handleAppointmentSubmit}>
          <label>Search or Select Doctor
            <input
              required
              list="appointment-doctors"
              value={appointmentForm.doctorName}
              onChange={e => {
                setAppointmentForm({ ...appointmentForm, doctorName: e.target.value, doctorTimeSlotId: '', appointmentNumber: '' })
                setAppointmentNumberOpen(false)
              }}
              placeholder="Type doctor name"
            />
            <datalist id="appointment-doctors">
              {doctors.map(doctor => (
                <option key={doctor.doctorName} value={doctor.doctorName}>{doctor.specialty}</option>
              ))}
            </datalist>
          </label>
          <label className="appt-form__wide">Select Appointment No
            <div className="appt-number-select" role="group" aria-label="Select appointment number" ref={appointmentNumberRef}>
              <button
                type="button"
                className="appt-number-trigger"
                onClick={() => setAppointmentNumberOpen(open => !open)}
                aria-expanded={appointmentNumberOpen}
                aria-haspopup="dialog"
                disabled={!appointmentForm.doctorName || selectedDoctorSlots.length === 0}
              >
                {appointmentForm.appointmentNumber && selectedSlot
                  ? `#${appointmentForm.appointmentNumber} · ${formatTime(getEstimatedTimeForNumber(selectedSlot, Number(appointmentForm.appointmentNumber)))} (${formatTime(selectedSlot.startAt)}-${formatTime(selectedSlot.endAt)})`
                  : 'Select appointment no'}
              </button>
              {appointmentNumberOpen && (
                <div className="appt-number-popover">
                  {selectedDoctorSlots.map(slot => {
                    const selectableNumbers = Array.from({ length: slot.capacity }, (_, i) => i + 1)
                    const bookedNumbers = getBookedNumbersForSlot(slot)
                    return (
                      <div className="appt-number-slot-group" key={slot.doctorTimeSlotId}>
                        <p className="appt-number-slot-title">
                          {formatDateTime(slot.startAt)}-{formatTime(slot.endAt)}
                        </p>
                        <div className="appt-number-grid">
                          {selectableNumbers.map(number => {
                            const isBooked = bookedNumbers.has(number)
                            const isSelected =
                              String(appointmentForm.doctorTimeSlotId) === String(slot.doctorTimeSlotId) &&
                              String(appointmentForm.appointmentNumber) === String(number)
                            return (
                              <button
                                key={`${slot.doctorTimeSlotId}-${number}`}
                                type="button"
                                className={`appt-number-btn${isSelected ? ' is-selected' : ''}${isBooked ? ' is-booked' : ''}`}
                                onClick={() => {
                                  if (isBooked) return
                                  setAppointmentForm({
                                    ...appointmentForm,
                                    doctorTimeSlotId: String(slot.doctorTimeSlotId),
                                    appointmentNumber: String(number),
                                  })
                                  setAppointmentNumberOpen(false)
                                }}
                                disabled={isBooked}
                                aria-pressed={isSelected}
                                aria-label={`Appointment number ${number}${isBooked ? ' already booked' : ''}`}
                              >
                                #{number}
                              </button>
                            )
                          })}
                        </div>
                      </div>
                    )
                  })}
                  <small>Booked numbers are disabled.</small>
                </div>
              )}
            </div>
          </label>
          {selectedSlot && (
            <div className="appt-estimate appt-form__wide">
              <strong>Appointment #{appointmentForm.appointmentNumber || '-'}</strong>
              <span>
                Estimated patient time:{' '}
                {appointmentForm.appointmentNumber
                  ? formatDateTime(getEstimatedTimeForNumber(selectedSlot, Number(appointmentForm.appointmentNumber)))
                  : 'Select a number'}
              </span>
            </div>
          )}
          <label>Patient Name
            <input required value={appointmentForm.patientName} onChange={e => setAppointmentForm({ ...appointmentForm, patientName: e.target.value })} />
          </label>
          <label>Patient Phone
            <input required value={appointmentForm.patientPhone} onChange={e => setAppointmentForm({ ...appointmentForm, patientPhone: e.target.value })} />
          </label>
          <label>Patient Email
            <input type="email" value={appointmentForm.patientEmail} onChange={e => setAppointmentForm({ ...appointmentForm, patientEmail: e.target.value })} />
          </label>
          <label>Appointment Type
            <select value={appointmentForm.appointmentType} onChange={e => setAppointmentForm({ ...appointmentForm, appointmentType: e.target.value })}>
              {TYPES.map(t => <option key={t} value={t}>{t}</option>)}
            </select>
          </label>
          <label className="appt-form__wide">Reason
            <textarea required rows="3" value={appointmentForm.reason} onChange={e => setAppointmentForm({ ...appointmentForm, reason: e.target.value })} />
          </label>
          <label className="appt-form__wide">Notes
            <textarea rows="2" value={appointmentForm.notes} onChange={e => setAppointmentForm({ ...appointmentForm, notes: e.target.value })} />
          </label>
          <div className="appt-form__actions">
            <Button variant="secondary" onClick={closeAppointmentModal}>Close</Button>
            <Button type="submit" loading={saving}>Create Appointment</Button>
          </div>
        </form>
      </Modal>

      <Modal open={slotOpen} onClose={() => setSlotOpen(false)} title="Add Doctor Time Slot" subtitle="Used by admin until doctor module is ready" size="md" id="slot-form-modal">
        <form className="appt-form" onSubmit={handleSlotSubmit}>
          <label>Doctor Name
            <input required value={slotForm.doctorName} onChange={e => setSlotForm({ ...slotForm, doctorName: e.target.value })} placeholder="Dr. Name" />
          </label>
          <label>Specialty
            <input required value={slotForm.specialty} onChange={e => setSlotForm({ ...slotForm, specialty: e.target.value })} placeholder="General Medicine" />
          </label>
          <label>Date
            <input required type="date" value={slotForm.date} onChange={e => setSlotForm({ ...slotForm, date: e.target.value })} />
          </label>
          <label>Start Time
            <input required type="time" value={slotForm.startTime} onChange={e => setSlotForm({ ...slotForm, startTime: e.target.value })} />
          </label>
          <label>End Time
            <input required type="time" value={slotForm.endTime} onChange={e => setSlotForm({ ...slotForm, endTime: e.target.value })} />
          </label>
          <label>Appointment Capacity
            <input required type="number" min="1" max="100" value={slotForm.capacity} onChange={e => setSlotForm({ ...slotForm, capacity: e.target.value })} />
          </label>
          <p className="appt-form__hint appt-form__wide">
            Appointment times are automatically estimated by dividing the selected time range by capacity.
          </p>
          <div className="appt-form__actions appt-form__wide">
            <Button variant="secondary" onClick={() => setSlotOpen(false)}>Close</Button>
            <Button type="submit" loading={saving}>Save Slot</Button>
          </div>
        </form>
      </Modal>

      <Modal open={Boolean(cancelTarget)} onClose={() => setCancelTarget(null)} title="Cancel Appointment" subtitle="A reason is required for audit history" size="sm" id="cancel-appointment-modal">
        <form className="appt-form appt-form--single" onSubmit={handleCancel}>
          <label>Cancellation Reason
            <textarea required rows="4" value={cancelReason} onChange={e => setCancelReason(e.target.value)} />
          </label>
          <div className="appt-form__actions">
            <Button variant="secondary" onClick={() => setCancelTarget(null)}>Close</Button>
            <Button variant="danger" type="submit" loading={saving}>Cancel Appointment</Button>
          </div>
        </form>
      </Modal>
    </div>
  )
}

function StatusPill({ status }) {
  const config = STATUS_CONFIG[status] || STATUS_CONFIG.Requested
  const Icon = config.icon
  return (
    <span className="appt-status" style={{ background: config.bg, color: config.color }}>
      <Icon size={12} />
      {status}
    </span>
  )
}

function getInitials(name = '') {
  return name.split(' ').filter(Boolean).map(part => part[0]).join('').slice(0, 2).toUpperCase()
}

function formatDateTime(value) {
  if (!value) return ''
  return new Intl.DateTimeFormat('en-LK', { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' }).format(new Date(value))
}

function formatTime(value) {
  if (!value) return ''
  return new Intl.DateTimeFormat('en-LK', { hour: '2-digit', minute: '2-digit' }).format(new Date(value))
}

function getEstimatedTimeForNumber(slot, appointmentNumber) {
  const startAt = new Date(slot.startAt)
  const endAt = new Date(slot.endAt)
  const totalMs = endAt.getTime() - startAt.getTime()
  if (totalMs <= 0 || slot.capacity <= 0) return startAt

  const intervalMs = totalMs / slot.capacity
  return new Date(startAt.getTime() + ((appointmentNumber - 1) * intervalMs))
}
