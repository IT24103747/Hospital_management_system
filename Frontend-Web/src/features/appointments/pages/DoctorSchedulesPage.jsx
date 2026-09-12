import { CalendarPlus, DoorOpen, Edit3, RefreshCw, Save, Trash2, X, XCircle } from 'lucide-react'
import { useEffect, useState } from 'react'
import Button from '../../../components/Button'
import { roomApi } from '../../rooms/services/roomApi'
import { doctorScheduleApi } from '../services/doctorScheduleApi'
import '../../rooms/pages/RoomsPage.css'

const emptyForm = { appointmentDate: '', startTime: '', endTime: '', roomId: '', capacity: 1, consultationFee: '' }
const combineDateAndTime = (date, time) => new Date(`${date}T${time}`).toISOString()
const today = () => {
  const now = new Date()
  return new Date(now.getTime() - now.getTimezoneOffset() * 60000).toISOString().slice(0, 10)
}
const localParts = value => {
  const date = new Date(value)
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString()
  return { appointmentDate: local.slice(0, 10), time: local.slice(11, 16) }
}

export default function DoctorSchedulesPage() {
  const [schedules, setSchedules] = useState([])
  const [rooms, setRooms] = useState([])
  const [form, setForm] = useState(emptyForm)
  const [editingId, setEditingId] = useState(null)
  const [loading, setLoading] = useState(true)
  const [checking, setChecking] = useState(false)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')

  const loadSchedules = async () => {
    setLoading(true)
    try { setSchedules(await doctorScheduleApi.getMine()) }
    catch (requestError) { setError(requestError.response?.data?.message || 'Unable to load schedules.') }
    finally { setLoading(false) }
  }
  useEffect(() => { loadSchedules() }, [])

  const clearForm = () => {
    setForm(emptyForm)
    setRooms([])
    setEditingId(null)
  }

  const validateDateTime = () => {
    if (!form.appointmentDate || !form.startTime || !form.endTime) return 'Select the appointment date, start time, and end time first.'
    const startAt = combineDateAndTime(form.appointmentDate, form.startTime)
    const endAt = combineDateAndTime(form.appointmentDate, form.endTime)
    if (new Date(form.appointmentDate + 'T00:00') < new Date(today() + 'T00:00')) return 'Appointment date cannot be in the past.'
    if (new Date(startAt) <= new Date()) return 'Schedule start time must be in the future.'
    if (new Date(endAt) <= new Date(startAt)) return 'End time must be after start time.'
    return null
  }

  const checkRooms = async () => {
    setError(''); setSuccess(''); setRooms([])
    const validationError = validateDateTime()
    if (validationError) return setError(validationError)
    const startAt = combineDateAndTime(form.appointmentDate, form.startTime)
    const endAt = combineDateAndTime(form.appointmentDate, form.endTime)
    setChecking(true)
    try {
      const available = await roomApi.getAvailable(startAt, endAt, editingId)
      setRooms(available)
      setForm(current => ({ ...current, roomId: available.some(room => String(room.roomId) === String(current.roomId)) ? current.roomId : '' }))
      if (available.length === 0) setError('No confirmed rooms are available for this time period.')
    } catch (requestError) { setError(requestError.response?.data?.message || 'Unable to check room availability.') }
    finally { setChecking(false) }
  }

  const submit = async event => {
    event.preventDefault(); setError(''); setSuccess('')
    const validationError = validateDateTime()
    if (validationError) return setError(validationError)
    if (!form.roomId) return setError('Check availability and select a room.')
    const consultationFee = Number(form.consultationFee)
    if (!Number.isFinite(consultationFee) || consultationFee <= 0 || consultationFee > 1000000) return setError('Enter a valid consulting fee between LKR 0.01 and LKR 1,000,000.00.')
    if (!/^\d+(?:\.\d{1,2})?$/.test(String(form.consultationFee))) return setError('Consulting fee can contain a maximum of two decimal places.')
    const payload = { roomId: Number(form.roomId), startAt: combineDateAndTime(form.appointmentDate, form.startTime), endAt: combineDateAndTime(form.appointmentDate, form.endTime), capacity: Number(form.capacity), consultationFee }
    setSaving(true)
    try {
      if (editingId) await doctorScheduleApi.update(editingId, payload)
      else await doctorScheduleApi.create(payload)
      const message = editingId ? 'Schedule updated successfully.' : 'Schedule and room booking confirmed.'
      clearForm(); setSuccess(message); await loadSchedules()
    } catch (requestError) { setError(requestError.response?.data?.message || `Unable to ${editingId ? 'update' : 'create'} schedule.`) }
    finally { setSaving(false) }
  }

  const beginEdit = schedule => {
    const start = localParts(schedule.startAt)
    const end = localParts(schedule.endAt)
    setEditingId(schedule.doctorTimeSlotId)
    setForm({ appointmentDate: start.appointmentDate, startTime: start.time, endTime: end.time, roomId: String(schedule.roomId), capacity: schedule.capacity, consultationFee: String(schedule.consultationFee) })
    setRooms([{ roomId: schedule.roomId, roomNumber: schedule.roomNumber, roomName: schedule.roomName, floor: schedule.floor }])
    setError(''); setSuccess('')
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  const cancel = async schedule => {
    if (!window.confirm(`Cancel the unbooked schedule in ${schedule.roomNumber}? The room will be released.`)) return
    try { await doctorScheduleApi.cancel(schedule.doctorTimeSlotId); setSuccess('Schedule cancelled and room released.'); await loadSchedules() }
    catch (requestError) { setError(requestError.response?.data?.message || 'Unable to cancel schedule.') }
  }

  const remove = async schedule => {
    if (!window.confirm(`Permanently delete the unbooked schedule in ${schedule.roomNumber}?`)) return
    try { await doctorScheduleApi.delete(schedule.doctorTimeSlotId); setSuccess('Schedule deleted permanently.'); if (editingId === schedule.doctorTimeSlotId) clearForm(); await loadSchedules() }
    catch (requestError) { setError(requestError.response?.data?.message || 'Unable to delete schedule.') }
  }

  return <div className="page-wrapper">
    <div className="page-header"><h1 className="page-title">Appointment Schedules</h1><p className="page-subtitle">Reserve an available hospital room for your appointment schedule</p></div>
    <form className="room-form glass-card" onSubmit={submit}>
      <h3 className="room-form__wide">{editingId ? 'Edit Appointment Schedule' : 'Create Appointment Schedule'}</h3>
      <label>Appointment Date<input type="date" min={today()} required value={form.appointmentDate} onChange={e=>setForm({...form,appointmentDate:e.target.value,roomId:''})}/></label>
      <label>Start Time<input type="time" required value={form.startTime} onChange={e=>setForm({...form,startTime:e.target.value,roomId:''})}/></label>
      <label>End Time<input type="time" required value={form.endTime} onChange={e=>setForm({...form,endTime:e.target.value,roomId:''})}/></label>
      <label>Patient Capacity<input type="number" min="1" max="100" required value={form.capacity} onChange={e=>setForm({...form,capacity:e.target.value})}/></label>
      <label>Consulting Fee (LKR)<input type="number" min="0.01" max="1000000" step="0.01" required value={form.consultationFee} onChange={e=>setForm({...form,consultationFee:e.target.value})} placeholder="2500.00"/></label>
      <label className="room-form__wide">Available Room<select required value={form.roomId} onChange={e=>setForm({...form,roomId:e.target.value})}><option value="">{rooms.length ? 'Select an available room' : 'Check availability first'}</option>{rooms.map(room=><option key={room.roomId} value={room.roomId}>{room.roomNumber} — {room.roomName} ({room.floor})</option>)}</select></label>
      <div className="room-form__actions room-form__wide"><Button type="button" variant="outline" icon={RefreshCw} loading={checking} onClick={checkRooms}>Check Available Rooms</Button><Button type="submit" icon={editingId ? Save : CalendarPlus} loading={saving}>{editingId ? 'Save Changes' : 'Confirm Schedule'}</Button>{editingId && <Button type="button" variant="outline" icon={X} onClick={clearForm}>Cancel Editing</Button>}</div>
    </form>
    {error && <p className="room-message room-message--error">{error}</p>}{success && <p className="room-message room-message--success">{success}</p>}
    <h2 className="schedule-heading">My Schedules</h2>
    {loading ? <p>Loading schedules...</p> : schedules.length === 0 ? <div className="doctor-empty">You have no appointment schedules.</div> : <div className="room-grid">{schedules.map(schedule=><article className="room-card glass-card" key={schedule.doctorTimeSlotId}>
      <div className="room-card__heading"><DoorOpen/><div><h3>{schedule.roomNumber}</h3><p>{schedule.roomName} · {schedule.floor}</p></div><span className={`room-status room-status--${schedule.isActive?'available':'booked'}`}>{schedule.isActive?'Active':'Cancelled'}</span></div>
      <p><strong>Date:</strong> {new Date(schedule.startAt).toLocaleDateString()}</p><p><strong>Time:</strong> {new Date(schedule.startAt).toLocaleTimeString([], {hour:'2-digit',minute:'2-digit'})} – {new Date(schedule.endAt).toLocaleTimeString([], {hour:'2-digit',minute:'2-digit'})}</p><p><strong>Appointments:</strong> {schedule.bookedCount}/{schedule.capacity}</p><p><strong>Consulting Fee:</strong> LKR {Number(schedule.consultationFee).toLocaleString(undefined, {minimumFractionDigits:2,maximumFractionDigits:2})}</p>
      <div className="schedule-actions">
        {schedule.isActive && new Date(schedule.endAt).getTime() > Date.now() && <Button variant="outline" size="sm" icon={Edit3} onClick={()=>beginEdit(schedule)}>Edit</Button>}
        {!schedule.hasPatientBookings && <>{schedule.isActive && <Button variant="danger" size="sm" icon={XCircle} onClick={()=>cancel(schedule)}>Cancel</Button>}<Button variant="danger" size="sm" icon={Trash2} onClick={()=>remove(schedule)}>Delete</Button></>}
      </div>
      {schedule.hasPatientBookings && <p className="schedule-locked">Schedule changes notify booked patients. Booked schedules cannot be cancelled or deleted.</p>}
    </article>)}</div>}
  </div>
}
