import { Check, Search, UserCheck, UserX, X } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import Button from '../../../components/Button'
import { approveDoctor, declineDoctor, getDoctorRegistrations } from '../services/doctorApi'
import './DoctorsPage.css'

export default function DoctorsPage() {
  const [doctors, setDoctors] = useState([])
  const [status, setStatus] = useState('Pending')
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const load = async () => {
    setLoading(true); setError('')
    try { setDoctors(await getDoctorRegistrations(status)) }
    catch (requestError) { setError(requestError.response?.data?.message || 'Unable to load doctor requests.') }
    finally { setLoading(false) }
  }
  useEffect(() => { load() }, [status])
  const visible = useMemo(() => doctors.filter(d => `${d.fullName} ${d.email} ${d.specialization} ${d.slmcLicenseNumber}`.toLowerCase().includes(search.toLowerCase())), [doctors, search])

  const review = async (doctor, approve) => {
    const reason = approve ? '' : window.prompt('Optional reason for declining this registration:', '')
    if (!approve && reason === null) return
    try { approve ? await approveDoctor(doctor.doctorId) : await declineDoctor(doctor.doctorId, reason); await load() }
    catch (requestError) { setError(requestError.response?.data?.message || 'Unable to review request.') }
  }

  return <div className="page-wrapper">
    <div className="page-header"><h1 className="page-title">Doctor Management</h1><p className="page-subtitle">Review doctor registrations and manage approved medical staff</p></div>
    <div className="doctor-toolbar">
      <div className="doctors__search-wrap"><Search size={15} /><input className="patients__search" value={search} onChange={e => setSearch(e.target.value)} placeholder="Search doctors" /></div>
      <div className="doctor-tabs">{['Pending','Approved','Declined'].map(value => <button key={value} onClick={() => setStatus(value)} className={status === value ? 'active' : ''}>{value}</button>)}</div>
    </div>
    {error && <p className="form-error">{error}</p>}
    {loading ? <p>Loading doctor registrations...</p> : visible.length === 0 ? <div className="doctor-empty">No {status.toLowerCase()} doctor registrations.</div> :
      <div className="doctor-request-grid">{visible.map(doctor => <article className="doctor-request glass-card" key={doctor.doctorId}>
        <div className="doctor-request__heading"><div className="doctor-card__avatar">{doctor.firstName[0]}{doctor.lastName[0]}</div><div><h3>Dr. {doctor.fullName}</h3><span className={`request-status status-${doctor.registrationStatus.toLowerCase()}`}>{doctor.registrationStatus}</span></div></div>
        <dl><Info label="Email" value={doctor.email}/><Info label="NIC" value={doctor.nic}/><Info label="Phone" value={doctor.phoneNumber}/><Info label="Specialization" value={doctor.specialization}/><Info label="SLMC License" value={doctor.slmcLicenseNumber}/><Info label="Submitted" value={new Date(doctor.createdAt).toLocaleString()}/></dl>
        {doctor.declineReason && <p className="decline-reason"><strong>Reason:</strong> {doctor.declineReason}</p>}
        {doctor.registrationStatus === 'Pending' && <div className="doctor-request__actions"><Button variant="primary" icon={Check} onClick={() => review(doctor, true)}>Approve</Button><Button variant="danger" icon={X} onClick={() => review(doctor, false)}>Decline</Button></div>}
        {doctor.registrationStatus === 'Approved' && <div className="review-result"><UserCheck size={18}/> Login enabled</div>}
        {doctor.registrationStatus === 'Declined' && <div className="review-result declined"><UserX size={18}/> Login disabled</div>}
      </article>)}</div>}
  </div>
}

function Info({ label, value }) { return <div><dt>{label}</dt><dd>{value}</dd></div> }
