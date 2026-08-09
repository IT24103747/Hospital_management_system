import { useEffect, useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { ArrowLeft, Phone, Mail, MapPin, Droplets, User, Calendar, Shield, CalendarClock, Stethoscope } from 'lucide-react'
import Button from '../../../components/Button'
import { BloodGroupBadge } from '../../../components/Badge'
import { patientApi } from '../services/patientApi'
import { calculateAge, formatDate, getInitials, nameToGradient } from '../../../lib/utils'
import './PatientDetailPage.css'

export default function PatientDetailPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const [patient, setPatient] = useState(null)
  const [appointments, setAppointments] = useState([])
  const [appointmentError, setAppointmentError] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)

  useEffect(() => {
    let active = true

    const loadPatient = async () => {
      setLoading(true)
      setError(null)
      setAppointmentError(null)
      setAppointments([])
      try {
        const patientResult = await patientApi.getById(id)
        if (active) {
          setPatient(patientResult)
          try {
            const appointmentResult = await patientApi.getAppointmentHistory(id)
            if (active) setAppointments(appointmentResult)
          } catch {
            if (active) setAppointmentError('Appointment history is temporarily unavailable.')
          }
        }
      } catch (err) {
        if (active) setError(err.response?.status === 404 ? 'Patient record not found.' : 'Unable to load this patient record.')
      } finally {
        if (active) setLoading(false)
      }
    }

    loadPatient()
    return () => { active = false }
  }, [id])

  if (loading) {
    return (
      <div className="page-wrapper patient-detail__state">
        <div className="skeleton" style={{ height: '160px', borderRadius: '14px' }} />
        <div className="skeleton" style={{ height: '260px', borderRadius: '14px' }} />
      </div>
    )
  }

  if (error || !patient) {
    return (
      <div className="page-wrapper patient-detail__state">
        <p className="text-muted">{error || 'Patient record not found.'}</p>
        <Button variant="secondary" onClick={() => navigate('/patients')}>Back</Button>
      </div>
    )
  }

  const [c1, c2] = nameToGradient(patient.firstName)
  const initials = getInitials(patient.firstName, patient.lastName)

  return (
    <div className="page-wrapper">
      <Button
        variant="ghost"
        icon={ArrowLeft}
        onClick={() => navigate('/patients')}
        id="back-to-patients"
        style={{ marginBottom: '24px' }}
      >
        Back to Patients
      </Button>

      <div className="patient-detail">
        <div className="patient-detail__hero glass-card">
          <div
            className="patient-detail__avatar"
            style={{ background: `linear-gradient(135deg, ${c1}, ${c2})` }}
          >
            {initials}
          </div>
          <div className="patient-detail__hero-info">
            <span className="patient-detail__eyebrow">Patient record</span>
            <h1 className="patient-detail__name">{patient.firstName} {patient.lastName}</h1>
            <p className="patient-detail__sub">
              {patient.gender} · {calculateAge(patient.dateOfBirth)} years old · {patient.nic}
            </p>
          </div>
          <BloodGroupBadge group={patient.bloodGroup} />
        </div>

        <div className="patient-detail__sections">
          <InfoCard title="Contact Information" icon={Phone}>
            <InfoRow icon={Phone} label="Phone" value={patient.phoneNumber} />
            <InfoRow icon={Mail} label="Email" value={patient.email} />
            <InfoRow icon={MapPin} label="Address" value={patient.address} />
          </InfoCard>

          <InfoCard title="Medical Information" icon={Droplets}>
            <InfoRow icon={Droplets} label="Blood Group" value={<BloodGroupBadge group={patient.bloodGroup} />} />
            <InfoRow icon={Calendar} label="Date of Birth" value={formatDate(patient.dateOfBirth)} />
            <InfoRow icon={User} label="Gender" value={patient.gender} />
          </InfoCard>

          <InfoCard title="Emergency Contact" icon={Shield}>
            <InfoRow icon={User} label="Name" value={patient.emergencyContactName} />
            <InfoRow icon={Phone} label="Phone" value={patient.emergencyContactPhone} />
          </InfoCard>

          <InfoCard title="Record Info" icon={Calendar}>
            <InfoRow icon={Calendar} label="Registered" value={formatDate(patient.createdAt)} />
            <InfoRow icon={Calendar} label="Last Updated" value={formatDate(patient.updatedAt)} />
          </InfoCard>
        </div>

        <section className="patient-history glass-card">
          <div className="patient-history__header">
            <div className="patient-history__heading">
              <div className="info-card__icon"><CalendarClock size={16} /></div>
              <div>
                <h3 className="info-card__title">Appointment History</h3>
                <p className="patient-history__sub">Recent scheduled visits for this patient</p>
              </div>
            </div>
            <span className="patient-history__count">{appointments.length} {appointments.length === 1 ? 'visit' : 'visits'}</span>
          </div>
          {appointments.length === 0 ? (
            <p className="patient-history__empty">{appointmentError || 'No appointments have been recorded for this patient.'}</p>
          ) : (
            <div className="patient-history__list">
              {appointments.map(appointment => (
                <article className="patient-history__item" key={appointment.appointmentId}>
                  <div className="patient-history__date">
                    <strong>{formatDate(appointment.scheduledAt)}</strong>
                    <span>{new Date(appointment.scheduledAt).toLocaleTimeString('en-LK', { hour: '2-digit', minute: '2-digit' })}</span>
                  </div>
                  <div className="patient-history__visit">
                    <div className="patient-history__doctor"><Stethoscope size={15} /><strong>{appointment.doctorName}</strong></div>
                    <span>{appointment.specialty} · {appointment.appointmentType}</span>
                    <small>{appointment.reason}</small>
                  </div>
                  <span className={`patient-history__status patient-history__status--${appointment.status.toLowerCase()}`}>
                    {appointment.status}
                  </span>
                </article>
              ))}
            </div>
          )}
        </section>
      </div>
    </div>
  )
}

function InfoCard({ title, icon: Icon, children }) {
  return (
    <div className="info-card glass-card">
      <div className="info-card__header">
        <div className="info-card__icon"><Icon size={16} /></div>
        <h3 className="info-card__title">{title}</h3>
      </div>
      <div className="info-card__body">{children}</div>
    </div>
  )
}

function InfoRow({ icon: Icon, label, value }) {
  return (
    <div className="info-row">
      <span className="info-row__label">
        <Icon size={13} />
        {label}
      </span>
      <span className="info-row__value">{value || '—'}</span>
    </div>
  )
}
