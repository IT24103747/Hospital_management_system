import { useParams, useNavigate } from 'react-router-dom'
import { ArrowLeft, Phone, Mail, MapPin, Droplets, User, Calendar, Shield } from 'lucide-react'
import Button from '../../../components/Button'
import { BloodGroupBadge } from '../../../components/Badge'
import { MOCK_PATIENTS } from '../services/patientApi'
import { calculateAge, formatDate, getInitials, nameToGradient } from '../../../lib/utils'
import './PatientDetailPage.css'

export default function PatientDetailPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const patient = MOCK_PATIENTS.find(p => p.patientId === Number(id))

  if (!patient) {
    return (
      <div className="page-wrapper">
        <p className="text-muted">Patient not found.</p>
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
