import { Plus, Search } from 'lucide-react'
import Button from '../../../components/Button'
import './DoctorsPage.css'

const MOCK_DOCTORS = [
  { id: 1, name: 'Dr. Priyantha Jayawardena', specialty: 'Cardiologist', department: 'Cardiology', experience: '15 yrs', patients: 234, available: true, color: ['#0ea5e9','#6366f1'] },
  { id: 2, name: 'Dr. Sumedha Bandara', specialty: 'Neurologist', department: 'Neurology', experience: '12 yrs', patients: 189, available: true, color: ['#10b981','#0ea5e9'] },
  { id: 3, name: 'Dr. Menaka Weerasinghe', specialty: 'Pediatrician', department: 'Pediatrics', experience: '8 yrs', patients: 312, available: false, color: ['#f59e0b','#ef4444'] },
  { id: 4, name: 'Dr. Ruwan Herath', specialty: 'Orthopedic Surgeon', department: 'Orthopedics', experience: '20 yrs', patients: 142, available: true, color: ['#8b5cf6','#ec4899'] },
  { id: 5, name: 'Dr. Chamari Gunaratne', specialty: 'Dermatologist', department: 'Dermatology', experience: '6 yrs', patients: 276, available: true, color: ['#06b6d4','#10b981'] },
  { id: 6, name: 'Dr. Nishantha Amarasinghe', specialty: 'Ophthalmologist', department: 'Ophthalmology', experience: '11 yrs', patients: 198, available: false, color: ['#0ea5e9','#10b981'] },
]

export default function DoctorsPage() {
  return (
    <div className="page-wrapper">
      <div className="page-header">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="page-title">Doctors</h1>
            <p className="page-subtitle">Medical staff directory · {MOCK_DOCTORS.length} doctors</p>
          </div>
          <Button variant="primary" icon={Plus} id="add-doctor-btn">Add Doctor</Button>
        </div>
      </div>

      <div className="doctors__search-wrap" style={{ marginBottom: '28px' }}>
        <Search size={15} style={{ color: 'var(--text-muted)' }} />
        <input className="patients__search" placeholder="Search doctors by name or specialty…" id="doctor-search" />
      </div>

      <div className="doctors__grid stagger-children animate-fade-in">
        {MOCK_DOCTORS.map(doc => (
          <div key={doc.id} className="doctor-card glass-card" id={`doctor-${doc.id}`}>
            <div className="doctor-card__top">
              <div
                className="doctor-card__avatar"
                style={{ background: `linear-gradient(135deg, ${doc.color[0]}, ${doc.color[1]})` }}
              >
                {doc.name.split(' ').filter(w => !w.startsWith('Dr')).map(w => w[0]).join('').slice(0,2)}
              </div>
              <span className={`doctor-card__status ${doc.available ? 'doctor-card__status--available' : 'doctor-card__status--busy'}`}>
                <span className="doctor-card__dot" />
                {doc.available ? 'Available' : 'Busy'}
              </span>
            </div>
            <h4 className="doctor-card__name">{doc.name}</h4>
            <p className="doctor-card__specialty">{doc.specialty}</p>
            <div className="doctor-card__stats">
              <div className="doctor-card__stat">
                <span className="doctor-card__stat-val">{doc.patients}</span>
                <span className="doctor-card__stat-lbl">Patients</span>
              </div>
              <div className="doctor-card__stat">
                <span className="doctor-card__stat-val">{doc.experience}</span>
                <span className="doctor-card__stat-lbl">Experience</span>
              </div>
              <div className="doctor-card__stat">
                <span className="doctor-card__stat-val">{doc.department.slice(0,4)}</span>
                <span className="doctor-card__stat-lbl">Dept</span>
              </div>
            </div>
            <Button variant="outline" size="sm" fullWidth id={`book-${doc.id}`}>Book Appointment</Button>
          </div>
        ))}
      </div>
    </div>
  )
}
