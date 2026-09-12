import { useState, useEffect, useRef } from 'react'
import {
  Plus,
  X,
  Paperclip,
  Save,
  User,
  Activity,
  FileText,
  Stethoscope,
  Pill,
  Calendar,
  UploadCloud,
  FileSpreadsheet,
  Image as ImageIcon,
  CheckCircle2,
  Trash2,
} from 'lucide-react'
import Modal from '../../../components/Modal'
import Button from '../../../components/Button'

const RECORD_TYPES = ['Consultation', 'LabReport', 'DischargeSummary', 'Prescription', 'GeneralNote']
const STATUSES = ['Finalized', 'Draft', 'Archived']

export default function MedicalRecordFormModal({
  open,
  isOpen,
  onClose,
  onSubmit,
  initialData = null,
  patients = [],
  loading = false,
}) {
  const isModalOpen = open ?? isOpen ?? false
  const fileInputRef = useRef(null)

  const [formData, setFormData] = useState({
    patientId: '',
    doctorId: '',
    appointmentId: '',
    recordDate: new Date().toISOString().split('T')[0],
    recordType: 'Consultation',
    diagnosis: '',
    symptoms: '',
    treatmentPlan: '',
    prescriptionNotes: '',
    labNotes: '',
    followUpDate: '',
    status: 'Finalized',
    attachments: [],
  })

  const [validationError, setValidationError] = useState(null)
  const [isDragging, setIsDragging] = useState(false)

  useEffect(() => {
    if (initialData) {
      setFormData({
        patientId: initialData.patientId || '',
        doctorId: initialData.doctorId || '',
        appointmentId: initialData.appointmentId || '',
        recordDate: initialData.recordDate ? initialData.recordDate.split('T')[0] : '',
        recordType: initialData.recordType || 'Consultation',
        diagnosis: initialData.diagnosis || '',
        symptoms: initialData.symptoms || '',
        treatmentPlan: initialData.treatmentPlan || '',
        prescriptionNotes: initialData.prescriptionNotes || '',
        labNotes: initialData.labNotes || '',
        followUpDate: initialData.followUpDate ? initialData.followUpDate.split('T')[0] : '',
        status: initialData.status || 'Finalized',
        attachments: initialData.attachments ? [...initialData.attachments] : [],
      })
    } else {
      setFormData({
        patientId: patients.length > 0 ? patients[0].patientId : '',
        doctorId: '',
        appointmentId: '',
        recordDate: new Date().toISOString().split('T')[0],
        recordType: 'Consultation',
        diagnosis: '',
        symptoms: '',
        treatmentPlan: '',
        prescriptionNotes: '',
        labNotes: '',
        followUpDate: '',
        status: 'Finalized',
        attachments: [],
      })
    }
    setValidationError(null)
  }, [initialData, isModalOpen, patients])

  const handleFileChange = async (e) => {
    const files = e.target.files
    if (!files || files.length === 0) return
    await processSelectedFiles(files)
  }

  const processSelectedFiles = async (fileList) => {
    const newItems = []
    for (let i = 0; i < fileList.length; i++) {
      const file = fileList[i]
      const fileType = file.type || (file.name.endsWith('.pdf') ? 'application/pdf' : 'application/octet-stream')
      
      const fileDataUrl = await new Promise((resolve) => {
        const reader = new FileReader()
        reader.onload = () => resolve(reader.result)
        reader.onerror = () => resolve(`/uploads/medical-records/${file.name}`)
        reader.readAsDataURL(file)
      })

      newItems.push({
        fileName: file.name,
        fileType: fileType,
        fileUrl: fileDataUrl,
        fileSize: file.size || 1024 * 500,
      })
    }

    setFormData((prev) => ({
      ...prev,
      attachments: [...prev.attachments, ...newItems],
    }))
    setValidationError(null)
    if (fileInputRef.current) {
      fileInputRef.current.value = ''
    }
  }

  const handleDrop = (e) => {
    e.preventDefault()
    setIsDragging(false)
    if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
      processSelectedFiles(e.dataTransfer.files)
    }
  }

  const handleRemoveAttachment = (index) => {
    setFormData((prev) => ({
      ...prev,
      attachments: prev.attachments.filter((_, i) => i !== index),
    }))
  }

  const handleSubmit = async (e) => {
    e.preventDefault()
    if (!formData.patientId) {
      setValidationError('Please select a patient.')
      return
    }
    if (!formData.diagnosis.trim()) {
      setValidationError('Primary diagnosis is required.')
      return
    }
    if (!formData.symptoms.trim()) {
      setValidationError('Symptoms & clinical findings are required.')
      return
    }
    if (!formData.treatmentPlan.trim()) {
      setValidationError('Treatment plan is required.')
      return
    }

    const payload = {
      ...formData,
      patientId: parseInt(formData.patientId, 10),
      doctorId: formData.doctorId ? parseInt(formData.doctorId, 10) : null,
      appointmentId: formData.appointmentId ? parseInt(formData.appointmentId, 10) : null,
      followUpDate: formData.followUpDate ? new Date(formData.followUpDate).toISOString() : null,
      recordDate: formData.recordDate ? new Date(formData.recordDate).toISOString() : new Date().toISOString(),
      attachments: formData.attachments.map((a) => ({
        fileName: a.fileName,
        fileType: a.fileType,
        fileUrl: a.fileUrl.startsWith('blob:') ? `/uploads/medical-records/${a.fileName}` : a.fileUrl,
        fileSize: a.fileSize,
      })),
    }
    await onSubmit(payload)
  }

  const isEdit = Boolean(initialData)

  const formatFileSize = (bytes) => {
    if (!bytes) return '0 KB'
    const kb = bytes / 1024
    if (kb < 1024) return `${kb.toFixed(1)} KB`
    return `${(kb / 1024).toFixed(1)} MB`
  }

  return (
    <Modal
      open={isModalOpen}
      onClose={onClose}
      title={isEdit ? 'Edit Medical Record' : 'Create New Medical Record'}
      subtitle={
        isEdit
          ? `Updating Record #${initialData?.medicalRecordId} for ${initialData?.patientName}`
          : 'Record patient consultation, clinical diagnosis, prescriptions, and lab findings'
      }
      size="lg"
      id="medical-record-form-modal"
    >
      <form onSubmit={handleSubmit} className="patient-form mr-form-styled" id="medical-record-form">
        {validationError && (
          <div className="patient-form__error" role="alert">
            {validationError}
          </div>
        )}

        {/* Section 1: Overview & Patient Details */}
        <div className="form-section">
          <h5 className="form-section__title" style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
            <User size={15} /> Patient & Record Information
          </h5>
          <div className="form-grid form-grid--2">
            <div className="field">
              <label className="field__label">Patient <span className="field__required">*</span></label>
              {isEdit ? (
                <input
                  type="text"
                  className="field__input"
                  value={initialData.patientName || `Patient #${initialData.patientId}`}
                  disabled
                />
              ) : (
                <select
                  className="field__input field__select"
                  value={formData.patientId}
                  onChange={(e) => setFormData({ ...formData, patientId: e.target.value })}
                  required
                >
                  <option value="">Select Patient</option>
                  {patients.map((p) => (
                    <option key={p.patientId} value={p.patientId}>
                      {p.fullName || `${p.firstName} ${p.lastName}`} (NIC: {p.nic || '—'})
                    </option>
                  ))}
                </select>
              )}
            </div>

            <div className="field">
              <label className="field__label">Record Type <span className="field__required">*</span></label>
              <select
                className="field__input field__select"
                value={formData.recordType}
                onChange={(e) => setFormData({ ...formData, recordType: e.target.value })}
                required
              >
                {RECORD_TYPES.map((type) => (
                  <option key={type} value={type}>{type}</option>
                ))}
              </select>
            </div>

            <div className="field">
              <label className="field__label">Record Date <span className="field__required">*</span></label>
              <input
                type="date"
                className="field__input"
                value={formData.recordDate}
                onChange={(e) => setFormData({ ...formData, recordDate: e.target.value })}
                required
              />
            </div>

            <div className="field">
              <label className="field__label">Workflow Status <span className="field__required">*</span></label>
              <select
                className="field__input field__select"
                value={formData.status}
                onChange={(e) => setFormData({ ...formData, status: e.target.value })}
                required
              >
                {STATUSES.map((st) => (
                  <option key={st} value={st}>{st}</option>
                ))}
              </select>
            </div>
          </div>
        </div>

        {/* Section 2: Clinical Findings & Diagnosis */}
        <div className="form-section">
          <h5 className="form-section__title" style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
            <Activity size={15} /> Clinical Evaluation & Diagnosis
          </h5>
          <div className="form-grid">
            <div className="field">
              <label className="field__label">Primary Diagnosis <span className="field__required">*</span></label>
              <input
                type="text"
                className="field__input"
                placeholder="e.g. Type 2 Diabetes, Acute Bronchitis"
                value={formData.diagnosis}
                onChange={(e) => setFormData({ ...formData, diagnosis: e.target.value })}
                maxLength={500}
                required
              />
            </div>

            <div className="field">
              <label className="field__label">Symptoms & Clinical Presentation <span className="field__required">*</span></label>
              <textarea
                className="field__input"
                placeholder="Enter patient symptoms and complaints..."
                rows={3}
                value={formData.symptoms}
                onChange={(e) => setFormData({ ...formData, symptoms: e.target.value })}
                maxLength={2000}
                required
                style={{ resize: 'vertical' }}
              />
            </div>

            <div className="field">
              <label className="field__label">Treatment Plan & Medical Advice <span className="field__required">*</span></label>
              <textarea
                className="field__input"
                placeholder="Enter treatment plan and doctor advice..."
                rows={3}
                value={formData.treatmentPlan}
                onChange={(e) => setFormData({ ...formData, treatmentPlan: e.target.value })}
                maxLength={2000}
                required
                style={{ resize: 'vertical' }}
              />
            </div>
          </div>
        </div>

        {/* Section 3: Prescriptions & Lab Notes */}
        <div className="form-section">
          <h5 className="form-section__title" style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
            <Pill size={15} /> Prescriptions & Diagnostics
          </h5>
          <div className="form-grid form-grid--2">
            <div className="field">
              <label className="field__label">Prescription Notes & Dosage</label>
              <textarea
                className="field__input"
                placeholder="e.g. Paracetamol 500mg TDS x 3 days"
                rows={3}
                value={formData.prescriptionNotes}
                onChange={(e) => setFormData({ ...formData, prescriptionNotes: e.target.value })}
                maxLength={2000}
                style={{ fontFamily: 'var(--font-mono)', fontSize: '0.82rem', resize: 'vertical' }}
              />
            </div>

            <div className="field">
              <label className="field__label">Lab Observations & Test Findings</label>
              <textarea
                className="field__input"
                placeholder="e.g. Blood Sugar: 110 mg/dL, Normal ECG"
                rows={3}
                value={formData.labNotes}
                onChange={(e) => setFormData({ ...formData, labNotes: e.target.value })}
                maxLength={4000}
                style={{ resize: 'vertical' }}
              />
            </div>

            <div className="field form-grid__span-2">
              <label className="field__label">Follow-up Date (Optional)</label>
              <input
                type="date"
                className="field__input"
                value={formData.followUpDate}
                onChange={(e) => setFormData({ ...formData, followUpDate: e.target.value })}
              />
            </div>
          </div>
        </div>

        {/* Section 4: Attachments (File Browser / Local Drag & Drop) */}
        <div className="form-section">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
            <h5 className="form-section__title" style={{ margin: 0, border: 'none', display: 'flex', alignItems: 'center', gap: '6px' }}>
              <Paperclip size={15} /> Diagnostic Attachments ({formData.attachments.length})
            </h5>
            <Button
              type="button"
              variant="secondary"
              size="sm"
              onClick={() => fileInputRef.current?.click()}
            >
              <Plus size={14} /> Browse Files
            </Button>
          </div>

          {/* Hidden native file input */}
          <input
            type="file"
            ref={fileInputRef}
            style={{ display: 'none' }}
            multiple
            accept=".pdf,.png,.jpg,.jpeg,.dicom,.doc,.docx"
            onChange={handleFileChange}
          />

          {/* Drag & Drop / Click to Browse Dropzone */}
          <div
            onDragOver={(e) => {
              e.preventDefault()
              setIsDragging(true)
            }}
            onDragLeave={() => setIsDragging(false)}
            onDrop={handleDrop}
            onClick={() => fileInputRef.current?.click()}
            style={{
              border: `2px dashed ${isDragging ? 'var(--clr-primary)' : 'var(--border-default)'}`,
              background: isDragging ? 'rgba(14, 165, 233, 0.08)' : 'var(--bg-base)',
              borderRadius: '12px',
              padding: '24px 16px',
              textAlign: 'center',
              cursor: 'pointer',
              transition: 'all 0.2s ease',
            }}
          >
            <UploadCloud size={28} color="var(--clr-primary)" style={{ margin: '0 auto 8px' }} />
            <div style={{ fontWeight: 600, fontSize: '0.9rem', color: 'var(--text-primary)' }}>
              Click to browse local files or drag & drop here
            </div>
            <div style={{ fontSize: '0.78rem', color: 'var(--text-muted)', marginTop: '4px' }}>
              Supports PDF reports, JPEG/PNG scans, DICOM, and Clinical Docs
            </div>
          </div>

          {/* List of Selected Local Files */}
          {formData.attachments.length > 0 && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '8px', marginTop: '12px' }}>
              {formData.attachments.map((att, i) => (
                <div
                  key={i}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    padding: '10px 14px',
                    background: 'var(--bg-surface)',
                    borderRadius: '8px',
                    border: '1px solid var(--border-default)',
                  }}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                    {att.fileType.includes('image') ? (
                      <ImageIcon size={18} color="var(--clr-accent)" />
                    ) : (
                      <FileSpreadsheet size={18} color="var(--clr-primary)" />
                    )}
                    <div>
                      <div style={{ fontWeight: 600, fontSize: '0.88rem', color: 'var(--text-primary)' }}>
                        {att.fileName}
                      </div>
                      <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>
                        {formatFileSize(att.fileSize)} • {att.fileType}
                      </div>
                    </div>
                  </div>
                  <button
                    type="button"
                    onClick={(e) => {
                      e.stopPropagation()
                      handleRemoveAttachment(i)
                    }}
                    style={{
                      background: 'transparent',
                      border: 'none',
                      color: 'var(--clr-danger)',
                      cursor: 'pointer',
                      padding: '4px',
                      borderRadius: '4px',
                    }}
                    title="Remove file"
                  >
                    <Trash2 size={16} />
                  </button>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Form Action Buttons */}
        <div className="form-actions">
          <Button variant="secondary" type="button" onClick={onClose} disabled={loading}>
            Cancel
          </Button>
          <Button variant="primary" type="submit" icon={Save} loading={loading}>
            {isEdit ? 'Save Changes' : 'Create Record'}
          </Button>
        </div>
      </form>
    </Modal>
  )
}
