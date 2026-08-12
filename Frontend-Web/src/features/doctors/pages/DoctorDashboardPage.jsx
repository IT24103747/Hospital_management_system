import { BadgeCheck, Stethoscope, UserRound } from 'lucide-react'
import { Link } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'

export default function DoctorDashboardPage() {
  const { user } = useAuth()
  return <div className="page-wrapper">
    <div className="page-header"><h1 className="page-title">Doctor Dashboard</h1><p className="page-subtitle">Welcome to your MediCore workspace</p></div>
    <div className="grid-cols-3">
      <section className="glass-card profile-panel"><Stethoscope size={30}/><h3>Dr. {user.fullName}</h3><p>Your approved doctor account is active.</p></section>
      <section className="glass-card profile-panel"><BadgeCheck size={30}/><h3>Registration Approved</h3><p>You can securely use doctor services.</p></section>
      <Link to="/doctor/profile" className="glass-card profile-panel"><UserRound size={30}/><h3>View Profile</h3><p>Review or update your name and phone number.</p></Link>
    </div>
  </div>
}
