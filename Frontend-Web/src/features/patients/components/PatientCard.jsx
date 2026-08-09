import { useState } from 'react'
import { Phone, Mail, MapPin, Edit2, Trash2, Eye, CreditCard } from 'lucide-react'
import { BloodGroupBadge } from '../../../components/Badge'
import Button from '../../../components/Button'
import { calculateAge, getInitials, nameToGradient } from '../../../lib/utils'
import './PatientCard.css'

export default function PatientCard({ patient, onEdit, onDelete, onView }) {
  const [hovered, setHovered] = useState(false)
  const [gradient] = useState(() => nameToGradient(patient.firstName))

  const fullName = `${patient.firstName} ${patient.lastName}`
  const age = calculateAge(patient.dateOfBirth)
  const initials = getInitials(patient.firstName, patient.lastName)

  return (
    <div
      className={`patient-card ${hovered ? 'patient-card--hovered' : ''}`}
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
    >
      <div
        className="patient-card__glow"
        style={{ background: `linear-gradient(135deg, ${gradient[0]}20, ${gradient[1]}20)` }}
      />

      <div className="patient-card__header">
        <div
          className="patient-card__avatar"
          style={{ background: `linear-gradient(135deg, ${gradient[0]}, ${gradient[1]})` }}
        >
          {initials}
        </div>
        <div className="patient-card__info">
          <h4 className="patient-card__name">{fullName}</h4>
          <span className="patient-card__meta">{patient.gender} · {age} yrs</span>
        </div>
        <BloodGroupBadge group={patient.bloodGroup} />
      </div>

      <div className="patient-card__details">
        <Detail icon={CreditCard} label="NIC" value={patient.nic} />
        <Detail icon={Phone} label="Phone" value={patient.phoneNumber} />
        <Detail icon={Mail} label="Email" value={patient.email} />
        <Detail icon={MapPin} label="Address" value={patient.address} />
      </div>

      <div className="patient-card__footer">
        <div className="patient-card__actions">
          <Button
            variant="secondary"
            size="sm"
            icon={Eye}
            onClick={() => onView?.(patient)}
            id={`view-patient-${patient.patientId}`}
          >
            View
          </Button>
          <Button
            variant="primary"
            size="sm"
            icon={Edit2}
            onClick={() => onEdit?.(patient)}
            id={`edit-patient-${patient.patientId}`}
          >
            Edit
          </Button>
          <Button
            variant="danger"
            size="sm"
            icon={Trash2}
            onClick={() => onDelete?.(patient)}
            id={`delete-patient-${patient.patientId}`}
          >
            Delete
          </Button>
        </div>
      </div>
    </div>
  )
}

function Detail({ icon: Icon, label, value }) {
  return (
    <div className="patient-card__detail">
      <Icon size={14} />
      <div className="patient-card__detail-content">
        <span className="patient-card__detail-label">{label}</span>
        <span className="patient-card__detail-value">{value || '—'}</span>
      </div>
    </div>
  )
}
