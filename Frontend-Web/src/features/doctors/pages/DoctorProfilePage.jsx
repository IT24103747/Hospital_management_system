import { Edit3, Save, X } from 'lucide-react'
import { useEffect, useState } from 'react'
import Button from '../../../components/Button'
import { useAuth } from '../../auth/AuthContext'
import { getMyDoctorProfile, updateMyDoctorProfile } from '../services/doctorApi'
import './DoctorProfilePage.css'

const editableFields = profile => ({
  firstName: profile.firstName || '',
  lastName: profile.lastName || '',
  phoneNumber: profile.phoneNumber || '',
})

export default function DoctorProfilePage() {
  const { updateUser } = useAuth()
  const [profile, setProfile] = useState(null)
  const [form, setForm] = useState({ firstName: '', lastName: '', phoneNumber: '' })
  const [editing, setEditing] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    getMyDoctorProfile()
      .then(data => {
        setProfile(data)
        setForm(editableFields(data))
      })
      .catch(requestError => setError(requestError.response?.data?.message || 'Unable to load profile.'))
  }, [])

  const beginEditing = () => {
    setForm(editableFields(profile))
    setError('')
    setSuccess('')
    setEditing(true)
  }

  const cancelEditing = () => {
    setForm(editableFields(profile))
    setError('')
    setEditing(false)
  }

  const changeField = (field, value) => setForm(current => ({ ...current, [field]: value }))

  const save = async () => {
    if (!editing) return

    setError('')
    setSuccess('')
    if (!form.firstName.trim() || !form.lastName.trim()) {
      setError('First name and second name are required.')
      return
    }
    if (!/^(?:07\d{8}|\+947\d{8})$/.test(form.phoneNumber.replace(/[ -]/g, ''))) {
      setError('Enter a valid Sri Lankan mobile number: 07XXXXXXXX or +947XXXXXXXX.')
      return
    }

    setSaving(true)
    try {
      const updated = await updateMyDoctorProfile({
        firstName: form.firstName.trim(),
        lastName: form.lastName.trim(),
        phoneNumber: form.phoneNumber.trim(),
      })
      setProfile(updated)
      setForm(editableFields(updated))
      updateUser({ fullName: updated.fullName })
      setEditing(false)
      setSuccess('Profile updated successfully.')
    } catch (requestError) {
      setError(requestError.response?.data?.message || 'Unable to update profile.')
    } finally {
      setSaving(false)
    }
  }

  if (!profile) return <div className="page-wrapper">{error || 'Loading profile...'}</div>

  return (
    <div className="page-wrapper">
      <div className="page-header">
        <h1 className="page-title">Doctor Profile</h1>
        <p className="page-subtitle">Your verified professional information</p>
      </div>

      <section className="doctor-profile-card glass-card">
        <div className="profile-grid">
          <ProfileField label="First Name" value={form.firstName} editable={editing} onChange={value => changeField('firstName', value)} />
          <ProfileField label="Second Name" value={form.lastName} editable={editing} onChange={value => changeField('lastName', value)} />
          <ProfileField label="Phone Number" value={form.phoneNumber} editable={editing} onChange={value => changeField('phoneNumber', value)} />
          <ProfileField label="Email" value={profile.email} />
          <ProfileField label="NIC" value={profile.nic} />
          <ProfileField label="Specialization" value={profile.specialization} />
          <ProfileField label="SLMC License Number" value={profile.slmcLicenseNumber} />
          <ProfileField label="Registration Status" value={profile.registrationStatus} />
        </div>

        {error && <p className="profile-message profile-message--error">{error}</p>}
        {success && <p className="profile-message profile-message--success">{success}</p>}

        <div className="profile-actions">
          {!editing ? (
            <Button type="button" variant="primary" icon={Edit3} onClick={beginEditing}>Edit</Button>
          ) : (
            <>
              <Button type="button" variant="primary" icon={Save} loading={saving} onClick={save}>Save Changes</Button>
              <Button type="button" variant="outline" icon={X} disabled={saving} onClick={cancelEditing}>Cancel</Button>
            </>
          )}
        </div>
      </section>
    </div>
  )
}

function ProfileField({ label, value, editable = false, onChange }) {
  return (
    <label className={`profile-field ${editable ? 'profile-field--editable' : 'profile-field--locked'}`}>
      <span>{label}</span>
      <input
        value={value || ''}
        readOnly={!editable}
        required={editable}
        aria-readonly={!editable}
        onChange={event => editable && onChange?.(event.target.value)}
      />
    </label>
  )
}
