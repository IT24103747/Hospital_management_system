import { useEffect, useMemo, useRef, useState } from 'react'
import { AlertTriangle, CalendarClock, CheckCircle, Clock, Pencil, Plus, RefreshCw, Search, Stethoscope, X, XCircle } from 'lucide-react'
import Button from '../../../components/Button'
import Modal from '../../../components/Modal'
import Table from '../../../components/Table'
import { useDebounce } from '../../../hooks/useDebounce'
import { useAppointments } from '../hooks/useAppointments'
import './AppointmentsPage.css'

const STATUS_FILTERS = ['Confirmed', 'Completed', 'Cancelled']
const STATUS_ACTIONS = ['Confirmed', 'Completed']
const TYPES = ['Consultation', 'Follow-up', 'Check-up', 'Procedure Review', 'Emergency']
const SPECIALTIES = ['Cardiology', 'Dermatology', 'General Medicine', 'Neurology', 'Ophthalmology', 'Orthopedics', 'Pediatrics']
const DEFAULT_CONSULTATION_FEE = 2500
const SPECIALTY_CONSULTATION_FEES = {
  Cardiology: 4500,
  Dermatology: 3500,
  'General Medicine': 2500,
  Neurology: 5000,
  Ophthalmology: 3000,
  Orthopedics: 4000,
  Pediatrics: 3000,
}

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
  patientId: '',
  patientName: '',
  patientAge: '',
  patientPhone: '',
  patientEmail: '',
  appointmentType: 'Consultation',
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
  const [status, setStatus] = useState('Confirmed')
  const [date, setDate] = useState('')
  const [appointmentOpen, setAppointmentOpen] = useState(false)
  const [editTarget, setEditTarget] = useState(null)
  const [appointmentNumberOpen, setAppointmentNumberOpen] = useState(false)
  const [slotOpen, setSlotOpen] = useState(false)
  const [cancelTarget, setCancelTarget] = useState(null)
  const [cancelReason, setCancelReason] = useState('')
  const [appointmentForm, setAppointmentForm] = useState(emptyAppointment)
  const [slotForm, setSlotForm] = useState(emptySlot)
  const appointmentNumberRef = useRef(null)
  const debouncedSearch = useDebounce(search, 300)

  const apiStatus = status === 'Confirmed' ? 'all' : status
  const filters = useMemo(() => ({ search: debouncedSearch, status: apiStatus, date, sortBy: 'startAt', sortDirection: 'asc' }), [apiStatus, debouncedSearch, date])
  const {
    appointments,
    slots,
    doctors: doctorDirectory,
    patients,
    loading,
    saving,
    error,
    refetch,
    createAppointment,
    updateAppointment,
    createSlot,
    updateStatus,
    cancelAppointment,
  } = useAppointments(filters)

  const today = new Date().toISOString().slice(0, 10)
  const stats = [
    { label: "Today's Appointments", value: appointments.filter(a => a.startAt?.slice(0, 10) === today).length, icon: CalendarClock, tone: 'primary' },
    { label: 'Available Slots', value: slots.reduce((sum, s) => sum + Math.max(0, (s.availableCount ?? s.capacity - s.bookedCount)), 0), icon: Clock, tone: 'blue' },
    { label: 'Confirmed', value: appointments.filter(a => getDisplayStatus(a.status) === 'Confirmed').length, icon: CheckCircle, tone: 'success' },
    { label: 'Completed', value: appointments.filter(a => a.status === 'Completed').length, icon: Stethoscope, tone: 'purple' },
  ]
  const doctors = useMemo(() => {
    if (doctorDirectory.length > 0) return doctorDirectory

    const byName = new Map()
    slots.forEach(slot => {
      if (!byName.has(slot.doctorName)) byName.set(slot.doctorName, slot.specialty)
    })
    return Array.from(byName, ([doctorName, specialty]) => ({ doctorName, specialty }))
  }, [doctorDirectory, slots])
  const selectedDoctorSlots = useMemo(() => {
    const term = appointmentForm.doctorName.trim().toLowerCase()
    if (!term) return []
    return slots.filter(slot =>
      slot.doctorName.toLowerCase() === term &&
      (
        (slot.availableCount ?? slot.capacity - slot.bookedCount) > 0 ||
        String(slot.doctorTimeSlotId) === String(appointmentForm.doctorTimeSlotId)
      ))
  }, [appointmentForm.doctorName, appointmentForm.doctorTimeSlotId, slots])
  const selectedSlot = slots.find(slot => String(slot.doctorTimeSlotId) === String(appointmentForm.doctorTimeSlotId))
  const selectedPatient = patients.find(patient => String(patient.patientId) === String(appointmentForm.patientId))
  const selectedDoctor = doctors.find(doctor => doctor.doctorName.toLowerCase() === appointmentForm.doctorName.trim().toLowerCase())
  const selectedConsultationFee = getConsultationFee({
    doctor: selectedDoctor,
    slot: selectedSlot,
    specialty: selectedSlot?.specialty || selectedDoctor?.specialty,
  })
  const filteredAppointments = useMemo(() => {
    const term = debouncedSearch.trim().toLowerCase()
    return appointments.filter(appointment => {
      const matchesSearch = !term || [
        appointment.patientName,
        appointment.patientPhone,
        appointment.patientEmail,
        appointment.doctorName,
        appointment.specialty,
      ].some(value => String(value || '').toLowerCase().includes(term))
      const displayStatus = getDisplayStatus(appointment.status)
      const matchesStatus = displayStatus === status
      const matchesDate = !date || (appointment.startAt || appointment.estimatedStartAt || '').slice(0, 10) === date
      return matchesSearch && matchesStatus && matchesDate
    })
  }, [appointments, date, debouncedSearch, status])

  const getBookedNumbersForSlot = (slot) => {
    const bySlot = appointments
      .filter(a => String(a.doctorTimeSlotId) === String(slot.doctorTimeSlotId))
      .filter(a => !editTarget || a.appointmentId !== editTarget.appointmentId)
      .filter(a => ['Requested', 'Confirmed', 'Completed', 'No-show'].includes(a.status))
      .map(a => Number(a.appointmentNumber))

    const fromSlot = (slot.bookedAppointmentNumbers || []).map(n => Number(n))
    const bookedNumbers = new Set([...bySlot, ...fromSlot])
    if (editTarget && String(editTarget.doctorTimeSlotId) === String(slot.doctorTimeSlotId)) {
      bookedNumbers.delete(Number(editTarget.appointmentNumber))
    }
    return bookedNumbers
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
    setEditTarget(null)
    setAppointmentForm(emptyAppointment)
    setAppointmentNumberOpen(false)
  }

  const openCreateAppointment = () => {
    setEditTarget(null)
    setAppointmentForm(emptyAppointment)
    setAppointmentNumberOpen(false)
    setAppointmentOpen(true)
  }

  const openEditAppointment = (appointment) => {
    const patient = patients.find(p => String(p.patientId) === String(appointment.patientId))
    setEditTarget(appointment)
    setAppointmentForm({
      doctorName: appointment.doctorName || '',
      doctorTimeSlotId: String(appointment.doctorTimeSlotId || ''),
      appointmentNumber: String(appointment.appointmentNumber || ''),
      patientId: appointment.patientId ? String(appointment.patientId) : '',
      patientName: appointment.patientName || '',
      patientAge: patient ? String(getPatientAge(patient)) : '',
      patientPhone: appointment.patientPhone || '',
      patientEmail: appointment.patientEmail || '',
      appointmentType: appointment.appointmentType || 'Consultation',
    })
    setAppointmentNumberOpen(false)
    setAppointmentOpen(true)
  }

  const handleAppointmentSubmit = async (e) => {
    e.preventDefault()
    if (!appointmentForm.doctorTimeSlotId || !appointmentForm.appointmentNumber) return

    const payload = {
      patientId: appointmentForm.patientId ? Number(appointmentForm.patientId) : null,
      patientName: appointmentForm.patientName,
      patientPhone: appointmentForm.patientPhone,
      patientEmail: appointmentForm.patientEmail,
      appointmentType: appointmentForm.appointmentType,
      consultationFee: selectedConsultationFee,
      reason: 'Appointment',
      notes: '',
      doctorTimeSlotId: Number(appointmentForm.doctorTimeSlotId),
      appointmentNumber: Number(appointmentForm.appointmentNumber),
    }
    if (editTarget) {
      await updateAppointment(editTarget.appointmentId, {
        ...payload,
        status: editTarget.status,
      })
    } else {
      await createAppointment(payload)
    }
    closeAppointmentModal()
  }

  const handlePatientNameChange = (value) => {
    const matchedPatient = patients.find(patient => getPatientFullName(patient).toLowerCase() === value.trim().toLowerCase())

    if (matchedPatient) {
      setAppointmentForm({
        ...appointmentForm,
        patientId: String(matchedPatient.patientId),
        patientName: getPatientFullName(matchedPatient),
        patientAge: String(getPatientAge(matchedPatient)),
        patientPhone: matchedPatient.phoneNumber || '',
        patientEmail: matchedPatient.email || '',
      })
      return
    }

    setAppointmentForm({
      ...appointmentForm,
      patientId: '',
      patientName: value,
    })
  }

  const handleSlotSubmit = async (e) => {
    e.preventDefault()
    const doctorName = slotForm.doctorName.trim()
    const specialty = slotForm.specialty.trim()
    const resolvedDoctor = doctors.find(doctor => doctor.doctorName.toLowerCase() === doctorName.toLowerCase())
      || doctors.find(doctor => doctor.doctorName.toLowerCase().includes(doctorName.toLowerCase()))

    await createSlot({
      doctorName,
      specialty: resolvedDoctor?.specialty || specialty,
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
      key: 'date',
      label: 'Date',
      render: (a) => formatDateOnly(a.estimatedStartAt || a.startAt),
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
    {
      key: 'consultationFee',
      label: 'Fee',
      render: (a) => formatCurrency(a.consultationFee ?? getConsultationFee({
        doctor: doctors.find(doctor => doctor.doctorName.toLowerCase() === String(a.doctorName || '').toLowerCase()),
        slot: a,
        specialty: a.specialty,
      })),
    },
    { key: 'status', label: 'Status', render: (a) => <StatusPill status={getDisplayStatus(a.status)} /> },
    {
      key: 'actions',
      label: '',
      align: 'right',
      render: (a) => (
        <div className="appt-actions">
          <select
            className="appt-inline-select"
            value={getDisplayStatus(a.status)}
            onChange={(e) => updateStatus(a.appointmentId, e.target.value)}
            disabled={saving || a.status === 'Cancelled' || a.status === 'Completed'}
            aria-label={`Update status for ${a.patientName}`}
          >
            {STATUS_ACTIONS.map(s => <option key={s} value={s}>{s}</option>)}
          </select>
          <Button
            variant="secondary"
            size="sm"
            icon={Pencil}
            onClick={() => openEditAppointment(a)}
            disabled={a.status === 'Cancelled' || a.status === 'Completed'}
            className="appt-icon-btn"
            aria-label={`Edit appointment for ${a.patientName}`}
          />
          <Button
            variant="danger"
            size="sm"
            icon={X}
            onClick={() => setCancelTarget(a)}
            disabled={a.status === 'Cancelled' || a.status === 'Completed'}
            className="appt-icon-btn"
            aria-label={`Cancel appointment for ${a.patientName}`}
          />
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
            <Button variant="primary" icon={Plus} onClick={openCreateAppointment}>New Appointment</Button>
          </div>
        </div>
      </div>

      <div className="appt-stats animate-fade-in">
        {stats.map(s => (
          <div key={s.label} className={`appt-stat appt-stat--${s.tone}`}>
            <span className="appt-stat__icon"><s.icon size={18} /></span>
            <div>
              <span className="appt-stat__label">{s.label}</span>
              <strong className="appt-stat__val">{s.value}</strong>
            </div>
          </div>
        ))}
      </div>

      <div className="appt-panel">
        <div className="appt-toolbar">
          <label className="appt-search">
            <Search size={15} />
            <input type="search" value={search} onChange={e => setSearch(e.target.value)} placeholder="Search patient, phone, doctor" />
          </label>
          <select value={status} onChange={e => setStatus(e.target.value)} className="appt-filter">
            {STATUS_FILTERS.map(s => <option key={s} value={s}>{s}</option>)}
          </select>
          <input type="date" value={date} onChange={e => setDate(e.target.value)} className="appt-filter" />
        </div>

        {error && (
          <div className="appt-error">
            <AlertTriangle size={16} />
            <span>{error}</span>
          </div>
        )}

        <Table columns={columns} data={filteredAppointments} loading={loading} emptyMessage="No appointments match the current filters" />
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

      <Modal
        open={appointmentOpen}
        onClose={closeAppointmentModal}
        title={editTarget ? 'Edit Appointment' : 'Create Appointment'}
        subtitle={editTarget ? 'Update appointment details' : 'Book on behalf of a patient'}
        size="lg"
        id="appointment-form-modal"
      >
        <form className="appt-form" onSubmit={handleAppointmentSubmit}>
          <div className="appt-form__wide appt-top-row">
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
            <label>Select Appointment No
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
          </div>
          {selectedSlot && (
            <div className="appt-estimate appt-form__wide">
              <div>
                <strong>Appointment #{appointmentForm.appointmentNumber || '-'}</strong>
                <span>
                  Estimated patient time:{' '}
                  {appointmentForm.appointmentNumber
                    ? formatDateTime(getEstimatedTimeForNumber(selectedSlot, Number(appointmentForm.appointmentNumber)))
                    : 'Select a number'}
                </span>
              </div>
              <div className="appt-fee">
                <span>Consultation fee</span>
                <strong>{formatCurrency(selectedConsultationFee)}</strong>
              </div>
            </div>
          )}
          <label>Patient Name
            <input
              required
              list="appointment-patients"
              value={appointmentForm.patientName}
              onChange={e => handlePatientNameChange(e.target.value)}
              placeholder="Select existing patient or type a new name"
            />
            <datalist id="appointment-patients">
              {patients.map(patient => (
                <option key={patient.patientId} value={getPatientFullName(patient)}>
                  {patient.phoneNumber}
                </option>
              ))}
            </datalist>
          </label>
          <label>Patient Age
            <input
              type="number"
              min="0"
              value={appointmentForm.patientAge}
              onChange={e => setAppointmentForm({ ...appointmentForm, patientAge: e.target.value })}
              readOnly={Boolean(selectedPatient)}
            />
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
          <div className="appt-form__actions">
            <Button variant="secondary" onClick={closeAppointmentModal}>Close</Button>
            <Button type="submit" loading={saving}>{editTarget ? 'Save Changes' : 'Create Appointment'}</Button>
          </div>
        </form>
      </Modal>

      <Modal open={slotOpen} onClose={() => setSlotOpen(false)} title="Add Doctor Time Slot" subtitle="Add on behalf of a doctor" size="md" id="slot-form-modal">
        <form className="appt-form" onSubmit={handleSlotSubmit}>
          <label>Doctor Name
            <input
              required
              list="doctor-directory"
              value={slotForm.doctorName}
              onChange={e => {
                const value = e.target.value
                const matchedDoctor = doctors.find(doctor => doctor.doctorName.toLowerCase() === value.toLowerCase())
                setSlotForm({
                  ...slotForm,
                  doctorName: value,
                  specialty: matchedDoctor?.specialty || '',
                })
              }}
              placeholder="Search doctor name"
            />
            <datalist id="doctor-directory">
              {doctors.map(doctor => (
                <option key={doctor.doctorName} value={doctor.doctorName}>{doctor.specialty}</option>
              ))}
            </datalist>
          </label>
          <label>Specialty
            <input
              required
              list="appointment-specialties"
              value={slotForm.specialty}
              onChange={e => setSlotForm({ ...slotForm, specialty: e.target.value })}
              placeholder="Select or type specialty"
            />
            <datalist id="appointment-specialties">
              {SPECIALTIES.map(specialty => (
                <option key={specialty} value={specialty} />
              ))}
            </datalist>
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

function getDisplayStatus(status) {
  return status === 'Requested' || status === 'No-show' ? 'Confirmed' : status
}

function getConsultationFee({ doctor, slot, specialty }) {
  const fee = doctor?.consultationFee ?? slot?.consultationFee
  if (Number.isFinite(Number(fee)) && Number(fee) >= 0) return Number(fee)

  const resolvedSpecialty = specialty || doctor?.specialty || slot?.specialty
  return SPECIALTY_CONSULTATION_FEES[resolvedSpecialty] ?? DEFAULT_CONSULTATION_FEE
}

function formatCurrency(value) {
  return new Intl.NumberFormat('en-LK', {
    style: 'currency',
    currency: 'LKR',
    maximumFractionDigits: 0,
  }).format(Number(value) || 0)
}

function getInitials(name = '') {
  return name.split(' ').filter(Boolean).map(part => part[0]).join('').slice(0, 2).toUpperCase()
}

function getPatientFullName(patient) {
  return patient.fullName || [patient.firstName, patient.lastName].filter(Boolean).join(' ')
}

function getPatientAge(patient) {
  if (typeof patient.age === 'number') return patient.age
  if (!patient.dateOfBirth) return ''

  const today = new Date()
  const birth = new Date(patient.dateOfBirth)
  let age = today.getFullYear() - birth.getFullYear()
  const monthDiff = today.getMonth() - birth.getMonth()
  if (monthDiff < 0 || (monthDiff === 0 && today.getDate() < birth.getDate())) age--
  return age
}

function formatDateTime(value) {
  if (!value) return ''
  return new Intl.DateTimeFormat('en-LK', { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' }).format(new Date(value))
}

function formatDateOnly(value) {
  if (!value) return ''
  return new Intl.DateTimeFormat('en-LK', { month: 'short', day: 'numeric', year: 'numeric' }).format(new Date(value))
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
