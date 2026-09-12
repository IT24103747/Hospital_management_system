import { useState } from 'react'
import {
  Calendar,
  User,
  Stethoscope,
  FileText,
  Paperclip,
  ExternalLink,
  Activity,
  Pill,
  FlaskConical,
  FileSpreadsheet,
  Download,
  CheckCircle2,
  Image as ImageIcon,
} from 'lucide-react'
import Modal from '../../../components/Modal'
import Button from '../../../components/Button'
import Badge from '../../../components/Badge'

export default function MedicalRecordDetailModal({
  record,
  onClose,
  onEdit,
  canEdit = true,
}) {
  const [previewAttachment, setPreviewAttachment] = useState(null)

  if (!record) return null

  const getTypeVariant = (type) => {
    switch (type) {
      case 'Consultation': return 'primary'
      case 'LabReport': return 'info'
      case 'DischargeSummary': return 'warning'
      case 'Prescription': return 'accent'
      default: return 'default'
    }
  }

  const formatFileSize = (bytes) => {
    if (!bytes) return '0 KB'
    const kb = bytes / 1024
    if (kb < 1024) return `${kb.toFixed(1)} KB`
    return `${(kb / 1024).toFixed(1)} MB`
  }

  const handleViewAttachment = (e, att) => {
    e.preventDefault()
    if (att.fileUrl?.startsWith('data:') || att.fileUrl?.startsWith('blob:')) {
      const win = window.open('')
      if (win) {
        if (att.fileType.includes('image')) {
          win.document.write(
            `<body style="margin:0;background:#0f172a;display:flex;align-items:center;justify-content:center;min-height:100vh;"><img src="${att.fileUrl}" style="max-width:95vw;max-height:95vh;border-radius:8px;box-shadow:0 10px 30px rgba(0,0,0,0.5);" /></body>`
          )
        } else if (att.fileType.includes('pdf')) {
          win.document.write(
            `<iframe src="${att.fileUrl}" frameborder="0" style="border:0;top:0;left:0;bottom:0;right:0;width:100%;height:100%;" allowfullscreen></iframe>`
          )
        } else {
          win.location.href = att.fileUrl
        }
      } else {
        setPreviewAttachment(att)
      }
    } else if (att.fileUrl?.startsWith('http://') || att.fileUrl?.startsWith('https://')) {
      window.open(att.fileUrl, '_blank')
    } else {
      setPreviewAttachment(att)
    }
  }

  const handleDownloadSample = (att) => {
    const docContent = `MEDICORE HOSPITAL MANAGEMENT SYSTEM
========================================
OFFICIAL CLINICAL DIAGNOSTIC REPORT
Document: ${att.fileName}
File Type: ${att.fileType} | Size: ${formatFileSize(att.fileSize)}
Uploaded Date: ${new Date(att.uploadedAt || Date.now()).toLocaleDateString()}

PATIENT INFORMATION
-------------------
Patient Name:  ${record.patientName}
Patient Email: ${record.patientEmail}

CLINICAL ENCOUNTER DETAILS
--------------------------
Record ID:        #${record.medicalRecordId}
Record Type:      ${record.recordType}
Date of Visit:    ${new Date(record.recordDate).toLocaleDateString()}
Attending Doctor: ${record.doctorName || 'Assigned Clinician'} (${record.doctorSpecialization || 'General Medicine'})

PRIMARY DIAGNOSIS
-----------------
${record.diagnosis}

SYMPTOMS & CLINICAL OBSERVATIONS
--------------------------------
${record.symptoms}

TREATMENT PLAN & MEDICAL ADVICE
-------------------------------
${record.treatmentPlan}

${record.prescriptionNotes ? `PRESCRIPTIONS:\n${record.prescriptionNotes}\n\n` : ''}${record.labNotes ? `DIAGNOSTIC FINDINGS:\n${record.labNotes}\n\n` : ''}========================================
Status: ${record.status} (Verified)
Generated on: ${new Date().toLocaleString()}
`
    const blob = new Blob([docContent], { type: 'text/plain;charset=utf-8' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = att.fileName.endsWith('.pdf') ? att.fileName.replace('.pdf', '_Report.txt') : `${att.fileName}.txt`
    document.body.appendChild(a)
    a.click()
    document.body.removeChild(a)
    URL.revokeObjectURL(url)
  }

  return (
    <>
      <Modal
        open={Boolean(record)}
        onClose={onClose}
        title={
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <FileText size={20} color="var(--clr-primary)" />
            <span>Medical Record #{record.medicalRecordId}</span>
            <Badge variant={getTypeVariant(record.recordType)}>{record.recordType}</Badge>
            <Badge variant={record.status === 'Finalized' ? 'success' : 'default'} dot>
              {record.status}
            </Badge>
          </div>
        }
        subtitle={`Patient: ${record.patientName} (${record.patientEmail})`}
        size="lg"
        id="medical-record-detail-modal"
      >
        <div className="mr-detail">
          {/* Top Info Banner */}
          <div className="mr-detail__grid">
            <div className="mr-detail__card">
              <div className="mr-detail__card-label"><User size={14} /> Patient</div>
              <div className="mr-detail__card-val font-semibold">{record.patientName || 'Unknown'}</div>
              <div className="mr-detail__card-sub">{record.patientEmail}</div>
            </div>

            <div className="mr-detail__card">
              <div className="mr-detail__card-label"><Stethoscope size={14} /> Clinician</div>
              <div className="mr-detail__card-val font-semibold">{record.doctorName || 'Assigned Clinician'}</div>
              <div className="mr-detail__card-sub">{record.doctorSpecialization || 'General Medicine'}</div>
            </div>

            <div className="mr-detail__card">
              <div className="mr-detail__card-label"><Calendar size={14} /> Recorded Date</div>
              <div className="mr-detail__card-val font-semibold">
                {new Date(record.recordDate).toLocaleDateString(undefined, {
                  year: 'numeric',
                  month: 'short',
                  day: 'numeric',
                })}
              </div>
              <div className="mr-detail__card-sub">
                {record.followUpDate ? `Follow-up: ${new Date(record.followUpDate).toLocaleDateString()}` : 'No follow-up'}
              </div>
            </div>
          </div>

          {/* Diagnosis & Clinical Findings */}
          <div className="form-section" style={{ marginTop: '8px' }}>
            <h5 className="form-section__title" style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
              <Activity size={15} /> Clinical Evaluation
            </h5>
            <div style={{ background: 'var(--bg-base)', padding: '14px 16px', borderRadius: '10px', border: '1px solid var(--border-default)' }}>
              <div style={{ fontSize: '0.8rem', fontWeight: 600, color: 'var(--text-muted)', textTransform: 'uppercase' }}>Primary Diagnosis</div>
              <div style={{ fontSize: '1.1rem', fontWeight: 700, color: 'var(--clr-primary)', marginTop: '2px', marginBottom: '10px' }}>
                {record.diagnosis}
              </div>

              <div style={{ fontSize: '0.8rem', fontWeight: 600, color: 'var(--text-muted)', textTransform: 'uppercase' }}>Symptoms & Observations</div>
              <div style={{ fontSize: '0.92rem', color: 'var(--text-primary)', lineHeight: 1.5, marginTop: '2px', marginBottom: '10px' }}>
                {record.symptoms}
              </div>

              <div style={{ fontSize: '0.8rem', fontWeight: 600, color: 'var(--text-muted)', textTransform: 'uppercase' }}>Treatment Plan & Medical Advice</div>
              <div style={{ fontSize: '0.92rem', color: 'var(--text-primary)', lineHeight: 1.5, marginTop: '2px' }}>
                {record.treatmentPlan}
              </div>
            </div>
          </div>

          {/* Prescription & Lab Notes */}
          {(record.prescriptionNotes || record.labNotes) && (
            <div className="form-section">
              <h5 className="form-section__title" style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                <Pill size={15} /> Prescriptions & Diagnostics
              </h5>
              <div className="form-grid form-grid--2">
                {record.prescriptionNotes && (
                  <div style={{ background: 'var(--bg-base)', padding: '14px', borderRadius: '10px', border: '1px solid var(--border-default)' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '0.8rem', fontWeight: 700, color: 'var(--clr-accent)', textTransform: 'uppercase', marginBottom: '6px' }}>
                      <Pill size={14} /> Prescribed Medications
                    </div>
                    <pre style={{ margin: 0, fontFamily: 'var(--font-mono)', fontSize: '0.84rem', color: 'var(--text-primary)', whiteSpace: 'pre-wrap', lineHeight: 1.4 }}>
                      {record.prescriptionNotes}
                    </pre>
                  </div>
                )}

                {record.labNotes && (
                  <div style={{ background: 'rgba(8,145,178,0.06)', padding: '14px', borderRadius: '10px', border: '1px solid rgba(8,145,178,0.2)' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '0.8rem', fontWeight: 700, color: 'var(--clr-info)', textTransform: 'uppercase', marginBottom: '6px' }}>
                      <FlaskConical size={14} /> Diagnostic Findings
                    </div>
                    <div style={{ fontSize: '0.88rem', color: 'var(--text-primary)', lineHeight: 1.4 }}>
                      {record.labNotes}
                    </div>
                  </div>
                )}
              </div>
            </div>
          )}

          {/* Read-Only Attachments Section */}
          <div className="form-section">
            <h5 className="form-section__title" style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
              <Paperclip size={15} /> Attached Reports & Scans ({record.attachments?.length || 0})
            </h5>

            {record.attachments && record.attachments.length > 0 ? (
              <div className="mr-attachments-list">
                {record.attachments.map((att) => (
                  <div key={att.attachmentId} className="mr-attachment-item">
                    <div className="mr-attachment-item__left">
                      {att.fileType.includes('image') ? (
                        <ImageIcon size={22} color="var(--clr-accent)" />
                      ) : (
                        <FileSpreadsheet size={22} color="var(--clr-primary)" />
                      )}
                      <div>
                        <div className="mr-attachment-item__name">{att.fileName}</div>
                        <div className="mr-attachment-item__meta">
                          {att.fileType} • {formatFileSize(att.fileSize)} • {new Date(att.uploadedAt).toLocaleDateString()}
                        </div>
                      </div>
                    </div>
                    <div className="mr-attachment-item__actions">
                      <button
                        type="button"
                        onClick={(e) => handleViewAttachment(e, att)}
                        className="mr-link-btn"
                        title="View / Download Document"
                      >
                        <ExternalLink size={15} />
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            ) : (
              <div
                style={{
                  border: '1px dashed var(--border-default)',
                  borderRadius: '8px',
                  padding: '16px',
                  textAlign: 'center',
                  background: 'var(--bg-base)',
                }}
              >
                <div style={{ fontSize: '0.85rem', color: 'var(--text-muted)' }}>
                  No diagnostic files or scans attached to this medical record.
                </div>
              </div>
            )}
          </div>

          {/* Footer Actions */}
          <div className="form-actions" style={{ marginTop: '10px' }}>
            {canEdit && (
              <Button variant="secondary" onClick={() => onEdit(record)}>
                Edit Record
              </Button>
            )}
            <Button variant="primary" onClick={onClose}>
              Close
            </Button>
          </div>
        </div>
      </Modal>

      {/* In-App Attachment Preview Modal */}
      {previewAttachment && (
        <Modal
          open={Boolean(previewAttachment)}
          onClose={() => setPreviewAttachment(null)}
          title={
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <FileSpreadsheet size={18} color="var(--clr-primary)" />
              <span>{previewAttachment.fileName}</span>
            </div>
          }
          subtitle={`${previewAttachment.fileType} • ${formatFileSize(previewAttachment.fileSize)}`}
          size="md"
        >
          <div style={{ padding: '8px 0', display: 'flex', flexDirection: 'column', gap: '14px' }}>
            <div
              style={{
                background: 'var(--bg-base)',
                border: '1px solid var(--border-default)',
                borderRadius: '10px',
                padding: '16px',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px', color: 'var(--clr-primary)', fontWeight: 700, fontSize: '0.92rem', marginBottom: '8px' }}>
                <CheckCircle2 size={16} /> MediCore Diagnostic Repository
              </div>
              <p style={{ fontSize: '0.84rem', color: 'var(--text-primary)', margin: '0 0 8px', lineHeight: 1.5 }}>
                This clinical attachment is linked to <strong>Medical Record #{record.medicalRecordId}</strong> for patient{' '}
                <strong>{record.patientName}</strong>.
              </p>
              <div style={{ fontSize: '0.78rem', color: 'var(--text-muted)', lineHeight: 1.4 }}>
                <strong>Diagnosis:</strong> {record.diagnosis}<br />
                <strong>Attending Clinician:</strong> {record.doctorName || 'Assigned Clinician'}
              </div>
            </div>

            <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
              <Button variant="secondary" onClick={() => setPreviewAttachment(null)}>
                Close Preview
              </Button>
              <Button
                variant="primary"
                icon={Download}
                onClick={() => handleDownloadSample(previewAttachment)}
              >
                Download Report
              </Button>
            </div>
          </div>
        </Modal>
      )}
    </>
  )
}
